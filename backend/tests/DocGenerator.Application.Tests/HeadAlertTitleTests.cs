using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات مساعد عنوان الملف الموحّد (ورقة إصلاح PR-4 §6.1ب): الترتيب، وتجاهل
/// الأجزاء الفارغة، والتدرّج إلى نوع الملف ثم رقم المستند، وحالتا «مستند محذوف»
/// و«لا ملف» — وهي نفس القواعد التي كانت مكررة بين المستودع والخدمة.
/// </summary>
public class HeadAlertTitleTests
{
    [Fact]
    public void Build_JoinsNamePartsInOrder()
    {
        var title = HeadAlertTitle.Build(
            documentId: 12,
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
            borrowerName: null,
            borrowerFather: " ",
            borrowerFamily: null,
            documentType: "وكالة");

        Assert.Equal("وكالة", title);
    }

    [Fact]
    public void Build_ReturnsDeletedDocumentMarker_WhenFieldsMissingButIdPresent()
    {
        var title = HeadAlertTitle.Build(
            documentId: 42,
            borrowerName: null,
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);

        Assert.Equal("مستند محذوف", title);
    }

    [Fact]
    public void Build_WithoutUsableFields_UsesDocumentFallbackOrEmpty()
    {
        var withId = HeadAlertTitle.Build(
            documentId: 42,
            borrowerName: " ",
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);
        var withoutDocument = HeadAlertTitle.Build(
            documentId: null,
            borrowerName: null,
            borrowerFather: null,
            borrowerFamily: null,
            documentType: null);

        Assert.Equal("مستند 42", withId);
        Assert.Equal(string.Empty, withoutDocument);
    }
}
