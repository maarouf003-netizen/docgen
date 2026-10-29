using System.IO;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// محدِّد جذر المستودع لاختبارات العقود المشتركة: يصعد من مجلد تشغيل الاختبار
/// (<see cref="AppContext.BaseDirectory"/>) حتى يجد علامة ثابتة (<c>AGENTS.md</c>)،
/// ثم يحلّ المسارات النسبية بفواصل المنصة — فلا يفترض <c>\</c> على Windows فقط
/// ولا يفشل على <c>ubuntu-latest</c> في CI (الخطة §8.10).
///
/// الفشل صريح برسالة تحمل المسار المطلق الذي بُحث عنه، لا <c>null</c> صامتًا.
/// </summary>
internal static class RepoRootLocator
{
    /// <summary>علامة جذر المستودع — موجودة في الجذر ولا تُنقل.</summary>
    private const string MarkerFile = "AGENTS.md";

    /// <summary>يعيد المسار المطلق لجذر المستودع.</summary>
    /// <exception cref="InvalidOperationException">إن لم تُوجد علامة الجذر في أي مجلد أب.</exception>
    public static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, MarkerFile)))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"تعذّر العثور على جذر المستودع: لا يوجد ملف العلامة '{MarkerFile}' في أي مجلد أبٍ " +
            $"ابتداءً من '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// يحل مسارًا نسبيًا من جذر المستودع (بفواصل <c>/</c> كما في المستودع) ويعيد مساره المطلق،
    /// ويرمي رسالة صريحة إن لم يوجد الملف.
    /// </summary>
    public static string ResolveFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var root = FindRoot();
        var platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(root, platformPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"لم يُعثر على ملف العقد المشترك '{relativePath}' داخل جذر المستودع المكتشف '{root}'. " +
                $"المسار المتوقع: '{fullPath}'.",
                fullPath);
        }

        return fullPath;
    }
}
