using System.Text.RegularExpressions;

namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P4-06d — قاعدة اتساق إعدادات المحلّلات الخارجية مع الاعتماديات المُعلنة.
/// <para>
/// المعنى الحاكم: <c>dotnet_diagnostic.&lt;ID&gt;.severity</c> في <c>.editorconfig</c>
/// لا يفعّل شيئًا من تلقاء نفسه؛ المحلّل يجب أن يكون محمّلًا في الترجمة عبر حزمة
/// <c>PackageReference</c>. إعداد قاعدة لمحلّل غير مُشار إليه يبدو صحيحًا ويمرّر
/// البناء دون أن يفحص سطرًا واحدًا (خضرة كاذبة)، وقد ثبت عمليًا في تقييم `P4-06d`
/// أن قواعد `StyleCop`/`Roslynator` المعلنة بلا حزمها تُبقي البناء نظيفًا مع وجود
/// آلاف المخالفات الحقيقية (<c>docs/audit/252</c>).
/// </para>
/// <para>
/// ولا يكفي وجود الحزمة في مشروع واحد: إعداد `.editorconfig` عام لكل ملفات
/// <c>*.cs</c>، فالمشاريع التي لا تحمل الحزمة تتجاهله بصمت. لذلك تُعدّ الحزمة
/// «مسنودة» فقط إذا وردت في <c>Directory.Build.props</c>/<c>Directory.Build.targets</c>
/// (تُستورد في كل المشاريع) أو في **كل** ملفات المشاريع.
/// </para>
/// <para>
/// تُترك عائلات محلّلات `SDK` المدمجة (نحو <c>CA</c>/<c>IDE</c>) بلا شرط لأنها
/// موجودة مع كل ترجمة.
/// </para>
/// </summary>
internal static class AnalyzerConfigurationPolicy
{
    /// <summary>عائلة القاعدة (حروف فقط) ← الحزمة التي يجب أن تكون مُشار إليها.</summary>
    private static readonly Dictionary<string, string> PackageByRuleFamily = new(StringComparer.Ordinal)
    {
        ["RCS"] = "Roslynator.Analyzers",
        ["SA"] = "StyleCop.Analyzers",
        ["SX"] = "StyleCop.Analyzers",
    };

    /// <summary>
    /// سطر إعداد شدة لقاعدة محلّل. التعليقات مستبعدة لأن النمط يبدأ بمسافات فقط
    /// (لا <c>#</c> ولا <c>;</c>)، فلا يُحتسب ذكر القاعدة في توثيق كإعداد فعلي.
    /// <para>
    /// المقارنة غير حساسة لحالة الأحرف لأن محلّل `.editorconfig` في Roslyn يقرأ
    /// أسماء الخصائص بلا حساسية للحالة؛ تجاهل ذلك يفتح تمريرًا كاذبًا.
    /// </para>
    /// </summary>
    private static readonly Regex SeveritySetting = new(
        @"^[ \t]*dotnet_diagnostic\.(?<rule>[A-Za-z]+[0-9]+)\.severity[ \t]*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>
    /// مرجع حزمة فعليّ. <c>PackageVersion</c> وحدها لا تكفي: هي تعريف إصدار مركزي
    /// بلا تحميل المحلّل، فلا تُعدّ إشارة.
    /// </summary>
    private static readonly Regex PackageReference = new(
        @"<PackageReference\s+Include\s*=\s*""(?<package>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// يعيد قائمة المخالفات: كل إعداد شدة لقاعدة من عائلة خارجية بلا حزمة مسنودة لكل
    /// المشاريع. القائمة فارغة = الإعدادات كلها قابلة للتنفيذ فعليًا.
    /// </summary>
    /// <param name="editorConfigFiles">ملفات <c>.editorconfig</c> بمحتواها ومسارها.</param>
    /// <param name="buildFiles">ملفات البناء (<c>.csproj</c>/<c>.props</c>/<c>.targets</c>) بمحتواها ومسارها.</param>
    internal static IReadOnlyList<string> FindUnbackedSeveritySettings(
        IEnumerable<(string Path, string Content)> editorConfigFiles,
        IReadOnlyCollection<(string Path, string Content)> buildFiles)
    {
        ArgumentNullException.ThrowIfNull(editorConfigFiles);
        ArgumentNullException.ThrowIfNull(buildFiles);

        var perDirectoryProps = new List<(string Directory, HashSet<string> Packages)>();
        var referencedPerProject = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var projectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var buildFile in buildFiles)
        {
            var fileName = GetFileName(buildFile.Path);
            var isProject = fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
            if (isProject)
            {
                projectPaths.Add(Normalize(buildFile.Path));
            }

            var isDirectoryWide = string.Equals(fileName, "Directory.Build.props", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "Directory.Build.targets", StringComparison.OrdinalIgnoreCase);

            var packages = PackageReference.Matches(buildFile.Content)
                .Select(match => match.Groups["package"].Value.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (packages.Count == 0)
            {
                continue;
            }

            if (isDirectoryWide)
            {
                // `Directory.Build.props` يُستورد في كل مشروع تحت مجلده، لا في المستودع كله.
                perDirectoryProps.Add((ParentDirectory(buildFile.Path), packages));
            }
            else if (isProject)
            {
                foreach (var package in packages)
                {
                    if (!referencedPerProject.TryGetValue(package, out var projects))
                    {
                        projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        referencedPerProject[package] = projects;
                    }

                    projects.Add(Normalize(buildFile.Path));
                }
            }
        }

        var violations = new List<string>();
        foreach (var editorConfig in editorConfigFiles)
        {
            foreach (Match match in SeveritySetting.Matches(editorConfig.Content))
            {
                var ruleId = match.Groups["rule"].Value;
                var family = new string(ruleId.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();

                if (!PackageByRuleFamily.TryGetValue(family, out var package) || IsBacked(package))
                {
                    continue;
                }

                violations.Add(
                    $"{editorConfig.Path}: 'dotnet_diagnostic.{ruleId}.severity' يتطلب حزمة '{package}' " +
                    "غير مسنودة لكل المشاريع (في Directory.Build.props أو في كل مشروع) — " +
                    "الإعداد بلا تحميل المحلّل في الترجمة لا يفحص شيئًا.");
            }
        }

        return violations;

        bool IsBacked(string package)
        {
            if (projectPaths.Count == 0)
            {
                return false;
            }

            referencedPerProject.TryGetValue(package, out var projects);
            var propsDirectories = perDirectoryProps
                .Where(props => props.Packages.Contains(package))
                .Select(props => props.Directory)
                .ToList();

            foreach (var project in projectPaths)
            {
                var coveredByProps = propsDirectories.Any(directory => IsUnder(project, directory));
                if (!coveredByProps && (projects is null || !projects.Contains(project)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string ParentDirectory(string path)
    {
        var normalized = Normalize(path);
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? string.Empty : normalized[..separator];
    }

    private static bool IsUnder(string projectPath, string directory)
    {
        if (directory.Length == 0)
        {
            return true;
        }

        return projectPath.StartsWith(directory + "/", StringComparison.Ordinal);
    }

    private static string GetFileName(string path)
    {
        var normalized = Normalize(path);
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? normalized : normalized[(separator + 1)..];
    }
}
