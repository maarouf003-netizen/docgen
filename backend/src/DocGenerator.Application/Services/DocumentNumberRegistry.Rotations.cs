using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentNumberRegistry : IDocumentNumberRegistry
{
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
                        "يوجد رقم مسجل للسنة الحالية؛ استخدم التصحيح")
                    { Code = "number_duplicate" };

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
}
