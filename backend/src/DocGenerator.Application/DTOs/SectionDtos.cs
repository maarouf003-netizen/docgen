namespace DocGenerator.Application.DTOs;

/// <summary>شعبة واحدة مع عداداتها (دوائر + اسم الرئيس الشاغل) — للقوائم والإدارة.</summary>
public record SectionDto(
    int Id,
    int BranchId,
    string? BranchName,
    string Name,
    bool IsActive,
    int CircuitCount,
    string? HeadName);

/// <summary>إنشاء شعبة في فرع — مشرف/مدير (قرار §2.15).</summary>
public record CreateSectionRequest(
    int BranchId,
    string? Name);

/// <summary>إعادة تسمية شعبة — مشرف/مدير.</summary>
public record RenameSectionRequest(
    string? Name);

/// <summary>تفعيل/تعطيل شعبة — مشرف/مدير (التعطيل ممنوع مع وجود دوائر).</summary>
public record SetSectionActiveRequest(
    bool IsActive);
