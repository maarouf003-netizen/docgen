using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس صيغة اسم فرع الجهة العامة: <c>PublicEntityBranchCatalog.Label</c> هو
/// المصدر المرجعي في الخلفية (عمود «فرع الجهة» في تصدير البوابة)، ويقابله
/// تنفيذ واحد في الواجهة (<c>publicEntityBranchLabel</c>) يفوّض إليه كلٌّ من
/// <c>PortalFileCard.branchShort</c> و<c>PortalBranchSelect.formatEntryShort</c>.
/// هذا الاختبار يثبّت دلالات المصدر (بما فيها الحالات الحدّية) فلا «تُصلح»
/// دلالته دون أن يكسر التطابق مع الواجهة بدل أن تتمايز الصيغة بصمت.
/// </summary>
public class PublicEntityBranchCatalogTests
{
    [Theory]
    [InlineData("دمشق", "فرع 1", "دمشق/فرع 1")]
    [InlineData("دمشق", "حلب", "دمشق/حلب")]
    // الجهة الأم بلا اسم فرعي: لا لاحقة زائدة، الاسم كله هو المرجع.
    [InlineData("دمشق", "الجهة الأم", "دمشق")]
    [InlineData("دمشق", "", "دمشق")]
    [InlineData("دمشق", null, "دمشق")]
    [InlineData("دمشق", "   ", "دمشق")]
    // الفرع بلا محافظة: يُعرض وحده (لا «/فرع» ولا «/»).
    [InlineData("", "فرع 1", "فرع 1")]
    [InlineData(null, "فرع 1", "فرع 1")]
    [InlineData("", "", "")]
    [InlineData(null, null, "")]
    // التقليم: أسماء فيها مسافات طرفية تُعرض نظيفة (وإلا اختلفت عن مرايا الواجهة).
    [InlineData(" دمشق ", " فرع 1 ", "دمشق/فرع 1")]
    [InlineData("  ", "فرع 1", "فرع 1")]
    // «الجهة الأم» بعد التقليم تُعامل كفرع أم (بلا لاحقة) كحالة الاسم المجرّد.
    [InlineData("دمشق", " الجهة الأم ", "دمشق")]
    public void Label_FormatsGovernorateAndBranch(
        string? governorate, string? branchName, string expected)
        => Assert.Equal(expected, PublicEntityBranchCatalog.Label(governorate, branchName));

    [Fact]
    public void ParentBranchName_IsTheJurisdictionItself_NotASubBranch()
    {
        // الحرفيّة مرآة لـ`publicEntityBranchLabel.PARENT_BRANCH_NAME` في الواجهة؛
        // اختلافُها يعني أن المصنّف يُظهر «دمشق/الجهة الأم» تكرارًا بلا معنى.
        Assert.Equal("الجهة الأم", PublicEntityBranchCatalog.ParentBranchName);
    }
}
