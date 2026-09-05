using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// تشابه الأسماء العربية للجهات العامة — خوارزمية هجينة (Hybrid) تجمع:
/// Jaccard على Bigrams + Normalized Levenshtein + Token-Jaccard، بمتوسط مرجّح،
/// بعد التطبيع بـ <see cref="ArabicNameNormalizer"/>.
/// تُستخدم لكشف «المجموعات المتشابهة» (Union-Find) ولاقتراح «مشابهات جهة محددة».
/// </summary>
public static class ArabicNameSimilarity
{
    /// <summary>عتبة تجميع المجموعات المتشابهة في تبويب «المجموعات المتشابهة».</summary>
    public const double DefaultClusterThreshold = 0.55;

    /// <summary>عتبة اقتراح مشابهات لجهة محددة (أقل حساسية من عتبة التجميع).</summary>
    public const double DefaultSimilarToThreshold = 0.55;

    /// <summary>أقصى عدد من المقترحات لجهة محددة.</summary>
    public const int DefaultMaxSimilarResults = 10;

    // أوزان المقاييس الثلاثة في المتوسط المرجّح — مجموعها 1.0.
    private const double BigramWeight = 0.4;
    private const double LevenshteinWeight = 0.3;
    private const double TokenWeight = 0.3;

    /// <summary>
    /// عبارات وظيفية تُعدّ ثانوية في تسميات الجهات العامة (نحو «المدير العام» / «مدير عام» /
    /// «- المدير العام») لأنها تصف منصبًا أو فرعًا لا جوهر الجهة. عِند المقارنة، نسمح باختلافٍ
    /// في هذه العبارات دون أن يُقاس حرفيًا كاملًا — فيرتفع تشابه الجهة نفسها مهما اختلفت صياغتها.
    /// </summary>
    private static readonly string[] FunctionalPhrases =
    {
        "المدير العام",
        "مدير عام",
        "مدير",
        "الادارة العامة",
        "الإدارة العامة",
        "فرع", // «- فرع دمشق» لا تغيّر جوهر الجهة
    };

    /// <summary>حساب درجة التشابه الهجينة بين اسمين (في [0,1]) — تطبيع داخليًا.</summary>
    public static double Similarity(string? nameA, string? nameB)
    {
        var normA = ArabicNameNormalizer.Normalize(nameA);
        var normB = ArabicNameNormalizer.Normalize(nameB);
        if (normA.Length == 0 || normB.Length == 0)
            return 0.0;
        if (normA == normB)
            return 1.0;

        // معالجة الكلمات الوظيفية: نزيلها مؤقتًا من نص المقارنة ثم نكافئ — إذا كان ثمة تعادل
        // جوهرًا بعد الإزالة نعيد درجة عالية (المماثلة في الجوهر تتجاوز اختلاف الصياغة الوظيفية).
        var coreA = StripFunctionalPhrases(normA);
        var coreB = StripFunctionalPhrases(normB);

        // التشابه على الجوهر (دون العبارات الوظيفية) — يلتقط «السورية للبناء» vs «السورية للبناء - المدير العام».
        double coreSim;
        if (coreA.Length > 0 && coreB.Length > 0)
            coreSim = CoreSimilarity(coreA, coreB);
        else
            coreSim = CoreSimilarity(normA, normB);

        // التشابه على النص الكامل (مع العبارات الوظيفية) للتمييز بين جهات مختلفة فعليًا.
        var fullSim = CoreSimilarity(normA, normB);

        // نأخذ الأقصى من تشابه الجوهر وتشابه النص الكامل: فإذا تطابق الجوهر ارتفعت الدرجة،
        // وإذا اختلفت الجهتان فعليًا (جوهر مختلف) بقي التشابه منخفضًا.
        return Math.Max(coreSim, fullSim);
    }

    private static double CoreSimilarity(string a, string b)
    {
        var bigram = JaccardBigrams(a, b);
        var lev = NormalizedLevenshtein(a, b);
        var token = TokenJaccard(a, b);
        return bigram * BigramWeight + lev * LevenshteinWeight + token * TokenWeight;
    }

