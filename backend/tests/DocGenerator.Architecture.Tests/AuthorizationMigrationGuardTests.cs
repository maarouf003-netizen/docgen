using System.Text.RegularExpressions;

namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P2-01 — حراس الترحيل إلى سياسات التفويض:
///
/// <para>
/// (أ) **المتحكمات المُرحَّلة** (المستندات، السجل، البوابة) لا تحمل قرارًا دورّيًا
/// مباشرًا: لا <c>RolePermissions</c> ولا مقارنة <c>Role ==/!=/is UserRole</c>
/// (وللمستندات أيضًا لا <c>GetRoleEnum</c> لأن اشتقاق الدور انتقل كليًا إلى الواجهة).
/// </para>
/// <para>
/// (ب) **قائمة انتظار راتشة**: كل متحكم لم يُرحَّل بعد وما زال يحمل قرارًا دورّيًا
/// مُدرَج بالاسم والسبب (المرحلة). أي ملف جديد بقرار دورّي يفشل فورًا (فشل مفتوح
/// مستحيل الصمت)، وأي ملف أُفرغ ولم تُحذف قائمته يفشل أيضًا (وإلا صارت القائمة
/// ادعاءً متضخمًا). إفراغ القائمة = إغلاق <c>P2-01c</c>.
/// </para>
/// <para>
/// الفحص نصّي على المصدر لا على التجميعة (السبب في تقرير P2-01a): <c>NetArchTest</c>
/// يستهدف أنواعًا لا دوال، و<see cref="RepositoryLayout.EnumerateCodeLines"/> يتجاهل
/// التعليقات. التحكم الإيجابي يثبت أن المنقّح يرى المخالفة الحقيقية.
/// </para>
/// </summary>
public class AuthorizationMigrationGuardTests
{
    /// <summary>
    /// المتحكمات المُرحَّلة (المسار من جذر مشروع `Api`): بلا `RolePermissions` ولا
    /// مقارنات دور. القيمة: تجزئات إضافية محظورة في هذا الملف وحده.
    /// </summary>
    private static readonly Dictionary<string, string[]> MigratedControllers = new(StringComparer.Ordinal)
    {
        ["Controllers/DocumentsController.cs"] = ["GetRoleEnum"],
        ["Controllers/EntityRegistryController.cs"] = [],
        ["Controllers/PortalController.cs"] = [],
    };

    /// <summary>
    /// ما تبقّى حتى `P2-01c` — ملف المتحكم ← المرحلة التي ستزيل قراره الدورّي.
    /// لا تُضف ملفًا بلا مرحلة، ولا تُبقِ ملفًا بعد إفراغه.
    /// </summary>
    private static readonly Dictionary<string, string> PendingControllers = new(StringComparer.Ordinal)
    {
        ["AppealsController.cs"] = "P2-01c",
        ["AppSuggestionsController.cs"] = "P2-01c",
        ["AuditLogsController.cs"] = "P2-01c",
        ["BranchesController.cs"] = "P2-01c",
        ["CorrespondencesController.cs"] = "P2-01c",
        ["DelegationsController.cs"] = "P2-01c",
        ["ExecutionCircuitsController.cs"] = "P2-01c",
        ["ForumController.cs"] = "P2-01c",
        ["PersonalRemindersController.cs"] = "P2-01c",
        ["ReviewLettersController.cs"] = "P2-01c",
        ["SectionsController.cs"] = "P2-01c",
        ["StatisticsController.cs"] = "P2-01c",
        ["UserManagementController.cs"] = "P2-01c",
    };

