namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P2-01a — حارس بنيوي على متحكم المستندات: بعد ترحيله إلى سياسة موردية
/// (<c>IDocumentAuthorization</c>) لا يعود إليه أي قرار دورّي مباشر.
///
/// <para>
/// الفحص نصّي على المصدر لا على التجميعة: <c>RolePermissions.X</c> نداء ثابت و
/// <c>GetRoleEnum()</c> دالة امتداد، ولا يستطيع <c>NetArchTest</c> استهداف الدالة
/// وحدها دون النوع كله (المتحكم يستخدم بقية امتدادات الهوية مشروعةً). يتجاهل الفحص
/// التعليقات عمدًا عبر <see cref="RepositoryLayout.EnumerateCodeLines"/>.
/// </para>
/// </summary>
public class DocumentsControllerAuthorizationGuardTests
{
    private const string ControllerFileName = "DocumentsController.cs";

    private static readonly string[] ForbiddenFragments = ["RolePermissions", "GetRoleEnum"];

    [Fact]
    public void DocumentsController_HasNoDirectRoleDecisions()
    {
        var controllerPath = Path.Combine(
            RepositoryLayout.ProjectSourceDirectory("DocGenerator.Api"),
            "Controllers",
            ControllerFileName);

        Assert.True(File.Exists(controllerPath), $"ملف المتحكم غير موجود: {controllerPath}");

        var codeLines = RepositoryLayout
            .EnumerateCodeLines(File.ReadAllLines(controllerPath))
            .ToList();

        // حماية من خضرة كاذبة: مسح ملف فارغ/مفقود ينجح دائمًا.
        Assert.True(
            codeLines.Count > 100,
            $"عُثر على {codeLines.Count} سطر كود فقط في المتحكم — مسار/قارئ المصدر خاطئ.");

        var violations = codeLines
            .Where(line => ForbiddenFragments.Any(fragment =>
                line.Code.Contains(fragment, StringComparison.Ordinal)))
            .Select(line => $"سطر {line.LineNumber}: {line.Code.Trim()}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            "قرار دورّي مباشر في متحكم المستندات — انقله إلى IDocumentAuthorization:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>تحكم إيجابي: المنقّح يرى المخالفة في الكود لا في التعليق.</summary>
    [Fact]
    public void Guard_DetectsViolationsInCode_AndIgnoresComments()
    {
        var codeLines = RepositoryLayout.EnumerateCodeLines(
        [
            "// RolePermissions.HasFullAccess(role) — تعليق لا يُحتسب",
            "/// <summary>GetRoleEnum() في توثيق لا يُحتسب</summary>",
            "if (!RolePermissions.CanEditDocuments(role)) return Forbid();",
            "var role = User.GetRoleEnum();",
        ]).ToList();

        Assert.Equal(2, codeLines.Count);
        Assert.Contains("RolePermissions", codeLines[0].Code, StringComparison.Ordinal);
        Assert.Contains("GetRoleEnum", codeLines[1].Code, StringComparison.Ordinal);
    }
}
