namespace DocGenerator.Application.DTOs;

/// <summary>دائرة تنفيذ واحدة مع عداداتها (ملفات + معلقات) ومالكها (قسم/شعبة).</summary>
public record ExecutionCircuitDto(
    int Id,
    int BranchId,
    string? BranchName,
    string Name,
    bool IsActive,
    int FileCount,
    int PendingCount,
    long Version,
    int? SectionId = null,
    string? SectionName = null);

/// <summary>إدخال/تسمية دائرة (الاسم فقط — الفرع من سياق رئيس القسم).</summary>
public class UpsertExecutionCircuitRequest
{
    public string? Name { get; set; }
    public long? Version { get; set; }
}

/// <summary>تعطيل/تفعيل دائرة.</summary>
public class SetCircuitActiveRequest
{
    public bool IsActive { get; set; }
    public long? Version { get; set; }
}

/// <summary>إحالة دفعة ملفات من دائرة إلى محامٍ (نفس الفرع، ذري).</summary>
public class ReferCircuitFilesRequest
{
    public List<int> FileIds { get; set; } = new();
    public int TargetLawyerId { get; set; }
    public int TargetCircuitId { get; set; }
}

/// <summary>نتيجة إحالة دفعة (منقول + متخطى).</summary>
public record ReferCircuitFilesResult(int ReferredCount, int SkippedCount, int RemainingCount);

/// <summary>نقل ملكية دائرة لمالك جديد داخل الفرع نفسه — `null` تعني قسم الفرع. `Version` للتفاؤلية (اختياري).</summary>
public record TransferCircuitRequest(int? TargetSectionId, long? Version = null);

/// <summary>صف ملف بانتظار إعادة القيد (المعروض حاليًا قديم من EffectiveFileIdentity).</summary>
public record PendingRegistrationDto(
    int DocumentId,
    int CircuitId,
    string? CircuitName,
    string? BorrowerName,
    string? OldFileNumber,
    string? OldFileType,
    string? OldFileYear);

/// <summary>إدخال رقم جديد واحد في complete-registrations.</summary>
public class CompleteRegistrationEntry
{
    public int DocumentId { get; set; }
    public string? FileNumber { get; set; }
    public string? FileType { get; set; }
    public string? FileYear { get; set; }
}

/// <summary>حفظ ذري لإعادة القيد (ملك المحامي فقط + وحدانية).</summary>
public class CompleteRegistrationsRequest
{
    public List<CompleteRegistrationEntry> Entries { get; set; } = new();
}

/// <summary>صف إحصائية دائرة (الدائرة × ملفاتها × محامون نشطون × معلقات) — كل ملف
/// يُحتسب مرة واحدة في دائرته (قرار §2.20)، وعمود الشعبة لصف القسم/الشعب (§12).</summary>
public record CircuitStatsDto(
    int CircuitId,
    string CircuitName,
    int BranchId,
    string? BranchName,
    bool IsActive,
    int FileCount,
    int LawyerCount,
    int PendingCount,
    int? SectionId = null,
    string? SectionName = null,
    /// <summary>رمز التزامن التفاؤلي — يُرسَل في نقل الدائرة لكشف السباق برسالة ودية.</summary>
    long Version = 0);