    private static readonly Regex RoleComparison = new(
        @"\bRole\s*(==|!=)\s*(DocGenerator\.Domain\.Enums\.)?UserRole\.|\bRole\s+is\s+(not\s+)?\(?\s*(DocGenerator\.Domain\.Enums\.)?UserRole\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RoleDecision = new(
        @"RolePermissions\.|\bRole\s*(==|!=)\s*(DocGenerator\.Domain\.Enums\.)?UserRole\.|\bRole\s+is\s+(not\s+)?\(?\s*(DocGenerator\.Domain\.Enums\.)?UserRole\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Theory]
    [InlineData("Controllers/DocumentsController.cs")]
    [InlineData("Controllers/EntityRegistryController.cs")]
    [InlineData("Controllers/PortalController.cs")]
    public void MigratedController_HasNoDirectRoleDecisions(string relativePath)
    {
        var lines = ReadCodeLines(relativePath);

        // حماية من خضرة كاذبة: ملف فارغ/مفقود ينجح دائمًا.
        Assert.True(lines.Count > 50, $"{relativePath}: عُثر على {lines.Count} سطر كود فقط — المسار خاطئ.");

        var forbidden = new List<string> { "RolePermissions", "RoleComparison" };
        if (MigratedControllers.TryGetValue(relativePath, out var extra))
        {
            forbidden.AddRange(extra);
        }

        var violations = lines
            .Where(line =>
                line.Code.Contains("RolePermissions", StringComparison.Ordinal)
                || RoleComparison.IsMatch(line.Code)
                || (extra is not null && extra.Any(fragment => line.Code.Contains(fragment, StringComparison.Ordinal))))
            .Select(line => $"سطر {line.LineNumber}: {line.Code.Trim()}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"{relativePath}: قرار دورّي مباشر في متحكم مُرحَّل — انقله إلى سياسة/جسر قدرات. " +
            $"المحظور: [{string.Join(", ", forbidden)}]" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void RoleDecisionInventory_MatchesThePendingAllowlist()
    {
        var controllers = EnumerateControllerFiles();
        Assert.True(controllers.Count >= 20, $"عُثر على {controllers.Count} متحكمًا فقط — مسار المصدر خاطئ.");

        var actual = controllers
            .Where(path => ReadCodeLines(path).Any(line => RoleDecision.IsMatch(line.Code)))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        var expected = PendingControllers.Keys.ToHashSet(StringComparer.Ordinal);

        var unexpected = actual.Except(expected, StringComparer.Ordinal).Order().ToList();
        var stale = expected.Except(actual, StringComparer.Ordinal).Order().ToList();

        Assert.True(
            unexpected.Count == 0 && stale.Count == 0,
            "مخالفات قائمة انتظار ترحيل التفويض:" + Environment.NewLine +
            (unexpected.Count == 0
                ? string.Empty
                : $"متحكمات بقرار دورّي خارج القائمة (رحّلها أو أدرجها بمرحلة): [{string.Join(", ", unexpected)}]" + Environment.NewLine) +
            (stale.Count == 0
                ? string.Empty
                : $"ملفات في القائمة لم يبقَ فيها قرار دورّي (احذفها من القائمة): [{string.Join(", ", stale)}]"));
    }

    /// <summary>تحكم إيجابي: المنقّح يرى المخالفة في الكود لا في التعليق.</summary>
    [Fact]
    public void Guard_DetectsRoleDecisionsInCode_AndIgnoresComments()
    {
        var codeLines = RepositoryLayout.EnumerateCodeLines(
        [
            "// RolePermissions.HasFullAccess(role) — تعليق لا يُحتسب",
            "/// <summary>GetRoleEnum() في توثيق لا يُحتسب</summary>",
            "if (!RolePermissions.CanEditDocuments(role)) return Forbid();",
            "if (Role == UserRole.Head) return Forbid();",
            "if (Role is not UserRole.Lawyer) return Forbid();",
        ]).ToList();

        Assert.Equal(3, codeLines.Count);
        Assert.Matches(RoleDecision, codeLines[0].Code);
        Assert.Matches(RoleDecision, codeLines[1].Code);
        Assert.Matches(RoleDecision, codeLines[2].Code);
    }

    private static IReadOnlyList<string> EnumerateControllerFiles()
        => Directory
            .EnumerateFiles(
                Path.Combine(RepositoryLayout.ProjectSourceDirectory("DocGenerator.Api"), "Controllers"),
                "*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static List<(int LineNumber, string Code)> ReadCodeLines(string relativePath)
    {
        var path = Path.Combine(
            RepositoryLayout.ProjectSourceDirectory("DocGenerator.Api"),
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"ملف المتحكم غير موجود: {path}");
        return RepositoryLayout.EnumerateCodeLines(File.ReadAllLines(path)).ToList();
    }
}
