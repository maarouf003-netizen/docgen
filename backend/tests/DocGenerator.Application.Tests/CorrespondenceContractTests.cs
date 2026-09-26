using System.Reflection;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس بنية عقود المراسلة بقوائم إدراج كاملة لا منع أسماء:
/// أي حقل جديد أو محذوف أو مُعاد تسميته يُفشل الحارس عمدًا — فالعقد سلكي
/// مشترك مع الواجهة وبيان `correspondence-contracts.json`.
/// القواعد المثبتة: حالة الاطلاع مقياس المستلم وحده (لا «هل شاهدتها أنا»)،
/// وحقّا التوثيق والرد قرارا خادم على العقدين، وتوثيق الأسماء في التفاصيل
/// وحدها (القائمة بلا أي عضو توثيق).
/// </summary>
public class CorrespondenceContractTests
{
    private static IReadOnlyList<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static IReadOnlyList<string> Names(Type type) =>
        Properties(type).Select(p => p.Name).ToList();

    // قائمة الإدراج الكاملة لعقد القائمة: أي حقل جديد أو محذوف أو مُعاد تسميته
    // يُفشل هذا الاختبار عمدًا — فالعقد سلكي مشترك مع الواجهة (`types/index.ts`)
    // وبيان `contracts/correspondence-contracts.json`، لا يُغيَّر بصمت.
    private static readonly string[] ListMembers =
    [
        "Id", "CorrespondenceNumber", "CorrespondenceDate", "Importance",
        "DocumentId", "FileContext", "CreatorName", "TargetName",
        "Snippet", "LastKind", "ViewStatus", "CanMarkSeen", "CanReply",
        "MessagesCount", "AdministrativeBranchName", "Governorate", "UpdatedAt",
    ];

    // قائمة الإدراج الكاملة لعقد التفاصيل — القاعدة نفسها.
    private static readonly string[] DetailMembers =
    [
        "Id", "CorrespondenceNumber", "CorrespondenceDate", "Importance",
        "DocumentId", "FileContext", "BranchId", "Governorate",
        "AdministrativeBranchName", "CreatorId", "CreatorName", "CreatorRole",
        "TargetUserId", "TargetName", "TargetRole", "ViewStatus",
        "CanMarkSeen", "CanReply", "Messages", "Receipts", "CreatedAt",
    ];

    [Fact]
    public void ListContract_ExposesExactlyTheDeclaredMembers()
        // بلا ترتيب: الترتيب السلكي يُثبَّت في بيان العقد المشترك، وهنا العضوية فقط.
        => Assert.Equal(
            ListMembers.OrderBy(n => n),
            Names(typeof(CorrespondenceListItemDto)).OrderBy(n => n));

    [Fact]
    public void DetailContract_ExposesExactlyTheDeclaredMembers()
        => Assert.Equal(
            DetailMembers.OrderBy(n => n),
            Names(typeof(CorrespondenceDto)).OrderBy(n => n));

    [Fact]
    public void ListContract_CarriesNoReceiptCollection()
    {
        // القائمة: لا مجموعة توثيق ولا عدّاده — الشهود بأسمائهم في التفاصيل وحدها.
        var collections = Properties(typeof(CorrespondenceListItemDto))
            .Where(p => p.PropertyType != typeof(string)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(collections);
    }

    [Fact]
    public void DetailContract_CarriesTargetReceipts_ListCarriesNoReceiptMembers()
    {
        // التفاصيل: توثيق المستلم بأسمائه؛ القائمة: لا مجموعة ولا عدّاد توثيق —
        // الحالة تُقرأ من `ViewStatus` وحقّ الفعل من `CanMarkSeen`.
        Assert.Contains("Receipts", Names(typeof(CorrespondenceDto)));
        Assert.DoesNotContain(Names(typeof(CorrespondenceListItemDto)), n => n.StartsWith("Receipt"));
    }

    [Theory]
    [InlineData(typeof(CorrespondenceListItemDto))]
    [InlineData(typeof(CorrespondenceDto))]
    public void MarkSeenRight_IsServerAuthoritative_BoolOnBothContracts(Type contract)
    {
        // بغير هذا الحقل تُشتقّ الواجهة الصلاحية من مقارنة المعرّفات وتاخذ
        // «يمكنني الرد» مكان «يمكنني التوثيق».
        var right = Properties(contract).SingleOrDefault(p => p.Name == "CanMarkSeen");

        Assert.NotNull(right);
        Assert.Equal(typeof(bool), right!.PropertyType);
    }

    [Theory]
    [InlineData(typeof(CorrespondenceListItemDto))]
    [InlineData(typeof(CorrespondenceDto))]
    public void ReplyRight_IsServerAuthoritative_BoolOnBothContracts(Type contract)
    {
        // حق الرد مستقل عن حق التوثيق: كلاهما قرار خادم على العقدين معًا،
        // فلا تُشتقّ الواجهة أحدهما من الآخر ولا من مقارنة المعرّفات.
        var right = Properties(contract).SingleOrDefault(p => p.Name == "CanReply");

        Assert.NotNull(right);
        Assert.Equal(typeof(bool), right!.PropertyType);
    }

    [Theory]
    [InlineData(typeof(CorrespondenceListItemDto))]
    [InlineData(typeof(CorrespondenceDto))]
    public void BothContracts_ExposeTheSharedViewStatus(Type contract)
    {
        // مقياس واحد للطرفين: حالة اطلاع المستلم، لا «هل شاهدها أنا».
        Assert.Contains("ViewStatus", Names(contract));
    }
}
