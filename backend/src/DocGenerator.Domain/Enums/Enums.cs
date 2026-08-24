namespace DocGenerator.Domain.Enums;

public enum UserRole
{
    Lawyer = 1,
    Head = 2,
    Manager = 3,
    Admin = 4,

    /// <summary>
    /// مندوب الجهة العامة (بوابة قراءة فقط + تصدير Excel) — يُربط بهوية جهة
    /// (Group) أو قيد (Entry) ويُمنع بنيويًا من كل مسارات الكتابة.
    /// </summary>
    EntityManager = 5
}

public enum ContractTypeSelector
{
    Bank = 1,
    Regular = 2
}

public enum ExecutionStatus
{
    None = 0,
    ExecutedForcibly = 1,
    ExecutedBySettlement = 2,
    Deferred = 3,
    DelegationExecuted = 4
}

/// <summary>
/// نطاق استهداف تنبيه رئيس القسم:
/// Document = مرتبط بملف معين (يصل للمحامي المختص)،
/// Lawyer = رسالة خاصة لمحامٍ معين، Branch = تعميم لكل محامي الفرع.
/// </summary>
public enum HeadAlertTargetType
{
    Document = 1,
    Lawyer = 2,
    Branch = 3,
    Head = 4
}
