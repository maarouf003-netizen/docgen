using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentNumberRegistry : IDocumentNumberRegistry
{
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
}
