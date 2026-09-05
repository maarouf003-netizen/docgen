using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests;

public class ArabicNameSimilarityTests
{
    // ── تطبيع داخلي ومنطق القياس ──

    [Fact]
    public void Similarity_IdenticalNames_ReturnsOne()
    {
        Assert.Equal(1.0, ArabicNameSimilarity.Similarity("المصرف التجاري السوري", "المصرف التجاري السوري"));
    }

    [Fact]
    public void Similarity_NormalizedVariants_ReturnsHigh()
    {
        // أ/إ/آ → ا ، ة → ه ، ى → ي — تطبيع يجعل الاختلافات الشكلية تساوي 1.
        Assert.Equal(1.0, ArabicNameSimilarity.Similarity("السورية للبناء", "السوريه للبناء"));
    }

    [Fact]
    public void Similarity_EmptyNames_ReturnsZero()
    {
        Assert.Equal(0.0, ArabicNameSimilarity.Similarity("", "المصرف"));
        Assert.Equal(0.0, ArabicNameSimilarity.Similarity("   ", ""));
        Assert.Equal(0.0, ArabicNameSimilarity.Similarity(null, "المصرف"));
    }

    [Fact]
    public void Similarity_CompletelyDifferent_ReturnsLow()
    {
        var sim = ArabicNameSimilarity.Similarity("المصرف التجاري السوري", "هيئة الاستثمار والتجارة");
        Assert.True(sim < 0.4);
    }

    [Fact]
    public void Similarity_SameEntityVaryingWording_HighEnough()
    {
        // مثال من منطق العمل: إضافة «المدير العام» أو صياغة مختلفة للجهة نفسها.
        var sim = ArabicNameSimilarity.Similarity(
            "المصرف التجاري السوري",
            "المصرف التجاري السوري - المدير العام");
        Assert.True(sim >= 0.5, $"got {sim}");
    }

    [Fact]
    public void Similarity_UserPrimaryExample_FunctionalWordFlagged()
    {
        // المثال الأهم: «المدير العام» إضافة وظيفية لا تغيّر جوهر الجهة.
        var sim = ArabicNameSimilarity.Similarity("المصرف التجاري السوري", "المصرف التجاري السوري - المدير العام");
        Assert.True(sim >= ArabicNameSimilarity.DefaultClusterThreshold, $"got {sim}");
    }

    [Fact]
    public void Similarity_MissingSegment_Moderate()
    {
        // «السورية للبناء» مقابل «السورية للبناء والتشييد» — تشابه جزئي واضح.
        var sim = ArabicNameSimilarity.Similarity("السورية للبناء", "السورية للبناء والتشييد");
        Assert.True(sim >= 0.5 && sim < 1.0, $"got {sim}");
    }

    // ── مقاييس مفردة ──

    [Fact]
    public void JaccardBigrams_Identical_ReturnsOne()
    {
        Assert.Equal(1.0, ArabicNameSimilarity.JaccardBigrams("السورية", "السورية"));
    }

    [Fact]
    public void NormalizedLevenshtein_Same_ReturnsOne()
    {
        Assert.Equal(1.0, ArabicNameSimilarity.NormalizedLevenshtein("سوريا", "سوريا"));
    }

    [Fact]
    public void NormalizedLevenshtein_OneEdit_CloseToMatchingLength()
    {
        var d = ArabicNameSimilarity.NormalizedLevenshtein("بنوك", "بنك");
        Assert.InRange(d, 0.5, 1.0);
    }

    [Fact]
    public void TokenJaccard_SharedWords_Positive()
    {
        var t = ArabicNameSimilarity.TokenJaccard("المصرف التجاري", "المصرف الزراعي");
        Assert.True(t > 0 && t < 1.0, $"got {t}");
    }

    // ── التجميع (Union-Find) ──

    [Fact]
    public void ClusterGroups_SimilarGroups_Grouped()
    {
        var groups = new List<PublicEntityGroup>
        {
            Group(1, "السورية للبناء والتشييد"),
            Group(2, "سورية للبناء والتشييد"),
            Group(3, "المصرف التجاري السوري"),
            Group(4, "الشركة السورية للبناء والتشييد"),
        };

        var clusters = ArabicNameSimilarity.ClusterGroups(groups);

        // نتوقع تجمع المجموعات المشابهة للبناء والتشييد في بيئة واحدة على الأقل.
        Assert.Contains(clusters, c => c.Any(x => x.Id == 1) && c.Any(x => x.Id == 2));
        // كل تجمع يضم 2 على الأقل.
        Assert.All(clusters, c => Assert.True(c.Count >= 2));
    }

    [Fact]
    public void ClusterGroups_SingleGroup_NoClusters()
    {
        var groups = new List<PublicEntityGroup> { Group(1, "وحيد") };
        Assert.Empty(ArabicNameSimilarity.ClusterGroups(groups));
    }

    [Fact]
    public void ClusterGroups_ExcludesInactive()
    {
        var inactive = Group(2, "المصرف التجاري السوري - المدير العام");
        inactive.IsActive = false;
        var groups = new List<PublicEntityGroup>
        {
            Group(1, "المصرف التجاري السوري"),
            inactive,
        };
        Assert.Empty(ArabicNameSimilarity.ClusterGroups(groups));
    }

    // ── مشابهات جهة محددة ──

    [Fact]
    public void FindSimilarTo_ReturnsRankedSimilarities()
    {
        var groups = new List<PublicEntityGroup>
        {
            Group(1, "المصرف التجاري السوري"),
            Group(2, "المصرف التجاري السوري - المدير العام"),
            Group(3, "هيئة الاستثمار"),
        };
        var target = groups[0];

        var similar = ArabicNameSimilarity.FindSimilarTo(groups, target, threshold: 0.4);

        Assert.Contains(similar, x => x.Group.Id == 2);
        Assert.DoesNotContain(similar, x => x.Group.Id == 3);
        // مرتبة تنازليًا.
        for (int i = 1; i < similar.Count; i++)
            Assert.True(similar[i - 1].Similarity >= similar[i].Similarity);
    }

    [Fact]
    public void FindSimilarTo_ExcludesSelf()
    {
        var groups = new List<PublicEntityGroup> { Group(1, "المصرف") };
        Assert.Empty(ArabicNameSimilarity.FindSimilarTo(groups, groups[0]));
    }

    private static PublicEntityGroup Group(int id, string name) => new()
    {
        Id = id,
        CanonicalName = name,
        IsActive = true,
    };
}
