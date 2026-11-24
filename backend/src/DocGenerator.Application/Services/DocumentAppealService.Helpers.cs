using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── أدوات التحقق ──────────────────────────────────────────────────────

    private static void EnsurePendingByCreator(DocumentAppeal appeal, int userId)
        => DocumentAppealRules.EnsurePendingByCreator(appeal, userId);

    internal static void EnsureAssignedFollower(DocumentAppeal appeal, int userId)
    {
        if (appeal.AssignedLawyerId != userId)
            throw new UnauthorizedAccessException("لا يمكنك تعديل هذا الاستئناف — ليس مسندًا إليك للمتابعة");
    }

    // ── أدوات مساعدة ──────────────────────────────────────────────────────

    internal static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>تطبيع نص اختياري مع رفض تجاوز الحد الأقصى برسالة عربية واضحة
    /// (بدل استثناء قاعدة البيانات الخام عند Postgres).</summary>
    internal static string? Bounded(string? value, int maxLength, string fieldName)
    {
        var normalized = Normalize(value);
        if (normalized is not null && normalized.Length > maxLength)
            throw new ArgumentException($"طول {fieldName} يتجاوز الحد الأقصى ({maxLength} حرفًا)");
        return normalized;
    }

    internal static string RequireText(string? value, string fieldName, int maxLength = 2000)
    {
        var normalized = Bounded(value, maxLength, fieldName)
            ?? throw new ArgumentException($"{fieldName} مطلوب");
        return normalized;
    }

    private static string Single(string? name) => string.IsNullOrWhiteSpace(name) ? "—" : name!.Trim();

    private static string TripleOr(string? first, string? second, string? third, string? fallback)
    {
        var parts = new[] { first, second, third }.Where(v => !string.IsNullOrWhiteSpace(v));
        var triple = string.Join(' ', parts);
        return !string.IsNullOrWhiteSpace(triple) ? triple : (string.IsNullOrWhiteSpace(fallback) ? "—" : fallback!.Trim());
    }
}
