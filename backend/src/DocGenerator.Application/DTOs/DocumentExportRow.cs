using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.DTOs;

/// <summary>طالب التنفيذ (اسمه الثلاثي فقط — ما تقرأه الورقة).</summary>
public interface IExportApplicant
{
    string? Name { get; }
    string? Father { get; }
    string? Family { get; }
}

/// <summary>الجهة المنفذ عليها (اسمها فقط).</summary>
public interface IExportPublicEntity
{
    string? EntityName { get; }
}

/// <summary>الشخص الطبيعي المنفذ عليه (اسمه الثلاثي فقط).</summary>
public interface IExportNaturalPerson
{
    string? Name { get; }
    string? Father { get; }
    string? Family { get; }
}

/// <summary>الإجراء التنفيذي (نصه فقط — عمود «الإجراءات والملاحظات» يعرض الأول).</summary>
public interface IExportAction
{
    string? Text { get; }
}

/// <summary>
/// الحقول الدنيا التي تقرأها مصنّفات Excel (المستندات والبوابة) — تُحققها
/// `DocumentResponse` (الكاملة) و`DocumentExportRow` (المسقطة) معًا، فتبقى
/// مساعدات `ExcelExportService` (`StatusText`/`ApplicantText`/`FullName`/`FileNumberText`)
/// واحدة دون تكرار ودون إجبار مسار التصدير على تحميل الشجرة الكاملة.
/// </summary>
public interface IDocumentSheetFields : IDocumentExecutionState
{
    string? AdministrativeBranchName { get; }
    string? BranchName { get; }
    string? Court { get; }
    string? SectionName { get; }
    string? FileNumber { get; }
    string? DisplayFileNumber { get; }
    string? FileType { get; }
    string? FileYear { get; }
    string? DisplayFileYear { get; }
    string? AnnexNumber { get; }
    string? Lawyer { get; }
    int ViewCount { get; }
    string? Applicant { get; }
    string? BorrowerName { get; }
    string? BorrowerFather { get; }
    string? BorrowerFamily { get; }
    IReadOnlyList<IExportApplicant> ExecutionApplicants { get; }
    IReadOnlyList<IExportPublicEntity> ExecutedPublicEntities { get; }
    IReadOnlyList<IExportNaturalPerson> ExecutedNaturalPersons { get; }
    IReadOnlyList<IExportAction> ExecutionActions { get; }
}

/// <summary>
/// صف تصدير المستندات بالإسقاط العمودي: الأعمدة اللازمة للورقة فقط (بلا `Include`
/// للشجرة الكاملة) — يخفض ذاكرة التصدير الكبير من مئات الميغابايت إلى بضعة
/// ميغابايتات. القوائم الصغيرة (0-3 عناصر) تُسقط كاملة مرتبة بـ`Id` ويُحسم
/// «الأول» منها في الذاكرة بنفس منطق المساعدات؛ `BaseNumbers` بذور (سنة/زمن/رقم)
/// ينتخب منها `EffectiveFileIdentity.LatestFrom` الرقم الفعّال. الكيانات المسقطة
/// غير متتبعة ولا تُحفظ أبدًا.
/// </summary>
public sealed class DocumentExportRow : IDocumentSheetFields
{
    public string? AdministrativeBranchName { get; set; }
    public string? BranchName { get; set; }
    public string? Court { get; set; }
    public string? SectionName { get; set; }
    public string? FileNumber { get; set; }
    public string? DisplayFileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
    public string? DisplayFileYear { get; set; }
    public string? AnnexNumber { get; set; }
    public string? Lawyer { get; set; }
    public int ViewCount { get; set; }
    public string? GeneralEntitySide { get; set; }
    public string? ExecutedStatus { get; set; }
    public string? ExecStatus { get; set; }
    public string? ExecSubStatus { get; set; }
    public bool IsDraft { get; set; }
    public string? Applicant { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerFather { get; set; }
    public string? BorrowerFamily { get; set; }
    public List<ExportApplicant> ExecutionApplicants { get; set; } = new();
    public List<ExportPublicEntity> ExecutedPublicEntities { get; set; } = new();
    public List<ExportNaturalPerson> ExecutedNaturalPersons { get; set; } = new();
    public List<ExportAction> ExecutionActions { get; set; } = new();
    public List<DocumentBaseNumber> BaseNumbers { get; set; } = new();
    // تحقيق صريح للواجهة: `List<T>` لا يطابق `IReadOnlyList<I>` في توقيع التنفيذ
    // (CS0738) رغم التحويل الضمني، فيُصرَّح به هنا سطرًا بسطر.
    IReadOnlyList<IExportApplicant> IDocumentSheetFields.ExecutionApplicants => ExecutionApplicants;
    IReadOnlyList<IExportPublicEntity> IDocumentSheetFields.ExecutedPublicEntities => ExecutedPublicEntities;
    IReadOnlyList<IExportNaturalPerson> IDocumentSheetFields.ExecutedNaturalPersons => ExecutedNaturalPersons;
    IReadOnlyList<IExportAction> IDocumentSheetFields.ExecutionActions => ExecutionActions;
}

/// <summary>طالب تنفيذ مسقط (للورقة فقط).</summary>
public sealed class ExportApplicant : IExportApplicant
{
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }
}

/// <summary>جهة منفذ عليها مسقطة (للورقة فقط).</summary>
public sealed class ExportPublicEntity : IExportPublicEntity
{
    public string? EntityName { get; set; }
}

/// <summary>شخص طبيعي منفذ عليه مسقط (للورقة فقط).</summary>
public sealed class ExportNaturalPerson : IExportNaturalPerson
{
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }
}

/// <summary>إجراء تنفيذي مسقط (للورقة فقط — الأول فقط).</summary>
public sealed class ExportAction : IExportAction
{
    public string? Text { get; set; }
}
