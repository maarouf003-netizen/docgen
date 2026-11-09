using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// التنفيذ الوحيد لكتابة <see cref="DocumentBaseNumber"/> وحقول رقم الملف.
/// كل مسار: معاملة + قفل (دائرة، سنة) مرتّب + إعادة قراءة + فحص صلب/لين + تدقيق.
/// </summary>
public sealed class DocumentNumberRegistry : IDocumentNumberRegistry
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentBaseNumberRepository _baseNumbers;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly ITransactionContext _txContext;
    private readonly IAuditLogger _audit;
    private readonly INumberWarningTokenService _tokens;
    private readonly NumberRegistryOptions _options;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DocumentNumberRegistry(
        IDocumentRepository documents,
        IDocumentBaseNumberRepository baseNumbers,
        IRepository<ExecutionCircuit> circuits,
        IUnitOfWork uow,
        ITransactionRunner tx,
        ITransactionContext txContext,
        IAuditLogger audit,
        INumberWarningTokenService tokens,
        NumberRegistryOptions options,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _documents = documents;
        _baseNumbers = baseNumbers;
        _circuits = circuits;
        _uow = uow;
        _tx = tx;
        _txContext = txContext;
        _audit = audit;
        _tokens = tokens;
        _options = options;
        _clock = clock;
        _timeZone = timeZone;
    }

    public bool AllowsCircuitlessLegacy => _options.AllowCircuitlessLegacy;

    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);

    public Task<NumberConflict?> FindConflictAsync(
        Document doc,
        int? excludeRowId,
        int? actorUserId,
        int circuitId,
        string typeKey,
        int year,
        string numberKey,
        CancellationToken ct)
        => FindConflictCoreAsync(doc, excludeRowId, actorUserId, circuitId, typeKey, year, numberKey, ct);

    private async Task<NumberConflict?> FindConflictCoreAsync(
        Document doc,
        int? excludeRowId,
        int? actorUserId,
        int circuitId,
        string typeKey,
        int year,
        string numberKey,
        CancellationToken ct,
        int? tokenDocumentId = null)
    {
        var fingerprintDocumentId = tokenDocumentId ?? doc.Id;
        var candidates = await _baseNumbers.FindConflictCandidatesAsync(typeKey, year, numberKey, ct);
        candidates = candidates.Where(c => c.Id != excludeRowId).ToList();
        if (candidates.Count == 0)
            return null;

        var hard = candidates
            .Where(c => c.VoidedAt is null
                && c.ExecutionCircuitId == circuitId
                && c.Document is { IsDeleted: false })
            .ToList();
        if (hard.Count > 0)
        {
            var self = hard.All(c => c.DocumentId == doc.Id);
            var hardMessage = self
                ? $"الرقم {numberKey} مستخدم سابقًا لهذا الملف في هذه الدائرة/السنة — أدخل رقمًا جديدًا أو استخدم التصحيح"
                : $"الرقم {numberKey} مستخدم بالفعل في الدائرة نفسها لسنة {year} ونوع {(string.IsNullOrWhiteSpace(doc.FileType) ? "—" : doc.FileType)}. لا يمكن تكرار الرقم في نفس الدائرة والنوع والسنة؛ اختر رقمًا آخر.";
            return new NumberConflict(
                true,
                hard.Select(c => c.Id).OrderBy(id => id).ToList(),
                hard.Select(c => c.DocumentId).Distinct().ToList(),
                year, circuitId, CircuitName(doc), doc.FileType ?? string.Empty, null, self,
                hardMessage, Fingerprint(fingerprintDocumentId, circuitId, typeKey, year, numberKey), null);
        }

        // اللين: صف ملغى (أي دائرة) أو صف غير ملغى في دائرة أخرى، وليس للملف نفسه (F5).
        var soft = candidates
            .Where(c => c.DocumentId != doc.Id)
            .Where(c => c.VoidedAt is not null
                || c.ExecutionCircuitId != circuitId
                || c.Document is { IsDeleted: true })
            .ToList();
        if (soft.Count == 0)
            return null;

        var fingerprint = Fingerprint(fingerprintDocumentId, circuitId, typeKey, year, numberKey);
        var rowIds = soft.Select(c => c.Id).OrderBy(id => id).ToList();
        var token = actorUserId is null
            ? null
            : _tokens.Issue(actorUserId.Value, fingerprintDocumentId, fingerprint, rowIds);
        var circuit = soft.Select(c => c.CircuitName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            ?? CircuitName(doc);
        var softMessage =
            $"تأكد من الرقم {numberKey}؛ لأنه مستخدم في ملف آخر (سنة {year}، نوع {(string.IsNullOrWhiteSpace(doc.FileType) ? "—" : doc.FileType)}، دائرة {circuit}). هل تريد إدخاله على أي حال؟";
        return new NumberConflict(
            false, rowIds, soft.Select(c => c.DocumentId).Distinct().ToList(),
            year, circuitId, circuit, doc.FileType ?? string.Empty, null, false,
            softMessage, fingerprint, token);
    }

    private string CircuitName(Document doc)
        => string.IsNullOrWhiteSpace(doc.Court) ? "—" : doc.Court;

    private static string Fingerprint(int documentId, int circuitId, string typeKey, int year, string numberKey)
        => string.Join('|', documentId.ToString(CultureInfo.InvariantCulture),
            circuitId.ToString(CultureInfo.InvariantCulture), typeKey,
            year.ToString(CultureInfo.InvariantCulture), numberKey);

    private Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
        => _txContext.HasActiveTransaction ? action() : _tx.RunAsync(_ => action(), ct);

    private Task InTransactionAsync(Func<Task> action, CancellationToken ct)
        => _txContext.HasActiveTransaction ? action() : _tx.RunAsync(async _ => await action(), ct);

    private async Task LockAsync(int circuitId, int year, CancellationToken ct)
    {
        if (_txContext.HasActiveTransaction)
            await _txContext.AcquireScopeLockAsync(
                string.Join(':', circuitId.ToString(CultureInfo.InvariantCulture),
                    year.ToString(CultureInfo.InvariantCulture)), ct);
    }

    /// <summary>
    /// قفل الملف أولًا: يُسلسل كل كتابات السجل لنفس الملف (قيد/تدوير/تصحيح/حذف/تجديد)
    /// حتى لو اختلفت السنة/الدائرة، ثم تُؤخذ أقفال النطاق مرتّبة لمنع الجمود.
    /// </summary>
    private async Task LockDocumentAsync(int documentId, CancellationToken ct)
    {
        if (_txContext.HasActiveTransaction)
            await _txContext.AcquireScopeLockAsync(
                $"doc:{documentId.ToString(CultureInfo.InvariantCulture)}", ct);
    }

    private async Task LockScopesAsync(IEnumerable<(int Circuit, int Year)> scopes, CancellationToken ct)
    {
        foreach (var scope in scopes.Distinct().OrderBy(s => s.Circuit).ThenBy(s => s.Year))
            await LockAsync(scope.Circuit, scope.Year, ct);
    }

    private sealed record PreparedRegistration(
        Document Doc, int CircuitId, int Year, string Number, string NumberKey,
        string TypeKey, string FileType, string CircuitName);

    private PreparedRegistration PrepareRegistration(Document doc, string number, string circuitName)
    {
        if (doc.ExecutionCircuitId is null)
            throw new ArgumentException("لا يمكن تسجيل رقم أساس لملف بلا دائرة تنفيذ");
        var currentYear = CurrentYear();
        var year = DocumentBaseNumberPolicy.ValidateYear(doc.FileYear, currentYear, "سنة القيد");
        if (year is null)
        {
            if (!_options.AllowCircuitlessLegacy)
                throw new ArgumentException("سنة قيد الملف مطلوبة عند تسجيل رقم أساس");
            year = currentYear;
        }
        var numberKey = DocumentBaseNumberPolicy.ValidateNumber(number)
            ?? throw new ArgumentException("رقم الأساس مطلوب");
        var typeKey = DocumentBaseNumberPolicy.NormalizeType(doc.FileType);
        return new PreparedRegistration(
            doc, doc.ExecutionCircuitId.Value, year.Value, numberKey, numberKey,
            typeKey, (doc.FileType ?? string.Empty).Trim(), circuitName);
    }

    /// <summary>يفحص التعارض ويرمي: صلب بلا تجاوز، ولين بلا رمز صالح برسالة تحذير.</summary>
    private async Task EnsureNoConflictAsync(
        Document doc,
        int? excludeRowId,
        int actorUserId,
        string? actorName,
        int circuitId,
        string typeKey,
        int year,
        string numberKey,
        string? warningToken,
        CancellationToken ct,
        string action = "number_registration",
        int? tokenDocumentId = null)
    {
        var conflict = await FindConflictCoreAsync(
            doc, excludeRowId, actorUserId, circuitId, typeKey, year, numberKey, ct, tokenDocumentId);
        if (conflict is null)
            return;
        if (conflict.IsHard)
            throw new DocumentConflictException(conflict.Message) { Code = "number_duplicate" };

        var tokenOwnerId = tokenDocumentId ?? doc.Id;
        var valid = warningToken is not null
            && _tokens.Validate(warningToken, actorUserId, tokenOwnerId, conflict.Fingerprint, conflict.ConflictingRowIds);
        if (!valid)
            throw new NumberReuseWarningException(
                conflict.Message,
                conflict.WarningToken
                ?? _tokens.Issue(actorUserId, tokenOwnerId, conflict.Fingerprint, conflict.ConflictingRowIds));

        await _audit.LogAsync(actorName, action, doc.Id, doc.DocumentType,
            $"أكد استخدام رقم أساس مستخدم في ملف آخر — صفوف التعارض: {string.Join(',', conflict.ConflictingRowIds)}", ct);
    }

    public Task SyncRegistrationAsync(
        Document doc, int? actorUserId, string? actorName, string? warningToken, CancellationToken ct,
        bool isNewDocument = false)
        => InTransactionAsync(async () =>
        {
            await LockDocumentAsync(doc.Id, ct);
            if (doc.ExecutionCircuitId is null)
            {
                if (!string.IsNullOrWhiteSpace(doc.FileNumber) && !_options.AllowCircuitlessLegacy)
                    throw new ArgumentException("لا يمكن تسجيل رقم أساس لملف بلا دائرة تنفيذ");
                return;
            }
            var circuitId = doc.ExecutionCircuitId.Value;
            var circuit = await _circuits.GetByIdAsync(circuitId, ct);
            var circuitName = circuit?.Name;
            if (string.IsNullOrWhiteSpace(circuitName))
                circuitName = string.IsNullOrWhiteSpace(doc.Court) ? "—" : doc.Court.Trim();
            var prepared = string.IsNullOrWhiteSpace(doc.FileNumber)
                ? null
                : PrepareRegistration(doc, doc.FileNumber, circuitName!);
            var year = prepared?.Year ?? CurrentYear();

            // نقرأ الصفوف أولًا لاكتشاف السنوات المتأثرة، ثم نقفل الملف (أُخذ) وكل نطاق
            // (الدائرة، السنة) مرتّبًا، ثم نعيد القراءة داخل الأقفال (G3).
            var initial = await _baseNumbers.ListActiveByDocumentCircuitAsync(doc.Id, circuitId, ct);
            await LockScopesAsync(
                initial.Select(r => (r.ExecutionCircuitId ?? circuitId, r.Year))
                    .Append((circuitId, year)), ct);

            // إعادة قراءة الصفوف داخل القفل (G3) — لا الاعتماد على doc.BaseNumbers المحمّلة قبل القفل.
            var active = await _baseNumbers.ListActiveByDocumentCircuitAsync(doc.Id, circuitId, ct);
            var activeRegistration = active
                .Where(b => b.Source == NumberSources.Registration)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.CreatedAt)
                .ThenByDescending(b => b.Id)
                .FirstOrDefault();

            if (prepared is null)
            {
                if (activeRegistration is not null)
                {
                    Void(activeRegistration, actorUserId, NumberVoidReasons.RegistrationCleared);
                    await _uow.SaveChangesAsync(ct);
                    await _audit.LogAsync(actorName, "clear_registration", doc.Id, doc.DocumentType,
                        DocumentText.AuditWithActor($"ألغى صف قيد رقم الأساس {activeRegistration.BaseNumber} — أُفرغ رقم القيد", doc), ct);
                }
                return;
            }

            if (activeRegistration is not null && activeRegistration.Year != prepared.Year)
            {
                Void(activeRegistration, actorUserId, NumberVoidReasons.RegistrationSuperseded);
                activeRegistration = null;
            }

            var fallback = activeRegistration is null
                ? await _baseNumbers.FindLegacyRegistrationFallbackAsync(
                    doc.Id, prepared.CircuitId, prepared.Year, prepared.NumberKey, ct)
                : null;
            var target = activeRegistration ?? fallback;

            await EnsureNoConflictAsync(
                doc, target?.Id, actorUserId ?? doc.CreatedById, actorName,
                prepared.CircuitId, prepared.TypeKey, prepared.Year, prepared.NumberKey, warningToken, ct,
                "register_number", isNewDocument ? 0 : null);

            if (target is not null)
            {
                target.Source = NumberSources.Registration;
                target.ExecutionCircuitId = prepared.CircuitId;
                target.CircuitName = prepared.CircuitName;
                target.Year = prepared.Year;
                target.FileType = prepared.FileType;
                target.TypeKey = prepared.TypeKey;
                target.BaseNumber = prepared.Number;
                target.NumberKey = prepared.NumberKey;
                target.UpdatedAt = DateTime.UtcNow;
                target.UpdatedById = actorUserId;
                _baseNumbers.Update(target);
            }
            else
            {
                await _baseNumbers.AddAsync(new DocumentBaseNumber
                {
                    DocumentId = doc.Id,
                    ExecutionCircuitId = prepared.CircuitId,
                    CircuitName = prepared.CircuitName,
                    Year = prepared.Year,
                    FileType = prepared.FileType,
                    TypeKey = prepared.TypeKey,
                    BaseNumber = prepared.Number,
                    NumberKey = prepared.NumberKey,
                    Source = NumberSources.Registration,
                    CreatedById = actorUserId ?? doc.CreatedById,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, ct);
            }

            // F10: إلحاق الرقم الجديد بنص البحث (إلحاق لا إعادة بناء).
            doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, prepared.Number);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "register_number", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"{(target is null ? "سجّل" : "حدّث")} رقم أساس {prepared.Year}: {prepared.Number}", doc), ct);
        }, ct);

    public Task SaveRotationsAsync(
        int userId,
        string? actorName,
        IReadOnlyList<BaseNumberEntry> entries,
        string? warningToken,
        CancellationToken ct)
    {
        if (entries is null || entries.Count == 0)
            return Task.CompletedTask;
        if (entries.Any(e => e is null))
            throw new ArgumentException("طلب تدوير غير صالح");

        var unique = entries.GroupBy(e => e.DocumentId).Select(g => g.Last()).ToList();
        if (unique.Count != entries.Count)
            throw new ArgumentException("لا يمكن تكرار نفس الملف في طلب التدوير");
        if (unique.Count > 500)
            throw new ArgumentException("عدد الملفات في طلب التدوير يتجاوز الحد الأقصى (500)");

        return InTransactionAsync(async () =>
        {
            var currentYear = CurrentYear();
            var ids = unique.Select(e => e.DocumentId).Distinct().OrderBy(id => id).ToList();
            // قفل كل ملف قبل قراءة حالته: يمنع سباق الحذف/التعديل مع التدوير.
            foreach (var id in ids)
                await LockDocumentAsync(id, ct);
            var docs = await _documents.GetByIdsAsync(ids, ct);
            var found = docs.ToDictionary(d => d.Id);
            var prepared = new List<(Document Doc, string Number)>();
            var legacy = new List<(Document Doc, string Number)>();
            var keySet = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in unique)
            {
                if (!found.TryGetValue(entry.DocumentId, out var doc))
                    throw new ArgumentException("أحد الملفات غير موجود أو محذوف");
                if (doc.CreatedById != userId)
                    throw new ArgumentException("لا يمكن تعديل رقم أساس لملف لا يملكه المحامي");

                var normalized = DocumentBaseNumberPolicy.NormalizeNumberOrNull(entry.BaseNumber);
                if (normalized is null)
                    continue; // الإدخال الفارغ = لا تغيير (G8)
                DocumentBaseNumberPolicy.ValidateNumber(normalized);

                if (doc.ExecutionCircuitId is null)
                {
                    if (!_options.AllowCircuitlessLegacy)
                        throw new ArgumentException(
                            $"الملف (رقم {doc.Id}) بلا دائرة تنفيذ — لا يمكن تدويره (V-18)");
                    // توافق انتقالي لبناءات الاختبار بلا دائرة.
                    if (doc.IsDraft || doc.NeedsRegistration
                        || doc.FileYear == currentYear.ToString(CultureInfo.InvariantCulture))
                        throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");
                    var legacyEligible = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                        ? doc.ExecutedStatus == ExecutedStatusCatalog.None
                            && doc.BaseNumbers.Any(b => b.VoidedAt is null && b.Year < currentYear)
                        : !ExecutionStatusCatalog.IsExecuted(doc.ExecStatus, doc.ExecSubStatus);
                    if (!legacyEligible)
                        throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");
                    legacy.Add((doc, normalized));
                    continue;
                }

                if (!DocumentBaseNumberPolicy.IsEligible(doc, currentYear))
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");
                var typeKey = DocumentBaseNumberPolicy.NormalizeType(doc.FileType);
                var key = string.Join('|',
                    doc.ExecutionCircuitId.Value.ToString(CultureInfo.InvariantCulture),
                    typeKey, currentYear.ToString(CultureInfo.InvariantCulture), normalized);
                if (!keySet.Add(key))
                    throw new ArgumentException("تكرار الرقم نفسه داخل طلب التدوير لنفس الدائرة والنوع والسنة");
                prepared.Add((doc, normalized));
            }

            if (prepared.Count == 0 && legacy.Count == 0)
                return;

            await LockScopesAsync(
                prepared.Select(p => (p.Doc.ExecutionCircuitId!.Value, currentYear)), ct);

            var softRows = new List<int>();
            foreach (var (doc, number) in prepared)
            {
                var circuitId = doc.ExecutionCircuitId!.Value;
                var existing = await _baseNumbers.FindActiveRowForYearAsync(doc.Id, circuitId, currentYear, ct);
                if (existing is not null)
                    throw new DocumentConflictException(
                        "يوجد رقم مسجل للسنة الحالية؛ استخدم التصحيح") { Code = "number_duplicate" };

                var typeKey = DocumentBaseNumberPolicy.NormalizeType(doc.FileType);
                var numberKey = DocumentBaseNumberPolicy.NormalizeNumber(number);
                var conflict = await FindConflictCoreAsync(
                    doc, null, userId, circuitId, typeKey, currentYear, numberKey, ct);
                if (conflict is null)
                    continue;
                if (conflict.IsHard)
                    throw new DocumentConflictException(conflict.Message) { Code = "number_duplicate" };
                softRows.AddRange(conflict.ConflictingRowIds);
            }

            if (softRows.Count > 0)
            {
                var batchFingerprint = BatchFingerprint(prepared, currentYear);
                var valid = warningToken is not null
                    && _tokens.Validate(warningToken, userId, 0, batchFingerprint, softRows);
                if (!valid)
                {
                    var token = _tokens.Issue(userId, 0, batchFingerprint, softRows);
                    var first = prepared[0];
                    var firstType = first.Doc.FileType;
                    var message =
                        $"تأكد من الرقم {DocumentBaseNumberPolicy.NormalizeNumber(first.Number)}؛ لأنه مستخدم في ملف آخر (سنة {currentYear}، نوع {(string.IsNullOrWhiteSpace(firstType) ? "—" : firstType)}). هل تريد إدخاله على أي حال؟";
                    throw new NumberReuseWarningException(message, token);
                }
            }

            var auditEntries = new List<AuditLogEntry>(prepared.Count + legacy.Count);
            foreach (var (doc, number) in prepared)
            {
                var circuitId = doc.ExecutionCircuitId!.Value;
                var typeKey = DocumentBaseNumberPolicy.NormalizeType(doc.FileType);
                await _baseNumbers.AddAsync(new DocumentBaseNumber
                {
                    DocumentId = doc.Id,
                    ExecutionCircuitId = circuitId,
                    CircuitName = string.IsNullOrWhiteSpace(doc.Court) ? "—" : doc.Court.Trim(),
                    Year = currentYear,
                    FileType = (doc.FileType ?? string.Empty).Trim(),
                    TypeKey = typeKey,
                    BaseNumber = number,
                    NumberKey = DocumentBaseNumberPolicy.NormalizeNumber(number),
                    Source = NumberSources.Rotation,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, ct);
                doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, number);
                doc.Version++;
                auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                    DocumentText.AuditWithActor($"دوّر الملف برقم أساس {currentYear}: {number}", doc)));
            }
            foreach (var (doc, number) in legacy)
            {
                // مسار انتقالي بلا دائرة (لا يُستخدم في الإنتاج): الصف بلا دائرة لا يدخل الفهرس الفريد.
                await _baseNumbers.AddAsync(new DocumentBaseNumber
                {
                    DocumentId = doc.Id,
                    Year = currentYear,
                    FileType = (doc.FileType ?? string.Empty).Trim(),
                    TypeKey = DocumentBaseNumberPolicy.NormalizeType(doc.FileType),
                    BaseNumber = number,
                    NumberKey = DocumentBaseNumberPolicy.NormalizeNumber(number),
                    Source = NumberSources.Rotation,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, ct);
                doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, number);
                doc.Version++;
                auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                    DocumentText.AuditWithActor($"دوّر الملف برقم أساس {currentYear}: {number}", doc)));
            }
            await _uow.SaveChangesAsync(ct);
            await _audit.LogManyAsync(auditEntries, ct);
            if (softRows.Count > 0)
                await _audit.LogAsync(actorName, "number_reuse_override", null, null,
                    $"أكد استخدام أرقام مستخدمة في ملفات أخرى — صفوف التعارض: {string.Join(',', softRows.Distinct())}", ct);
        }, ct);
    }

    private static string BatchFingerprint(IEnumerable<(Document Doc, string Number)> prepared, int year)
        => "batch|" + string.Join(';', prepared
            .OrderBy(p => p.Doc.Id)
            .Select(p => string.Join(':',
                p.Doc.Id.ToString(CultureInfo.InvariantCulture),
                p.Doc.ExecutionCircuitId?.ToString(CultureInfo.InvariantCulture) ?? "",
                DocumentBaseNumberPolicy.NormalizeType(p.Doc.FileType),
                year.ToString(CultureInfo.InvariantCulture),
                DocumentBaseNumberPolicy.NormalizeNumber(p.Number))));

    public Task CorrectAsync(
        int documentId,
        int year,
        int? rowId,
        string number,
        int actorUserId,
        string? actorName,
        string? warningToken,
        CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await LockDocumentAsync(documentId, ct);
            var doc = await _documents.GetByIdAsync(documentId, ct)
                ?? throw new KeyNotFoundException("الملف غير موجود");
            if (doc.CreatedById != actorUserId)
                throw new UnauthorizedAccessException("لا يمكن تصحيح رقم أساس لملف لا يملكه المحامي");
            var currentYear = CurrentYear();
            DocumentBaseNumberPolicy.ValidateYear(
                year.ToString(CultureInfo.InvariantCulture), currentYear, "السنة");

            var row = rowId is not null
                ? await _baseNumbers.FindRowWithDocumentAsync(rowId.Value, ct)
                : doc.ExecutionCircuitId is null
                    ? null
                    : await _baseNumbers.FindActiveRowForYearAsync(documentId, doc.ExecutionCircuitId.Value, year, ct);
            if (row is null || row.DocumentId != documentId || row.Year != year || row.VoidedAt is not null)
                throw new KeyNotFoundException("صف رقم الأساس غير موجود أو ملغى أو لا يخص هذا الملف/السنة");
            if (row.ExecutionCircuitId is null && !_options.AllowCircuitlessLegacy)
                throw new ArgumentException("صف رقم الأساس بلا دائرة — لا يمكن تصحيحه (V-18)");

            var normalized = DocumentBaseNumberPolicy.ValidateNumber(number)
                ?? throw new ArgumentException("رقم الأساس مطلوب");
            var numberKey = DocumentBaseNumberPolicy.NormalizeNumber(normalized);
            var circuitId = row.ExecutionCircuitId ?? 0;
            await LockScopesAsync(new[] { (circuitId, year) }, ct);

            // إعادة قراءة الصف داخل القفل: قد يكون أُلغي/حُذف بين القراءة والقفل.
            row = await _baseNumbers.FindRowWithDocumentAsync(row.Id, ct);
            if (row is null || row.DocumentId != documentId || row.Year != year || row.VoidedAt is not null)
                throw new KeyNotFoundException("صف رقم الأساس غير موجود أو ملغى أو لا يخص هذا الملف/السنة");

            await EnsureNoConflictAsync(
                doc, row.Id, actorUserId, actorName,
                circuitId, row.TypeKey, year, numberKey, warningToken, ct, "correct_number");

            var before = row.BaseNumber;
            row.BaseNumber = normalized;
            row.NumberKey = numberKey;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedById = actorUserId;
            _baseNumbers.Update(row);

            if (row.Source == NumberSources.Registration
                && doc.ExecutionCircuitId == circuitId)
            {
                doc.FileNumber = normalized;
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                doc.UpdatedAt = DateTime.UtcNow;
                doc.Version++;
                _documents.Update(doc);
            }

            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "correct_number", documentId, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"صحّح رقم أساس {year}: قبل «{before}» بعد «{normalized}» (صف {row.Id})", doc), ct);
        }, ct);

    public Task UndoRotationAsync(
        int documentId, int year, int actorUserId, string? actorName, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await LockDocumentAsync(documentId, ct);
            var doc = await _documents.GetByIdAsync(documentId, ct)
                ?? throw new KeyNotFoundException("الملف غير موجود");
            if (doc.CreatedById != actorUserId)
                throw new UnauthorizedAccessException("لا يمكن التراجع عن تدوير ملف لا يملكه المحامي");
            var currentYear = CurrentYear();
            if (year != currentYear)
                throw new ArgumentException("التراجع مسموح لتدوير السنة الحالية فقط");
            if (doc.ExecutionCircuitId is null)
                throw new ArgumentException("الملف بلا دائرة تنفيذ");
            var circuitId = doc.ExecutionCircuitId.Value;
            await LockScopesAsync(new[] { (circuitId, year) }, ct);

            var rows = await _baseNumbers.ListActiveByCircuitYearAsync(circuitId, year, ct);
            var row = rows
                .Where(r => r.DocumentId == documentId && r.Source == NumberSources.Rotation)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .FirstOrDefault();
            if (row is null)
                throw new KeyNotFoundException("لا يوجد صف تدوير فعّال لهذه السنة في دائرة الملف");
            if (rows.Any(r => r.DocumentId == documentId && r.Id != row.Id))
                throw new DocumentConflictException(
                    "لا يمكن التراجع: توجد هوية أخرى فعّالة لنفس السنة في دائرة الملف") { Code = "number_duplicate" };

            Void(row, actorUserId, NumberVoidReasons.UndoRotation);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "undo_rotation", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"تراجع عن تدوير {year}: ألغى الرقم {row.BaseNumber}", doc), ct);
        }, ct);

    public Task ApplyRenewalAsync(
        Document doc,
        string number,
        int year,
        string? fileType,
        int actorUserId,
        string? actorName,
        string? warningToken,
        CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await LockDocumentAsync(doc.Id, ct);
            if (doc.ExecutionCircuitId is null)
            {
                if (!_options.AllowCircuitlessLegacy)
                    throw new ArgumentException("لا يمكن تجديد رقم أساس لملف بلا دائرة تنفيذ (V-18)");
                // توافق انتقالي لبناءات الاختبار اليدوية بلا دائرة (لا يُستخدم في الإنتاج).
                await _baseNumbers.AddAsync(new DocumentBaseNumber
                {
                    DocumentId = doc.Id,
                    Year = year,
                    BaseNumber = DocumentBaseNumberPolicy.NormalizeNumber(number),
                    NumberKey = DocumentBaseNumberPolicy.NormalizeNumber(number),
                    TypeKey = DocumentBaseNumberPolicy.NormalizeType(fileType ?? doc.FileType),
                    FileType = fileType ?? doc.FileType,
                    Source = NumberSources.Renewal,
                    CreatedById = actorUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, ct);
                doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, number);
                await _uow.SaveChangesAsync(ct);
                return;
            }
            var currentYear = CurrentYear();
            DocumentBaseNumberPolicy.ValidateYear(
                year.ToString(CultureInfo.InvariantCulture), currentYear, "سنة الإعادة");
            var normalized = DocumentBaseNumberPolicy.ValidateNumber(number, "رقم الملف الجديد")
                ?? throw new ArgumentException("رقم الملف الجديد مطلوب");
            var numberKey = DocumentBaseNumberPolicy.NormalizeNumber(normalized);
            var circuitId = doc.ExecutionCircuitId.Value;
            await LockScopesAsync(new[] { (circuitId, year) }, ct);

            var active = await _baseNumbers.ListActiveByDocumentCircuitAsync(doc.Id, circuitId, ct);
            var latestYear = active.Count == 0 ? 0 : active.Max(r => r.Year);
            if (year < latestYear)
                throw new ArgumentException(
                    $"سنة التجديد ({year}) أقدم من أحدث هوية غير ملغاة في دائرة الملف ({latestYear})");

            var type = (fileType ?? doc.FileType ?? string.Empty).Trim();
            var typeKey = DocumentBaseNumberPolicy.NormalizeType(type);
            await EnsureNoConflictAsync(
                doc, null, actorUserId, actorName, circuitId, typeKey, year, numberKey, warningToken, ct,
                "renew_number");

            await _baseNumbers.AddAsync(new DocumentBaseNumber
            {
                DocumentId = doc.Id,
                ExecutionCircuitId = circuitId,
                CircuitName = string.IsNullOrWhiteSpace(doc.Court) ? "—" : doc.Court.Trim(),
                Year = year,
                FileType = type,
                TypeKey = typeKey,
                BaseNumber = normalized,
                NumberKey = numberKey,
                Source = NumberSources.Renewal,
                CreatedById = actorUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, ct);
            doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, normalized);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "renew_number", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"سجّل رقم تجديد {year}: {normalized}", doc), ct);
        }, ct);

    public Task VoidForDocumentDeleteAsync(
        Document doc, int actorUserId, string? actorName, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            await LockDocumentAsync(doc.Id, ct);
            var rows = await _baseNumbers.ListByDocumentAsync(
                doc.Id, includeDeletedDocument: true, ct);
            if (rows.Count == 0)
                return;
            await LockScopesAsync(
                rows.Select(r => (r.ExecutionCircuitId ?? 0, r.Year)), ct);

            foreach (var row in rows)
            {
                row.VoidedAt ??= DateTime.UtcNow;
                row.VoidedById = actorUserId;
                row.VoidReason = NumberVoidReasons.DocumentDeleted;
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedById = actorUserId;
            }
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "void_numbers", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"ألغى {rows.Count} صف رقم أساس لحذف الملف (الرقم يبقى معروضًا)", doc), ct);
        }, ct);

    public async Task<IReadOnlyList<BaseNumberHistoryDto>> GetHistoryAsync(
        int documentId, bool includeDeletedDocument, CancellationToken ct)
    {
        var doc = includeDeletedDocument
            ? await _documents.GetDeletedByIdAsync(documentId, ct)
            : await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return Array.Empty<BaseNumberHistoryDto>();
        var rows = await _baseNumbers.ListByDocumentAsync(documentId, includeDeletedDocument, ct);
        return DocumentBaseNumberPolicy.BuildHistory(doc, rows, CurrentYear());
    }

    private static void Void(DocumentBaseNumber row, int? actorUserId, string reason)
    {
        row.VoidedAt = DateTime.UtcNow;
        row.VoidedById = actorUserId;
        row.VoidReason = reason;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedById = actorUserId;
    }
}
