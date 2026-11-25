using System.Text;

namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P4-06d — حارس «الإعداد الفعّال» للمحلّلات: أي شدة قاعدة معلنة لمحلّل خارجي
/// (<c>RCS</c>/<c>SA</c>/<c>SX</c>) يجب أن تقابلها حزمة مُشار إليها **في كل المشاريع**
/// (أو في <c>Directory.Build.props</c> الذي تُستورد فيه).
/// <para>
/// سبب الحارس: تقييم `P4-06d` (<c>docs/audit/252</c>) أثبت أن إعداد
/// <c>dotnet_diagnostic.SA1101.severity = error</c> **بلا حزمة StyleCop** يمرّر
/// البناء بـ0 تحذير/0 خطأ مع وجود ~21,571 مخالفة حقيقية للقاعدة في المصدر نفسه،
/// بينما وجود الحزمة يجعل البناء يفشل فورًا. فالإعداد بلا حزمة ليس «تشديدًا مؤجلًا»
/// بل خضرة كاذبة، وهذا الحارس يمنع إدخالها.
/// </para>
/// </summary>
public class AnalyzerConfigurationTests
{
    /// <summary>مجلدات لا تُساهم في إعدادات البناء المتتبَّعة (مخرجات/اعتماديات/أدوات).</summary>
    private static readonly string[] PrunedDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".vs",
        "node_modules",
        "dist",
        "coverage",
        "TestResults",
        "artifacts",
    ];

    /// <summary>
    /// الحالة الحقيقية للمستودع: لا إعداد شدة لمحلّل خارجي بلا حزمته.
    /// <para>
    /// الفحص يقع على الملفات الفعلية لا على قائمة محفوظة، فأي `.editorconfig`
    /// جديد (جذريًا أو متداخلًا) يدخل الحارس تلقائيًا؛ وثبات صلاحية المحرّك نفسه
    /// مُبرهن في الاختبارات التالية بمخالفات مصطنعة.
    /// </para>
    /// </summary>
    [Fact]
    public void Repository_AnalyzerSeveritySettings_AreBackedByReferencedPackages()
    {
        var root = RepositoryLayout.RepositoryRoot();

        var editorConfigFiles = EnumerateRepoFiles(
                root,
                fileName => string.Equals(fileName, ".editorconfig", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, ".globalconfig", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: Relative(root, path), Content: File.ReadAllText(path)))
            .ToList();

        var buildFiles = EnumerateRepoFiles(
                Path.Combine(root, "backend"),
                fileName => fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: Relative(root, path), Content: File.ReadAllText(path)))
            .ToList();

        Assert.True(editorConfigFiles.Count > 0, "لم يُعثر على أي '.editorconfig' — لا يمكن أن يكون الفحص صحيحًا.");
        Assert.True(
            buildFiles.Count >= 7,
            $"وُجد {buildFiles.Count} ملف بناء فقط تحت 'backend' — أقل من مشاريع الحلّ المتوقعة.");

        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(editorConfigFiles, buildFiles);

        Assert.True(violations.Count == 0, BuildFailureMessage(violations));
    }

    [Theory]
    [InlineData("SA1101", "StyleCop.Analyzers")]
    [InlineData("SX1101", "StyleCop.Analyzers")]
    [InlineData("RCS1194", "Roslynator.Analyzers")]
    public void Policy_FlagsSeveritySettingForAbsentAnalyzerPackage(string ruleId, string package)
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", $"[*.cs]{Environment.NewLine}dotnet_diagnostic.{ruleId}.severity = error{Environment.NewLine}")],
            [("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", "<Project />")]);

        var violation = Assert.Single(violations);
        Assert.Contains(ruleId, violation, StringComparison.Ordinal);
        Assert.Contains(package, violation, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_AcceptsSeveritySettingWhenPackageIsReferencedDirectoryWide()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine
                + "dotnet_diagnostic.SA1101.severity = error" + Environment.NewLine
                + "dotnet_diagnostic.RCS1194.severity = warning" + Environment.NewLine)],
            [
                (
                    "backend/Directory.Build.props",
                    "<Project><ItemGroup><PackageReference Include=\"StyleCop.Analyzers\" PrivateAssets=\"all\" />"
                        + "<PackageReference Include=\"Roslynator.Analyzers\" PrivateAssets=\"all\" /></ItemGroup></Project>"),
                ("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", "<Project />"),
            ]);

        Assert.Empty(violations);
    }

    /// <summary>
    /// مشروع واحد فقط يحمل الحزمة ليس كافيًا: الإعداد في `.editorconfig` عام لكل
    /// ملفات `*.cs`، فأي مشروع بلا الحزمة يتجاهل الإعداد بصمت.
    /// </summary>
    [Fact]
    public void Policy_RejectsPackageReferencedInOnlyOneProject()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine + "dotnet_diagnostic.SA1101.severity = error" + Environment.NewLine)],
            [
                (
                    "backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj",
                    "<Project><ItemGroup><PackageReference Include=\"StyleCop.Analyzers\" PrivateAssets=\"all\" /></ItemGroup></Project>"),
                ("backend/src/DocGenerator.Application/DocGenerator.Application.csproj", "<Project />"),
            ]);

        Assert.Single(violations);
    }

    [Fact]
    public void Policy_AcceptsPackageReferencedInEveryProject()
    {
        const string reference = "<Project><ItemGroup><PackageReference Include=\"StyleCop.Analyzers\" /></ItemGroup></Project>";

        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine + "dotnet_diagnostic.SA1101.severity = error" + Environment.NewLine)],
            [
                ("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", reference),
                ("backend/src/DocGenerator.Application/DocGenerator.Application.csproj", reference),
            ]);

        Assert.Empty(violations);
    }

    /// <summary>
    /// `Directory.Build.props` في مجلد فرعي يغطي مشاريع ذلك المجلد وحده (كما يفعل
    /// MSBuild)؛ فلا يكفي لإعداد عام لكل `*.cs` في المستودع.
    /// </summary>
    [Fact]
    public void Policy_RejectsPackageInDirectoryPropsThatCoversOnlyPartOfTheProjects()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine + "dotnet_diagnostic.SA1101.severity = error" + Environment.NewLine)],
            [
                (
                    "backend/tests/Directory.Build.props",
                    "<Project><ItemGroup><PackageReference Include=\"StyleCop.Analyzers\" PrivateAssets=\"all\" /></ItemGroup></Project>"),
                ("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", "<Project />"),
                ("backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj", "<Project />"),
            ]);

        Assert.Single(violations);
    }

    /// <summary>
    /// محلّلات `SDK` المدمجة (<c>CA</c>/<c>IDE</c>) ليست حزمًا خارجية، وقواعدها
    /// تعمل بلا أي <c>PackageReference</c> — فلا تُعدّ مخالفة.
    /// </summary>
    [Fact]
    public void Policy_IgnoresBuiltInSdkAnalyzerFamilies()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine
                + "dotnet_diagnostic.CA2007.severity = error" + Environment.NewLine
                + "dotnet_diagnostic.IDE0005.severity = warning" + Environment.NewLine)],
            [("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", "<Project />")]);

        Assert.Empty(violations);
    }

    /// <summary>
    /// القاعدة المعلّقة بكامل السطر ليست إعدادًا فعليًا؛ عدّها مخالفة يعني منع
    /// توثيق القرار في `.editorconfig` نفسه.
    /// </summary>
    [Fact]
    public void Policy_IgnoresCommentedOutSeveritySettings()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine
                + "# dotnet_diagnostic.SA1101.severity = error" + Environment.NewLine
                + "; dotnet_diagnostic.RCS1194.severity = warning" + Environment.NewLine)],
            [("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", "<Project />")]);

        Assert.Empty(violations);
    }

    /// <summary>
    /// تعريف الإصدار المركزي وحده لا يحمّل المحلّل؛ الإشارة الفعلية
    /// (<c>PackageReference</c>) هي الشرط.
    /// </summary>
    [Fact]
    public void Policy_TreatsCentralPackageVersionAloneAsNotReferenced()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine + "dotnet_diagnostic.SA1101.severity = error" + Environment.NewLine)],
            [
                (
                    "backend/Directory.Packages.props",
                    "<Project><ItemGroup><PackageVersion Include=\"StyleCop.Analyzers\" Version=\"1.3.180\" /></ItemGroup></Project>"),
            ]);

        Assert.Single(violations);
    }

    /// <summary>
    /// محلّل `.editorconfig` في Roslyn يقرأ أسماء الخصائص بلا حساسية للحالة؛ لو كان
    /// الحارس حسّاسًا للحالة لمرّر `DotNet_Diagnostic.SA1101.Severity` بلا حزمة.
    /// </summary>
    [Fact]
    public void Policy_IsCaseInsensitiveAboutSeveritySettingKeys()
    {
        var violations = AnalyzerConfigurationPolicy.FindUnbackedSeveritySettings(
            [(".editorconfig", "[*.cs]" + Environment.NewLine + "DotNet_Diagnostic.SA1101.Severity = error" + Environment.NewLine)],
            [("backend/src/DocGenerator.Domain/DocGenerator.Domain.csproj", "<Project />")]);

        Assert.Single(violations);
    }

    private static string BuildFailureMessage(IReadOnlyList<string> violations)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "إعداد شدة قاعدة لمحلّل خارجي بلا حزمة مُشار إليها لا يفحص شيئًا (خضرة كاذبة). "
            + "أضف PackageReference للحزمة أو أزل الإعداد. المرجع: docs/audit/252.");
        foreach (var violation in violations)
        {
            builder.AppendLine("- " + violation);
        }

        return builder.ToString();
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static IEnumerable<string> EnumerateRepoFiles(string root, Func<string, bool> fileNamePredicate)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));
        var matches = new List<string>();

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var subdirectory in directory.EnumerateDirectories())
            {
                if (!PrunedDirectoryNames.Contains(subdirectory.Name, StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(subdirectory);
                }
            }

            foreach (var file in directory.EnumerateFiles())
            {
                if (fileNamePredicate(file.Name))
                {
                    matches.Add(file.FullName);
                }
            }
        }

        matches.Sort(StringComparer.Ordinal);
        return matches;
    }
}
