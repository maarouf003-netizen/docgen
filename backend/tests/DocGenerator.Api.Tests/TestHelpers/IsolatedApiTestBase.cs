namespace DocGenerator.Api.Tests.TestHelpers;

/// <summary>
/// قاعدة الفئات المعزولة (R1-3): مصنع `ApiFactory` خاص لكل نسخة اختبار
/// (قاعدة SQLite مؤقتة معزولة لكل صنف) مع دورة حياة `IAsyncLifetime` —
/// ترثها الفئات ذات التهيئة الفارغة مباشرةً، وتُبقي `Correspondence`
/// و`MissingRole` تهيئتهما عبر `override`. مصنع لكل نسخة = لا مشاركة
/// حالة عبر الوراثة. مستثناة عمدًا: التجميعة المخصصة
/// `execution-circuits-integration` ومصانع `RF016`/`SecurityHeaders`/
/// `Swagger`/`DatabaseInitializer` — عزلها مقصود.
/// </summary>
public abstract class IsolatedApiTestBase : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = new();

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}
