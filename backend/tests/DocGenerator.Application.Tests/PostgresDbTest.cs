using System.Collections.Concurrent;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قاعدة اختبارات القاعدة المشتركة في <c>Application.Tests</c> على <c>PostgreSQL</c>
/// (‏`ADR-0009`): قاعدة معزولة **لكل صنف اختبار** تُنشأ من القالب المشترك مرة واحدة،
/// ويُفرَّغ كل ما فيها قبل كل حالة (<see cref="PostgresTestDb.Reset"/>، ~15ms دافئًا)
/// بدل استنساخ قاعدة كاملة (~2s) لكل اختبار. الحالات في الصنف الواحد تُنفَّذ
/// تسلسليًا في `xUnit`، فلا تتزاحم على قاعدة الصنف.
///
/// <para>التصفير في باني القاعدة **قبل** باني الصنف المشتق: يبقى بذر الحالة في باني
/// الصنف كما هو، وتوابع القاعدة (خدمات، معرّفات…) تُهيَّأ مباشرة بعده. الدوال كلها
/// متزامنة عن قصد: لا <c>async</c> في مسار البناء كي لا تُحجب خيوط المجمّع.</para>
///
/// <para>اسم الحقل <c>_db</c> محفوظ في القاعدة كي لا تُشوَّش آلاف المراجع المنقولة،
/// و<c>NewContext()</c> لسياق إضافي على القاعدة نفسها.</para>
/// </summary>
public abstract class PostgresDbTest : IDisposable
{
    private static readonly ConcurrentDictionary<Type, PostgresTestDb> Fixtures = new();

    /// <summary>قاعدة الصنف المعزولة (تشاركها كل حالات الصنف وتُصفَّر قبل كل حالة).</summary>
    protected readonly PostgresTestDb Pg;

    /// <summary>سياق الحالة بعد التصفير (ملكية القاعدة للصنف لا للاختبار).</summary>
    protected readonly DocGeneratorPostgresDbContext _db;

    protected PostgresDbTest()
    {
        Pg = Fixtures.GetOrAdd(GetType(), _ => PostgresTestDb.Create());
        Pg.Reset();
        _db = Pg.CreateContext();
    }

    public virtual void Dispose() => _db.Dispose();

    /// <summary>سياق إضافي على القاعدة نفسها (ملكية المستدعي: <c>using</c>/<c>await using</c>).</summary>
    protected DocGeneratorPostgresDbContext NewContext() => Pg.CreateContext();
}
