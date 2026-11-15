namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P1-06 — حظر raw SQL داخل طبقة Application.
/// <para>
/// NetArchTest يفحص الاعتماديات بين الأنواع، ولا يرى نص SQL المكتوب في سلاسل ولا
/// أسماء الامتدادات التي تُستدعى عبر <c>Microsoft.EntityFrameworkCore</c>؛ لذلك
/// يُفحص مصدر Application نصيًا (مع تجاهل التعليقات) كما أوصت خارطة الطريق عند عدم
/// دعم المكتبة للقاعدة مباشرة. قاعدة «Application بلا اعتماد على EF Core» تغطي
/// ما يمكن رصده بنيويًا، وهذا الفحص يغطي النص المباشر.
/// </para>
/// </summary>
public class RawSqlBoundaryTests
{
    /// <summary>
    /// البادئات تكفي لتغطية العائلة كاملة: <c>FromSql</c> (Raw/Interpolated/Composed)
    /// و<c>ExecuteSql</c> (Raw/Interpolated/Command).
    /// </summary>
    private static readonly string[] ForbiddenFragments = ["FromSql", "ExecuteSql"];

    [Fact]
    public void ApplicationSources_DoNotContain_RawSqlCalls()
    {
        var applicationDirectory = RepositoryLayout.ProjectSourceDirectory("DocGenerator.Application");
        var sourceFiles = RepositoryLayout.EnumerateSourceFiles(applicationDirectory);

        Assert.NotEmpty(sourceFiles);

        var violations = new List<string>();

        foreach (var sourceFile in sourceFiles)
        {
            foreach (var (lineNumber, code) in RepositoryLayout.EnumerateCodeLines(File.ReadLines(sourceFile)))
            {
                foreach (var fragment in ForbiddenFragments)
                {
                    if (code.Contains(fragment, StringComparison.Ordinal))
                    {
                        var relativePath = Path.GetRelativePath(applicationDirectory, sourceFile);
                        violations.Add($"{relativePath}:{lineNumber}: {code.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "طبقة Application يجب أن تبقى خالية من raw SQL (FromSql*/ExecuteSql*)."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CodeLineScanner_IgnoresComments_ButKeepsRealCode()
    {
        // اختبار مضاد للخضرة الكاذبة: لو انكسر مُنقّح التعليقات فسيمر الفحص الأول
        // بصمت على كود مخالف، أو يفشل زورًا على توثيق يذكر الدالة المحظورة.
        string[] lines =
        [
            "// db.Documents.FromSqlRaw(\"SELECT * FROM Documents\") — ممنوع",
            "/// <summary>نداء FromSql/ExecuteSql محظور في هذه الطبقة.</summary>",
            "/*",
            "   ExecuteSqlRaw(\"UPDATE Documents SET x = 1\")",
            "*/",
            "var query = db.Documents.FromSql($\"SELECT * FROM {table}\");",
            "/* تعليق كتلي بسطر واحد */ var raw = \"ExecuteSql\";",
        ];

        var codeLines = RepositoryLayout.EnumerateCodeLines(lines).ToList();

        Assert.Equal(2, codeLines.Count);
        Assert.Equal(6, codeLines[0].LineNumber);
        Assert.Contains("FromSql", codeLines[0].Code, StringComparison.Ordinal);
        Assert.Equal(7, codeLines[1].LineNumber);
        Assert.Contains("ExecuteSql", codeLines[1].Code, StringComparison.Ordinal);
    }
}
