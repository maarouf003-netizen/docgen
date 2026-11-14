namespace DocGenerator.Api.Filters;

/// <summary>
/// استثناء صريح من إزالة التكرار العامة (`P0-04`) لإجراءٍ يملك آلية أضيق
/// بالفعل (حجوزات الخدمة) أو لا يمثل نية كتابة (دخول/خروج/تليمترية).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class SkipIdempotencyAttribute : Attribute
{
}
