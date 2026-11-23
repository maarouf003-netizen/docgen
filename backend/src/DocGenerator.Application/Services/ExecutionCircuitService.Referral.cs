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
    public async Task<ReferCircuitFilesResult> ReferFilesAsync(
        int sourceCircuitId, int branchId, int? ownerSectionId, int actorUserId, string? actorName,
        ReferCircuitFilesRequest request, CancellationToken ct = default, string? idempotencyKey = null)
    {
        // RF-011: حجز المفتاح قبل أي تحقق يستحق 400 بلا حجز — التحققات الثقيلة داخل
        // النواة؛ التكرار بنفس البصمة يُعيد اللقطة المخزنة نفسها. البصمة بمعرفات
        // مرتبة حتى لا تُبطل إعادة ترتيب المصفوفة التكرار.
        var orderedIds = (request.FileIds ?? new List<int>()).Distinct().OrderBy(id => id).ToList();
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "circuits.refer-files",
            actorUserId, idempotencyKey,
            IdempotencyGuard.Fingerprint(new
            {
                sourceCircuitId,
                fileIds = orderedIds,
                request.TargetLawyerId,
                request.TargetCircuitId,
                branchId,
                ownerSectionId,
            }), ct);

        ReferCircuitFilesResult result;
        try
        {
            result = await ReferFilesCoreAsync(sourceCircuitId, branchId, ownerSectionId, actorUserId, actorName, request, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        // اللقطة بشكل جسم الاستجابة نفسه (مع البانر) ليُعاد حرفيًا عند التكرار.
        await IdempotencyGuard.CompleteAsync(_idempotency, ticket,
            IdempotencyGuard.Snapshot(new
            {
                result.ReferredCount,
                result.SkippedCount,
                result.RemainingCount,
                reminder = EmptyingReminderBanner,
            }), ct: ct);
        return result;
    }

    /// <summary>نواة الإحالة (تحققات + نقل + تدقيق) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<ReferCircuitFilesResult> ReferFilesCoreAsync(
        int sourceCircuitId, int branchId, int? ownerSectionId, int actorUserId, string? actorName,
        ReferCircuitFilesRequest request, CancellationToken ct)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        if (request.FileIds is null || request.FileIds.Count == 0)
            throw new ArgumentException("حدد ملفًا واحدًا على الأقل للإحالة");
        if (sourceCircuitId == request.TargetCircuitId)
            throw new ArgumentException("دائرة المصدر والهدف متماثلتان");

        var distinctIds = request.FileIds.Distinct().ToList();
        int referred = 0, skipped = 0, remaining = 0;

        await _tx.RunAsync(async token =>
        {
            var source = await GetOwnedAsync(sourceCircuitId, branchId, ownerSectionId, token);
            var target = await GetOwnedAsync(request.TargetCircuitId, branchId, ownerSectionId, token);
            if (!target.IsActive)
                throw new ArgumentException("دائرة الهدف معطلة — اختر دائرة نشطة");
            var sourceName = source.Name;
            var targetName = target.Name;

            // بدء الإفراغ يعطّل الدائرة الملغاة تلقائيًا (لا ملفات جديدة أثناء التفريغ).
            if (source.IsActive)
            {
                source.IsActive = false;
                source.UpdatedAt = DateTime.UtcNow;
                source.Version++;
                _circuits.Update(source);
            }

            var lawyer = await _users.GetByIdAsync(request.TargetLawyerId, token);
            if (lawyer is null || lawyer.Role != UserRole.Lawyer || !lawyer.IsActive)
                throw new ArgumentException("المحامي الهدف غير موجود أو معطل");
            if (lawyer.BranchId != branchId)
                throw new ArgumentException("المحامي الهدف ليس ضمن فرع الدائرة");
            var targetFullName = string.IsNullOrWhiteSpace(lawyer.FullName) ? lawyer.Username : lawyer.FullName;

            var docs = await _documents.ListByIdsForUpdateAsync(distinctIds, token);
            var byId = docs.ToDictionary(d => d.Id);
            foreach (var id in distinctIds)
            {
                if (!byId.TryGetValue(id, out var doc) || doc.IsDeleted)
                    throw new ArgumentException($"الملف ({id}) غير موجود في دائرة المصدر");
                if (doc.ExecutionCircuitId != sourceCircuitId)
                    throw new ArgumentException($"الملف ({id}) ليس من دائرة المصدر");
            }
            var now = DateTime.UtcNow;
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
            var referredAudit = new List<(Document Doc, Dictionary<string, string?> Before)>();
            foreach (var doc in docs)
            {
                // تخطي المسند لمالكه الحالي بصمت مع عدّاد متخطىً.
                if (doc.CreatedById == request.TargetLawyerId && doc.ExecutionCircuitId == request.TargetCircuitId)
                {
                    skipped++;
                    continue;
                }
                var oldNumber = EffectiveFileIdentity.Number(doc, currentYear) ?? doc.FileNumber;
                var oldType = doc.FileType;
                var oldYear = EffectiveFileIdentity.Year(doc, currentYear) ?? doc.FileYear;

                // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة.
                var before = DocumentChangeTracker.Capture(doc);
                doc.CreatedById = request.TargetLawyerId;
                doc.Lawyer = targetFullName;
                doc.ExecutionCircuitId = request.TargetCircuitId;
                doc.Court = target.Name;
                doc.CourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                doc.FileNumber = null;
                doc.FileType = null;
                doc.FileYear = null;
                doc.NeedsRegistration = true;
                doc.IsDraft = false;
                doc.UpdatedAt = now;
                doc.Version++;
                // V-07: صفوف الدائرة القديمة تبقى غير ملغاة (توثيق وحجز في دائرتها/سنتها)،
                // ولا تظهر في هوية الدائرة الجديدة. لا حذف ولا إلغاء هنا.
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                _documents.Update(doc);

                // وقعة الإحالة تُبنى بقاموس Details متغيرًا (يدخل الذيل عند نقل إنابة المناب)
                // وتُضاف في النهاية بتمريرة تسلسل واحدة — القاعدتان 2/3 (C2) تليانيان بلا استبعاد.
                var occDetails = new Dictionary<string, string>
                {
                    ["fromCircuit"] = sourceName,
                    ["toCircuit"] = targetName,
                    ["oldNumber"] = oldNumber ?? string.Empty,
                    ["oldType"] = oldType ?? string.Empty,
                    ["oldYear"] = oldYear ?? string.Empty,
                };
                int? oldYearNum = int.TryParse(oldYear, out var oy) ? oy : null;

                // الإنابات تتبع ملفاتها: المنيب المنقول يسحب إناباته كما هي؛ المناب المنقول يُحدَّث DelegatedCourt لوجهته.
                var outgoing = await _delegations.ListBySourceAsync(doc.Id, token);
                foreach (var g in outgoing)
                {
                    g.UpdatedAt = now;
                    _delegations.Update(g);
                }
                // قاعدة 2 (C2): المنيب المنقول يبلّغ مناباته الجاهزة — وقعة source-file-moved
                // على كل مناب مرتبط بإنابة صادرة، بأرقام المنيب القديمة. إنابة بلا مناب بعد → تُتخطى.
                if (outgoing.Count > 0)
                {
                    var mirrorMap = await _documents.MapMirrorsBySourceDelegationIdsAsync(
                        outgoing.Select(g => g.Id).ToList(), token);
                    foreach (var g in outgoing)
                    {
                        if (!mirrorMap.TryGetValue(g.Id, out var mirrorId)) continue;
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = mirrorId,
                            OccurrenceType = OccurrenceTypeCatalog.SourceFileMoved,
                            Source = OccurrenceSourceCatalog.System,
                            EventDate = now,
                            FileNumber = oldNumber,
                            FileType = oldType,
                            Year = oldYearNum,
                            FromCircuitName = sourceName,
                            ToCircuitName = targetName,
                            Details = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["fromCircuit"] = sourceName,
                                ["toCircuit"] = targetName,
                            }),
                            CreatedById = actorUserId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, token);
                    }
                }
                // قاعدة 3 (C2): إن كان الملف المنقول منابًا (له SourceDelegationId) وإنابته
                // داخلية تستهدف دائرة المصدر، تُحدَّث للوجهة — وذيل تغيّر الوجهة يُدوَّن على
                // وقعة إحالة المناب، ووقعة متناظرة mirror-file-moved تُكتب على المنيب
                // (بأرقام المناب القديمة — لا تحميل للمنيب: FK فقط، وحارس حذف الملفات
                // يمنع حذف أي ملف له إنابة فبقاؤه مضمون).
                if (doc.SourceDelegationId is not null)
                {
                    var src = await _delegations.GetByIdAsync(doc.SourceDelegationId.Value, token);
                    if (src is not null && !src.IsExternal && src.DelegatedCircuitId == sourceCircuitId)
                    {
                        // oldCourt يُلتقط قبل الطفرات مع سقوط احتياطي إلى اسم دائرة المصدر عند الشذوذ.
                        var oldCourt = string.IsNullOrWhiteSpace(src.DelegatedCourt) ? sourceName : src.DelegatedCourt;
                        src.DelegatedCircuitId = request.TargetCircuitId;
                        src.DelegatedCourt = target.Name;
                        src.DelegatedCourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                        src.UpdatedAt = now;
                        _delegations.Update(src);
                        occDetails["delegationCircuitChange"] = $"من «{oldCourt}» إلى «{targetName}»";
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = src.SourceDocumentId,
                            OccurrenceType = OccurrenceTypeCatalog.MirrorFileMoved,
                            Source = OccurrenceSourceCatalog.System,
                            EventDate = now,
                            FileNumber = oldNumber,
                            FileType = oldType,
                            Year = oldYearNum,
                            FromCircuitName = sourceName,
                            ToCircuitName = targetName,
                            Details = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["fromCircuit"] = sourceName,
                                ["toCircuit"] = targetName,
                            }),
                            CreatedById = actorUserId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, token);
                    }
                }
                var occ = new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    OccurrenceType = OccurrenceTypeCatalog.CircuitReferred,
                    Source = OccurrenceSourceCatalog.System,
                    EventDate = now,
                    FileNumber = oldNumber,
                    FileType = oldType,
                    Year = oldYearNum,
                    FromCircuitName = sourceName,
                    ToCircuitName = targetName,
                    Details = System.Text.Json.JsonSerializer.Serialize(occDetails),
                    CreatedById = actorUserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _occurrences.AddAsync(occ, token);
                referredAudit.Add((doc, before));
                referred++;
            }
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);

            // الواردة المعلقة بلا ملف مناب تُعاد توجيهها للدائرة الجديدة.
            var incoming = await _delegations.ListByDelegatedCircuitAsync(sourceCircuitId, token);
            var now2 = DateTime.UtcNow;
            foreach (var g in incoming.Where(g => g.Status == DelegationStatusCatalog.PendingHead))
            {
                g.DelegatedCircuitId = request.TargetCircuitId;
                g.DelegatedCourt = target.Name;
                g.DelegatedCourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                g.UpdatedAt = now2;
                _delegations.Update(g);
            }
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);

            // تدقيق حقلي لكل ملف محال (S5) — داخل المعاملة بعد الحفظ الرئيسي.
            foreach (var (doc, before) in referredAudit)
                await ExecutionCircuitRules.LogDocumentChangeAsync(_audit, doc, actorName, "refer_circuit_files",
                    $"أحال الملف ({doc.Id}) من دائرة «{sourceName}» إلى دائرة «{targetName}» للمحامي {targetFullName}", before, token);

            await _audit.LogAsync(actorName, "refer_circuit_files", null, null,
                $"أحال {referred} ملفًا من دائرة «{sourceName}» إلى دائرة «{targetName}» للمحامي {targetFullName} (متخطى {skipped})", token);

            // تنبيه المحامي المدمج (لكل محامٍ × دائرة) عبر Outbox داخل المعاملة.
            await EnqueuePendingAlertAsync(
                request.TargetLawyerId,
                branchId,
                actorUserId,
                request.TargetCircuitId,
                target.Name,
                now,
                token);
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);

            var counts = await _documents.CountByCircuitsAsync(new List<int> { sourceCircuitId }, token);
            remaining = counts.TryGetValue(sourceCircuitId, out var v) ? v.FileCount : 0;
        }, ct);

        return new ReferCircuitFilesResult(referred, skipped, remaining);
    }

    /// <summary>
    /// تسجيل تنبيه إعادة القيد عبر Outbox داخل معاملة العمل — المفتاح الدلالي
    /// (محامٍ × دائرة) يضمن إنشاءً أو تحديثًا واحدًا بلا بحث نصي.
    /// </summary>
    private Task EnqueuePendingAlertAsync(
        int lawyerId,
        int branchId,
        int actorUserId,
        int circuitId,
        string circuitName,
        DateTime occurredAt,
        CancellationToken token)
        => _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                HeadAlertKindCatalog.CircuitPendingDocuments,
                HeadAlertMessages.CircuitPendingDocuments(new(circuitName)),
                HeadAlertAudience.SingleLawyer,
                branchId,
                actorUserId,
                ExecutionCircuitId: circuitId,
                RecipientUserId: lawyerId),
            supersedePlan: null,
            occurredAt: occurredAt,
            ct: token);
}