    /// <summary>إزالة العبارات الوظيفية من نص الاسم المطبَّع (تُزال أكبر عبارة أولًا).</summary>
    private static string StripFunctionalPhrases(string normalized)
    {
        var result = normalized;
        foreach (var phrase in FunctionalPhrases.OrderByDescending(p => p.Length))
        {
            var normPhrase = ArabicNameNormalizer.Normalize(phrase);
            if (normPhrase.Length == 0)
                continue;
            result = result.Replace(normPhrase, " ", StringComparison.Ordinal);
        }
        return string.Join(' ', result.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Jaccard على أزواج الأحرف المتتالية (Bigrams). قاعدة: |تقاطع| / |اتحاد|.
    /// النتيجة في [0,1].
    /// </summary>
    public static double JaccardBigrams(string a, string b)
    {
        if (a.Length < 2 || b.Length < 2)
        {
            // أسماء قصيرة جدًا: نقارن المساواة الحرفية فقط.
            return string.Equals(a, b, StringComparison.Ordinal) ? 1.0 : 0.0;
        }

        var setA = BigramSet(a);
        var setB = BigramSet(b);

        int intersection = 0;
        foreach (var gram in setA)
            if (setB.Contains(gram))
                intersection++;

        var union = setA.Count + setB.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// مسافة التعديل (Levenshtein) الطبيعية: 1 - (distance / max(a,b)).
    /// النتيجة في [0,1] حيث 1 = تطابق تام.
    /// </summary>
    public static double NormalizedLevenshtein(string a, string b)
    {
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0)
            return 1.0;
        return 1.0 - (double)LevenshteinDistance(a, b) / maxLen;
    }

    /// <summary>
    /// Jaccard على مجموعات الكلمات (Token Jaccard) بعد تقسيم الاسم إلى كلمات.
    /// النتيجة في [0,1].
    /// </summary>
    public static double TokenJaccard(string a, string b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        if (tokensA.Count == 0 && tokensB.Count == 0)
            return 1.0;
        if (tokensA.Count == 0 || tokensB.Count == 0)
            return 0.0;

        int intersection = 0;
        foreach (var t in tokensA)
            if (tokensB.Contains(t))
                intersection++;

        var union = tokensA.Count + tokensB.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// تجميع مجموعات متشابهة (الهويات الأم النشطة) عبر Union-Find: كل زوج تجاوز
    /// العتبة يُربط في بيئة واحدة. تُعاد قوائم القيود (بأرقام مجموعاتها).
    /// لا تُتضمَّن إلا المجموعات النشطة، ويُستثنى أي زوج متطابق التطبيع أو المجموعة مع نفسها.
    /// </summary>
    public static List<List<PublicEntityGroup>> ClusterGroups(
        IReadOnlyList<PublicEntityGroup> groups,
        double threshold = DefaultClusterThreshold)
    {
        var active = groups.Where(g => g.IsActive).ToList();
        if (active.Count < 2)
            return new List<List<PublicEntityGroup>>();

        var parent = new int[active.Count];
        for (int i = 0; i < parent.Length; i++)
            parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        void Union(int x, int y)
        {
            var rx = Find(x);
            var ry = Find(y);
            if (rx != ry)
                parent[ry] = rx;
        }

        for (int i = 0; i < active.Count; i++)
        {
            for (int j = i + 1; j < active.Count; j++)
            {
                var gi = active[i];
                var gj = active[j];
                if (gi.Id == gj.Id)
                    continue;
                var sim = Similarity(gi.CanonicalName, gj.CanonicalName);
                if (sim >= threshold)
                    Union(i, j);
            }
        }

        var clusters = new Dictionary<int, List<PublicEntityGroup>>();
        for (int i = 0; i < active.Count; i++)
        {
            var root = Find(i);
            if (!clusters.TryGetValue(root, out var list))
                clusters[root] = list = new List<PublicEntityGroup>();
            list.Add(active[i]);
        }

        // نُبقي البيئات التي تحتوي على مجموعهتين على الأقل (مجموعة حقيقية).
        return clusters.Values
            .Where(c => c.Count >= 2)
            .Select(c => c.OrderBy(g => g.CanonicalName, StringComparer.Ordinal).ToList())
            .ToList();
    }

    /// <summary>أقرب المشابهات لمجموعة محددة (ضمن النشطة، دون المجموعة نفسها) فوق عتبة، مرتبة تنازليًا.</summary>
    public static List<(PublicEntityGroup Group, double Similarity)> FindSimilarTo(
        IReadOnlyList<PublicEntityGroup> groups,
        PublicEntityGroup target,
        double threshold = DefaultSimilarToThreshold,
        int maxResults = DefaultMaxSimilarResults)
    {
        var result = groups
            .Where(g => g.IsActive && g.Id != target.Id)
            .Select(g => (Group: g, Sim: Similarity(target.CanonicalName, g.CanonicalName)))
            .Where(x => x.Sim >= threshold)
            .OrderByDescending(x => x.Sim)
            .ThenBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .Take(maxResults)
            .ToList();
        return result;
    }

    // ── مساعدات خاصة ──

    private static HashSet<string> BigramSet(string s)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i <= s.Length - 2; i++)
            set.Add(s.Substring(i, 2));
        return set;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0)
            return m;
        if (m == 0)
            return n;

        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (int j = 0; j <= m; j++)
            prev[j] = j;

        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }

    private static HashSet<string> Tokenize(string s)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            tokens.Add(token);
        return tokens;
    }
}
