using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentNumberRegistry : IDocumentNumberRegistry
{
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
                    "لا يمكن التراجع: توجد هوية أخرى فعّالة لنفس السنة في دائرة الملف")
                { Code = "number_duplicate" };

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
}
