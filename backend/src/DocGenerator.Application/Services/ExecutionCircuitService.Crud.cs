using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    public async Task<ExecutionCircuitDto> CreateAsync(int branchId, int actorUserId, string? actorName, string name, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        var trimmed = NormalizeName(name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الدائرة مطلوب");

        // ملكية المنشئ (§8.4): دائرة رئيس الشعبة تلحق بشعبته تلقائيًا، ودائرة
        // رئيس القسم ملك القسم (`null`)؛ وغير الرؤساء مرفوض (الإدارة تنقل فقط).
        var ownerSection = await ResolveOwnerSectionAsync(branchId, actorUserId, ct);

        var circuit = new ExecutionCircuit
        {
            BranchId = branchId,
            SectionId = ownerSection?.Id,
            Name = trimmed,
            NameNorm = norm,
            IsActive = true,
            CreatedById = actorUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        try
        {
            await _tx.RunAsync(async token =>
            {
                // فحص ودّي قبل الإدراج لبيان المالك في الرسالة؛ القيد الفريد في القاعدة
                // يبقى الحارس النهائي ضد السباق (والالتقاط أدناه يغطي هذه الحالة النادرة).
                var existing = await FindCircuitByNameAsync(branchId, norm, null, token);
                if (existing is not null)
                    throw new DocumentConflictException(
                        await DuplicateCircuitMessageAsync(existing, ownerSection?.Id, token));
                await _circuits.AddAsync(circuit, token);
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
                await _audit.LogAsync(actorName, "create_circuit", null, null,
                    $"أدخل دائرة تنفيذ «{trimmed}» في فرعه", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            // سباق حقيقي بين فحصين متزامنين: الرسالة العامة تحمل القيد الفرعي نفسه.
            throw new DocumentConflictException(
                $"الدائرة «{trimmed}» موجودة مسبقًا في فرعك — لا يمكن تكرار اسم الدائرة داخل الفرع", ex);
        }
        return new ExecutionCircuitDto(circuit.Id, circuit.BranchId, null, circuit.Name, circuit.IsActive, 0, 0, circuit.Version,
            circuit.SectionId, ownerSection?.Name);
    }

    public async Task<ExecutionCircuitDto> RenameAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, string name, long? version, CancellationToken ct = default, int? actorUserId = null)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        var trimmed = NormalizeName(name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);

        ExecutionCircuitDto result = null!;
        try
        {
            await _tx.RunAsync(async token =>
            {
                var circuit = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
                if (version is not null && circuit.Version != version.Value)
                    throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
                // فحص ودّي قبل التسمية لبيان مالك الاسم المحجوز؛ القيد الفريد يبقى الحارس النهائي.
                var conflict = await FindCircuitByNameAsync(branchId, norm, circuitId, token);
                if (conflict is not null)
                    throw new DocumentConflictException(
                        await DuplicateCircuitMessageAsync(conflict, ownerSectionId, token));
                if (circuit.Name != trimmed)
                {
                    var oldName = circuit.Name;
                    circuit.Name = trimmed;
                    circuit.NameNorm = norm;
                    circuit.UpdatedAt = DateTime.UtcNow;
                    circuit.Version++;

                    // تحديث جماعي فوري لكل الملفات في نفس المعاملة + رفع Version يدويًا + إعادة بناء الثلاثية.
                    var now = DateTime.UtcNow;
                    var docs = await _documents.ListByCircuitAsync(circuitId, includeDeleted: false, token);
                    var renamedAudit = new List<(Document Doc, Dictionary<string, string?> Before)>();
                    foreach (var d in docs)
                    {
                        // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة.
                        var before = DocumentChangeTracker.Capture(d);
                        d.Court = trimmed;
                        d.CourtNorm = string.IsNullOrWhiteSpace(trimmed) ? null : ArabicNameNormalizer.Normalize(trimmed);
                        d.SearchText = DocumentSearchTextBuilder.Build(d);
                        d.FullData = DocumentSearchTextBuilder.BuildFullData(d);
                        d.UpdatedAt = now;
                        d.Version++;
                        _documents.Update(d);

                        // وقوعّة التسمية على كل ملف متأثر (§5.2) — وإلا مرّت الجماعية بلا أثر ملفي.
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = d.Id,
                            OccurrenceType = OccurrenceTypeCatalog.CircuitRenamed,
                            Source = OccurrenceSourceCatalog.System,
                            EventDate = now,
                            FileNumber = d.FileNumber,
                            FileType = d.FileType,
                            Year = int.TryParse(d.FileYear, out var oy) ? oy : null,
                            FromCircuitName = oldName,
                            ToCircuitName = trimmed,
                            CreatedById = actorUserId ?? circuit.CreatedById,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, token);
                        renamedAudit.Add((d, before));
                    }
                    // إعادة التسمية تُحدث أيضًا DelegatedCourt/Norm في صفوف DocumentDelegation حيث DelegatedCircuitId = الدائرة.
                    var delegations = await _delegations.ListByDelegatedCircuitAsync(circuitId, token);
                    foreach (var g in delegations)
                    {
                        g.DelegatedCourt = trimmed;
                        g.DelegatedCourtNorm = string.IsNullOrWhiteSpace(trimmed) ? null : ArabicNameNormalizer.Normalize(trimmed);
                        g.UpdatedAt = now;
                        _delegations.Update(g);
                    }
                    // ترحيل نص التنبيهات المدمجة عبر Outbox داخل المعاملة نفسها:
                    // المفتاح الدلالي (الدائرة) هو ما يربط التنبيهات، فلا حاجة
                    // للبحث النصي الهش في صفوف HeadAlerts.
                    await _alertOutbox.EnqueueCircuitRenamedAsync(
                        circuitId,
                        HeadAlertMessages.CircuitPendingDocuments(new(circuit.Name)),
                        now,
                        token);
                    _circuits.Update(circuit);
                    await _uow.SaveChangesAsync(token);
                    _cache?.Invalidate(ReferenceDataGroups.Circuits);
                    // تدقيق حقلي لكل ملف (S5) — داخل المعاملة بعد الحفظ الرئيسي.
                    foreach (var (doc, before) in renamedAudit)
                        await LogCircuitDocumentChangeAsync(doc, actorName, "rename_circuit",
                            $"أعاد تسمية دائرة الملف ({doc.Id}) من «{oldName}» إلى «{trimmed}»", before, token);
                    await _audit.LogAsync(actorName, "rename_circuit", null, null,
                        $"أعاد تسمية دائرة التنفيذ من «{oldName}» إلى «{trimmed}» مع تحديث {docs.Count} ملفًا و{delegations.Count} إنابة", token);
                }
                result = await ToDtoAsync(circuit, token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            // سباق حقيقي بين فحصين متزامنين: الرسالة العامة تحمل القيد الفرعي نفسه.
            throw new DocumentConflictException(
                $"الدائرة «{trimmed}» موجودة مسبقًا في فرعك — لا يمكن تكرار اسم الدائرة داخل الفرع", ex);
        }
        return result;
    }

    public async Task<ExecutionCircuitDto> SetActiveAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, bool isActive, long? version, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        ExecutionCircuitDto result = null!;
        await _tx.RunAsync(async token =>
        {
            var tracked = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
            if (version is not null && tracked.Version != version.Value)
                throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
            tracked.IsActive = isActive;
            tracked.UpdatedAt = DateTime.UtcNow;
            tracked.Version++;
            _circuits.Update(tracked);
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
            await _audit.LogAsync(actorName, isActive ? "activate_circuit" : "deactivate_circuit",
                null, null, $"{(isActive ? "فعّل" : "عطّل")} دائرة التنفيذ «{tracked.Name}»", token);
            result = await ToDtoAsync(tracked, token);
        }, ct);
        return result;
    }

    /// <summary>
    /// حذف الدائرة نهائيًا (تعديل البند 41 — قرار المالك 2026-10-17): الترتيب
    /// **الحارس ← الوارد المعلقة ← فصل الوارد ← التطهير ← إزالة الدائرة ← الحفظ ← التدقيق**.
    /// العلة التقنية: فصل الوارد قبل التطهير يمنع تعارض أثر التتبّع مع صفوف الإنابة
    /// المحذوفة تسلسليًا ضمن التطهير (الإنتاج محصّن أصلًا بحارس
    /// DocumentService.DeleteAsync: يمنع حذف أي ملف له إنابة صادرة أو مناب أو استئناف —
    /// فلا صف محذوف يحمل إنابة/مناب/استئنافًا، والمناب بالقيد Restrict غير قابل للتحقق إنتاجيًا).
    /// الحارس يعدّ الأحياء فقط (محتسبين المعلقين منها)؛ سلة المحذوفات تُطهَّر نهائيًا
    /// (CASCADE يزيل توابعها، SET NULL يفك مراجعها، وAuditLogs/DocumentFieldChanges
    /// بلا FK فيبقيان تاريخًا كاملًا — إعلان صريح لا سهو).
    /// </summary>
    public async Task DeleteAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        await _tx.RunAsync(async token =>
        {
            var circuit = await GetOwnedAsync(circuitId, branchId, ownerSectionId, token);
            // حارس الحذف: الأحياء فقط (تعديل البند 41) + الإنابات الواردة المعلقة.
            var counts = await _documents.CountByCircuitsAsync(new List<int> { circuitId }, token);
            counts.TryGetValue(circuitId, out var v);
            if (v.FileCount > 0)
                throw new DocumentConflictException($"لا يمكن حذف الدائرة — أفرغها أولًا (بها {v.FileCount} ملفًا)");
            // C1: متجاوزًا فلتر المصدر المحذوف — المعلقة تحظر أيًا كان حال مصدرها.
            var pendingIncoming = await _delegations.CountPendingIncomingByCircuitIncludingDeletedAsync(circuitId, token);
            if (pendingIncoming > 0)
                throw new DocumentConflictException($"لا يمكن حذف الدائرة — بها {pendingIncoming} إنابة واردة معلقة تستهدفها");
            // التاريخ المجمد (البند 45): الإنابات غير المعلقة التي استهدفت الدائرة تُفك
            // عن المرجع (DelegatedCircuitId=null) مع إبقاء الاسم النصي — وإلا انفجر الحذف
            // بانتهاك FK (Restrict) رغم اجتياز الحارس. المعلقة محظورة أعلاه فلا تصل هنا.
            // C1: متجاوزًا الفلتر أيضًا — إنابة مصدرها محذوف ما زال صفها يشير للدائرة.
            // فصل الوارد قبل التطهير (علة الترتيب المذكورة أعلاه).
            var incoming = await _delegations.ListByDelegatedCircuitIncludingDeletedAsync(circuitId, token);
            var detached = 0;
            var now = DateTime.UtcNow;
            foreach (var g in incoming)
            {
                if (g.Status == DelegationStatusCatalog.PendingHead)
                    throw new DocumentConflictException("لا يمكن حذف الدائرة — بها إنابة واردة معلقة تستهدفها");
                g.DelegatedCircuitId = null;
                g.UpdatedAt = now;
                _delegations.Update(g);
                detached++;
            }
            // تطهير سلة المحذوفات نهائيًا (قرار المالك — تعديل البند 41): حذف فيزيائي
            // ضمن معاملة الحذف نفسها؛ والتسلسل الموثوق به: CASCADE للتوابع (الوقوعات/
            // أرقام الأساس/الإسنادات/الأطراف/الكفلاء/الورثة/الأموال/الاستئنافات/إنابات
            // الملف المحذوف)، SET NULL للتنبيهات/المرسلات/خطابات المراجعة — والعلة
            // تسهل اختبارها: أي صف شاذ يفشل بصوت عالٍ داخل المعاملة مع rollback.
            var purged = 0;
            var deletedDocs = await _documents.ListDeletedByCircuitAsync(circuitId, token);
            var deletedDocIds = deletedDocs.Select(d => d.Id).ToList();
            // FK أرقام الأساس على المستند Restrict (V-18/E17): التطهير الفيزيائي للدائرة
            // يزيل صفوف أرقام الملفات المحذوفة صراحةً قبل حذف المستندات (وإلا انفجر الحذف).
            if (deletedDocIds.Count > 0)
            {
                if (_baseNumberRepository is not null)
                {
                    foreach (var deletedDocId in deletedDocIds)
                    {
                        var rows = await _baseNumberRepository.ListByDocumentAsync(
                            deletedDocId, includeDeletedDocument: true, token);
                        foreach (var row in rows)
                            _baseNumberRepository.Remove(row);
                    }
                }
                else
                {
                    var idSet = deletedDocIds.ToHashSet();
                    var baseRows = (await _baseNumbers.ListAsync(token))
                        .Where(b => idSet.Contains(b.DocumentId))
                        .ToList();
                    foreach (var row in baseRows)
                        _baseNumbers.Remove(row);
                }
            }
            var linkedAlerts = await _alerts.ListActiveByCircuitOrDocumentsAsync(circuitId, deletedDocIds, token);
            foreach (var alert in linkedAlerts)
            {
                alert.IsArchived = true;
                alert.ArchivedAt = now;
            }
            foreach (var d in deletedDocs)
            {
                _documents.Remove(d);
                purged++;
            }
            _circuits.Remove(circuit);
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
            // عبارة التطهير تُلحق للتدقيق فقط عند purged > 0 (لا «مع» مكررة ولا «تطهير 0»).
            var purgeClause = purged > 0 ? $"، وطُهِّر نهائيًا {purged} ملفًا محذوفًا" : string.Empty;
            await _audit.LogAsync(actorName, "delete_circuit", null, null,
                $"حذف دائرة التنفيذ «{circuit.Name}» مع فك {detached} إنابة مكتملة عن مرجعها (الاسم محفوظ){purgeClause}", token);
        }, ct);
    }
}
