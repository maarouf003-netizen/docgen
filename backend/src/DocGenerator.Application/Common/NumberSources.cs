namespace DocGenerator.Application.Common;

/// <summary>مصادر صف رقم الأساس ووسوم الإلغاء — ثوابت واحدة تمنع انحراف النصوص.</summary>
public static class NumberSources
{
    public const string Registration = "registration";
    public const string Rotation = "rotation";
    public const string Renewal = "renewal";
}

/// <summary>أسباب الإلغاء المخزنة في <c>DocumentBaseNumbers.VoidReason</c>.</summary>
public static class NumberVoidReasons
{
    public const string DocumentDeleted = "document-deleted";
    public const string UndoRotation = "undo-rotation";
    public const string RegistrationSuperseded = "registration-superseded";
    public const string RegistrationCleared = "registration-cleared";
}
