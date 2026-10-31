using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات مساعد عنوان الملف الموحّد (ورقة إصلاح PR-4 §6.1ب): الترتيب، وتجاهل
/// الأجزاء الفارغة، والتدرّج إلى نوع الملف ثم رقم المستند، والفصل الصريح بين
/// «مستند محذوف» (مرجع بلا صف مرئي) و«مستند قائم بلا حقول عرض» (يُعرض برقمه)،
/// وحالة التنبيه بلا مرجع ملف (عنوان فارغ).
/// </summary>
public class HeadAlertTitleTests
{
    [Fact]
    public void Build_JoinsNamePartsInOrder()
    {
        var title = HeadAlertTitle.Build(
            documentId: 12,
            documentExists: true,
            borrowerName: "محمد",
            borrowerFather: "أحمد",
            borrowerFamily: "العلي",
            documentType: "عقد");

        Assert.Equal("محمد أحمد العلي", title);
    }

    [Fact]
    public void Build_SkipsBlankNameParts()
    {
        var title = HeadAlertTitle.Build(
            documentId: 12,
            documentExists: true,
            borrowerName: "محمد",
            borrowerFather: "   ",
            borrowerFamily: "العلي",
            documentType: "عقد");

        Assert.Equal("محمد العلي", title);
    }

    [Fact]
    public void Build_FallsBackToDocumentType_WhenNoNameParts()
    {
        var title = HeadAlertTitle.Build(
            documentId: 12,
            documentExists: true,
            borrowerName: null,
            borrowerFather: " ",
            borrowerFamily: null,
            documentType: "وكالة");

        Assert.Equal("وكالة", title);
    }

    [Fact]
    public void Build_ReturnsDeletedMarker_WhenDocumentReferenceHasNoVisibleRow()
    {
        var title = HeadAlertTitle.Build(
            documentId: 42,
            documentExists: false,
            borrowerName: null,
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);

        Assert.Equal("مستند محذوف", title);
    }

    [Fact]
    public void Build_ExistingBlankDocument_UsesIdFallback_NotDeletedMarker()
    {
        // صف قديم/مستورد: الملف قائم لكن DocumentType وكل حقول الاسم فارغة.
        var title = HeadAlertTitle.Build(
            documentId: 42,
            documentExists: true,
            borrowerName: null,
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);

        Assert.Equal("مستند 42", title);
        Assert.NotEqual("مستند محذوف", title);
    }

    [Fact]
    public void Build_WithoutDocumentReference_ReturnsEmpty_EvenIfNameFieldsPresent()
    {
        var title = HeadAlertTitle.Build(
            documentId: null,
            documentExists: false,
            borrowerName: "محمد",
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);

        Assert.Equal(string.Empty, title);
    }

    [Fact]
    public void Build_WhitespaceOnlyFieldsOnExistingDocument_UsesIdFallback()
    {
        var title = HeadAlertTitle.Build(
            documentId: 42,
            documentExists: true,
            borrowerName: " ",
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);

        Assert.Equal("مستند 42", title);
    }
}
