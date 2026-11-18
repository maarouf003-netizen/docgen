namespace DocGenerator.Api.Tests;

/// <summary>
/// مجموعة تكامل مشتركة على <c>PostgreSQL</c> حقيقي: مصنع واحد وقاعدة فريدة لكل
/// مجموعة، والاختبارات داخلها تُنفَّذ تسلسليًا وتعيد الضبط بـ<c>Respawn</c> قبل كل
/// اختبار — فتبقى معزولة بلا كلفة إنشاء قاعدة/ترحيل لكل اختبار.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresApiTestCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "api-integration-postgres";
}
