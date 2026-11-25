namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P1-06 — أدوات تحديد مواقع المصادر داخل المستودع للفحوص النصية.
/// <para>
/// الفحص النصي (raw SQL) يحتاج ملفات <c>.cs</c> لا تجميعة مُصرَّفة، ويجب ألا يعتمد
/// على مسارات مطلقة في جهاز المطوّر. لذلك يصعد المساعد من مجلد تشغيل الاختبار
/// (<see cref="AppContext.BaseDirectory"/>) حتى يجد جذر المستودع المميّز بوجود
/// <c>backend/DocGenerator.sln</c>، ثم يتحقق من وجود ملف المشروع المطلوب حتى لا
/// ينتهي الفحص بصمت إلى مجلد خاطئ أو فارغ.
/// </para>
/// </summary>
internal static class RepositoryLayout
{
    private const string SolutionRelativePath = "backend/DocGenerator.sln";

    /// <summary>
    /// يعيد المسار المطلق لجذر المستودع (المجلد الذي يضم <c>backend/DocGenerator.sln</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">إذا لم يُعثر على الجذر.</exception>
    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "DocGenerator.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"لم يُعثر على جذر المستودع (يُعرَّف بوجود '{SolutionRelativePath}') صعودًا من '{AppContext.BaseDirectory}'.");
    }

    /// <summary>يعيد المسار المطلق لمجلد مصادر مشروع داخل <c>backend/src</c>.</summary>
    /// <exception cref="InvalidOperationException">إذا لم يُعثر على الجذر أو المشروع.</exception>
    internal static string ProjectSourceDirectory(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var root = RepositoryRoot();
        var projectDirectory = Path.Combine(root, "backend", "src", projectName);
        if (File.Exists(Path.Combine(projectDirectory, projectName + ".csproj")))
        {
            return projectDirectory;
        }

        throw new InvalidOperationException(
            $"وُجد جذر المستودع في '{root}' لكن مشروع '{projectName}' غير موجود في 'backend/src'.");
    }

    /// <summary>يسرد ملفات <c>.cs</c> المصدرية فقط، مستبعدًا مخرجات البناء والملفات المولّدة.</summary>
    internal static IReadOnlyList<string> EnumerateSourceFiles(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);

        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifactOrGeneratedFile(projectDirectory, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// يعيد أسطر الكود مع رقم السطر، بعد تجاهل التعليقات السطرية والكتلية.
    /// <para>
    /// التجاهل مقصود: ذكر <c>FromSqlRaw</c> في توثيق «ممنوع الاستخدام» ليس مخالفة،
    /// بينما أي ذكر في كود فعلي يُعدّ مخالفة.
    /// </para>
    /// </summary>
    internal static IEnumerable<(int LineNumber, string Code)> EnumerateCodeLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var insideBlockComment = false;
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            var remainder = line;

            while (true)
            {
                if (insideBlockComment)
                {
                    var blockEnd = remainder.IndexOf("*/", StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        remainder = string.Empty;
                        break;
                    }

                    insideBlockComment = false;
                    remainder = remainder[(blockEnd + 2)..];
                    continue;
                }

                var trimmed = remainder.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    // تعليق حتى نهاية السطر (بما فيه /// التوثيقية).
                    remainder = string.Empty;
                    break;
                }

                if (trimmed.StartsWith("/*", StringComparison.Ordinal))
                {
                    var blockEnd = remainder.IndexOf("*/", StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        insideBlockComment = true;
                        remainder = string.Empty;
                        break;
                    }

                    remainder = remainder[(blockEnd + 2)..];
                    continue;
                }

                break;
            }

            if (remainder.Length > 0)
            {
                yield return (lineNumber, remainder);
            }
        }
    }

    private static bool IsBuildArtifactOrGeneratedFile(string projectDirectory, string path)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, path);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => segment is "bin" or "obj"))
        {
            return true;
        }

        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }
}
