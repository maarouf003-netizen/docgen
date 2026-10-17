using DocGenerator.Application.Common;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// صف ملف واحد في جدول تدوير أرقام الأساس — ملفات المحامي المؤهلة للتدوير
/// مع رقم أساس السنة الحالية إن وُجد (وإلا يكون null فيُترك الحقل فارغًا للإدخال).
/// DisplayName هو اسم العرض (اسم المقترض، أو اسم طالب العرض لمن لفائف العائلتين
/// Executed + Deposit اللتين بلا مقترض)، ويعود للواجهة لعرضه عمود الاسم الموحد.
/// </summary>
public record RotationDocumentDto(
    int DocumentId,
    string? Court,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string? FileNumber,
    string? FileType,
    string? BaseNumber,
    string? DisplayName,
    string? EffectiveFileNumber = null,
    string? EffectiveFileYear = null);

/// <summary>
/// رقم أساس مُدخل لملف واحد في سنة التدوير الحالية. القيمة الفارغة تعني
/// إلغاء رقم أساس السنة الحالية لهذا الملف (مع الاحتفاظ بأرقام السنوات السابقة).
/// </summary>
public record BaseNumberEntry(
    int DocumentId,
    string? BaseNumber);

public class SaveBaseNumbersRequest
{
    public List<BaseNumberEntry> Entries { get; set; } = new();
}

/// <summary>
/// سجل رقم أساس لسنة واحدة من تاريخ تدوير أرقام الأساس للملف — تُعرض كل السجلات
/// (سنة واحدة قد تملك أكثر من رقم — الأحدث CreatedAt هو المعتبر)، مرتبة بالسنة ثم CreatedAt تنازليًا.
/// </summary>
public record BaseNumberHistoryDto(
    int Year,
    string BaseNumber);

/// <summary>
/// وقعة واحدة من «وقوعات الملف»: شطب/تجديد (وضع «منفذ عليه») أو إجراء تغيير حالة
/// (نظام «طالبة تنفيذ»: تريث/منفذ بالتسوية/منفذ جبريا/تراجع). EventDate/ReceiptDate
/// زمنيّان (تُفسَّر النصوص الحرة في الخدمة وتحوَّل إلى DateTime)، وDetails تحمل حقول
/// إجراءات تغيير الحالة الكاملة (المفاتيح الإنكليزية المعتمدة في الخدمة)، بينما
/// DetailsText تحمل السرد النصي الحر للوقوعات الآلية (كنوع «تغيير جهة») — أحدهما
/// فقط غير null، فلا يُلوَّث القاموس بالنص الخام.
/// </summary>
public record DocumentOccurrenceDto(
    int Id,
    string OccurrenceType,
    string OccurrenceTypeLabel,
    DateTime? EventDate,
    string? FileNumber,
    string? FileType,
    int? Year,
    string? ReceiptNumber,
    DateTime? ReceiptDate,
    IReadOnlyDictionary<string, string>? Details,
    string? CreatedByName,
    string? Source = null,
    string? DetailsText = null,
    string? FromCircuitName = null,
    string? ToCircuitName = null);
