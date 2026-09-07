using DocGenerator.Application.Services;

namespace DocGenerator.Application.Tests;

public class EntityChangeMessagesTests
{
    // ── ترويسة المرجع (DecreeSuffix) ──

    [Fact]
    public void DecreeSuffix_AllEmpty_ReturnsEmpty()
    {
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("", "", null));
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("", "  ", null));
        Assert.Equal("", EntityChangeMessages.DecreeSuffix(" ", "", null));
    }

    [Fact]
    public void DecreeSuffix_PartialDecree_ReturnsEmpty()
    {
        // نوع بلا رقم أو رقم بلا نوع — رابط ناقص يمنع بناؤه نهائيًا
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("قرار", "", null));
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("", "99", null));
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("قرار", "  ", null));
        // تاريخ وحده بلا نوع/رقم (توحيد بتاريخ مرسوم فقط عبر API) — لا رابط من تاريخ وحده
        var date = new DateTime(2026, 3, 15);
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("", "", date));
        Assert.Equal("", EntityChangeMessages.DecreeSuffix("  ", " ", date));
    }

    [Fact]
    public void DecreeSuffix_WithDecree_FormatsKindNumberDate()
    {
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var suffix = EntityChangeMessages.DecreeSuffix("قرار وزاري", "99", date);
        Assert.Equal("قرار وزاري رقم 99 بتاريخ 2026-03-15", suffix);
    }

    [Fact]
    public void DecreeSuffix_WithoutDate_FormatsKindAndNumber()
    {
        Assert.Equal("مرسوم رقم 7", EntityChangeMessages.DecreeSuffix("مرسوم", "7", null));
    }

    // ── توحيد التسمية (بلا مرسوم بعد حذف حقوله من النافذة) ──

    [Fact]
    public void UnifyOccurrence_WithoutDecree_OmitsBrokenSuffix()
    {
        var text = EntityChangeMessages.UnifyOccurrence("هيئة الاستثمار، هيئة الاستثمار والتجارة", "هيئة الاستثمار", "", "", null);
        Assert.DoesNotContain("بموجب", text);
        Assert.DoesNotContain("رقم", text);
        Assert.Equal("تم توحيد تسمية \"هيئة الاستثمار، هيئة الاستثمار والتجارة\" إلى «هيئة الاستثمار»", text);
    }

    [Fact]
    public void UnifyOccurrence_WithoutDecree_TrimsWhitespaceFields()
    {
        var text = EntityChangeMessages.UnifyOccurrence("الجهة أ", "الجهة الموحدة", "  ", "", null);
        Assert.DoesNotContain("بموجب", text);
        Assert.DoesNotContain("رقم", text);
    }

    [Fact]
    public void UnifyOccurrence_WithDecree_AppendsSuffix()
    {
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var text = EntityChangeMessages.UnifyOccurrence("الجهة أ", "الجهة الموحدة", "قرار وزاري", "99", date);
        Assert.Equal("تم توحيد تسمية \"الجهة أ\" إلى «الجهة الموحدة» بموجب قرار وزاري رقم 99 بتاريخ 2026-03-15", text);
    }

    // ── بقية الأنماط بلا ارتداد: المرسوم الملزم يبقى كما هو ──

    [Fact]
    public void RenameOccurrence_WithRequiredDecree_KeepsSuffix()
    {
        var text = EntityChangeMessages.RenameOccurrence("وزارة التعليم", "وزارة التربية", "قرار", "12", null);
        Assert.Equal("تم تعديل اسم الجهة من «وزارة التعليم» إلى «وزارة التربية» بموجب قرار رقم 12", text);
    }

    [Fact]
    public void RenameOccurrence_WithoutDecree_OmitsSuffix()
    {
        var text = EntityChangeMessages.RenameOccurrence("وزارة التعليم", "وزارة التربية", "", "", null);
        Assert.Equal("تم تعديل اسم الجهة من «وزارة التعليم» إلى «وزارة التربية»", text);
    }

    [Fact]
    public void MergeOccurrence_WithoutDecree_OmitsSuffix()
    {
        var text = EntityChangeMessages.MergeOccurrence("الجهة أ، الجهة ب", "الجهة الناجية", "", "", null);
        Assert.Equal("تم دمج \"الجهة أ، الجهة ب\" مع \"الجهة الناجية\"", text);
    }

    [Fact]
    public void AbolishOccurrence_WithoutDecree_OmitsSuffix()
    {
        var text = EntityChangeMessages.AbolishOccurrence("الجهة الجديدة", "الجهة الملغاة", "", "", null);
        Assert.Equal("حلّت الجهة «الجهة الجديدة» محل «الجهة الملغاة»", text);
    }
}