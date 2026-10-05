namespace DocGenerator.Application.DTOs;

/// <summary>دائرة تنفيذ واحدة مع عداداتها (ملفات + معلقات).</summary>
public record ExecutionCircuitDto(
    int Id,
    int BranchId,
    string? BranchName,
    string Name,
    bool IsActive,
    int FileCount,
    int PendingCount,
    long Version);

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

/// <summary>صف إحصائية دائرة (الدائرة × ملفاتها × محامون نشطون × معلقات).</summary>
public record CircuitStatsDto(
    int CircuitId,
    string CircuitName,
    int BranchId,
    string? BranchName,
    bool IsActive,
    int FileCount,
    int LawyerCount,
    int PendingCount);
