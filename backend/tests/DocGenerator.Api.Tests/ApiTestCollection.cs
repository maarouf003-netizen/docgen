namespace DocGenerator.Api.Tests;

/// <summary>
/// مجموعة اختبارات تكاملية تشترك في مضيف <see cref="ApiFactory"/> واحد (قاعدة
/// `PostgreSQL` فريدة وبداية واحدة): إقلاع التطبيق الحقيقي وترحيلات EF والبذر تُنفَّذ
/// مرة واحدة بدل 16 مرة، لأنها أثقل عمل في الجلسة. الاختبارات داخل مجموعة واحدة
/// تُنفَّذ تسلسليًا فتبقى معزولة البيانات بلا تصادم (أسماء المستخدمين المنشأة
/// GUID-عشوائية، وانفراد أسماء الواردات الحرفية بفئاتها)، ومن يحتاج حالة نظيفة
/// تمامًا يستدعي <see cref="ApiFactory.ResetAsync"/> في <c>InitializeAsync</c>.
/// كل فئة تنضم إليها تُعلَّق بـ <c>[Collection(ApiTestCollection.Name)]</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api-integration";
}
