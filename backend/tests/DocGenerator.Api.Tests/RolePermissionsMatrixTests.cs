using System.Reflection;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-02 — المصفوفة الذرّية لكتالوج الصلاحيات: <b>كل قدرة × كل دور</b>.
///
/// <para>
/// العمود المرجعي هنا هو <b>قرار المالك</b> لا سلوك الكود: الجدول مكتوب يدويًا في
/// <see cref="Rows"/> (الأدوار التي تحمل كل قدرة)، وأي تعديل في <see cref="RolePermissions"/>
/// بلا تحديث صريح للصفّ يفشل الخلية المعنية برسالة تسمّي القدرة والدور. هذا هو فرق
/// المصفوفة الذرّية عن اختبار انحدار عادي يقرأ التوقعات من الكود نفسه.
/// </para>
/// <para>
/// حارس الاكتمال (<see cref="Catalog_CoversEveryCapabilityExactlyOnce"/>) يمنع الصمت
/// المعاكس: قدرة جديدة تُضاف إلى الكتالوج بلا صفٍّ هنا تفشل الاختبار، فلا تصبح مصفوفة
/// «تغطية 100%» ناقصة بصمت. وحارس الأدوار (<see cref="DeclaredRoles_AreTheSixDecidedRoles"/>)
/// يمنع دورًا سابعًا من الدخول بقيمة افتراضية <c>false</c> بلا قرار.
/// </para>
/// <para>
/// القرارات المرجعية: مصفوفة الأدوار (§2.1/§2.21 من <c>SUBHEAD_PLAN.md</c>)،
/// <c>BQ-001</c>/<c>BQ-001د</c> (مساواة رؤية المدير/المشرف وإدارة المستخدمين)،
/// <c>BQ-002</c>/<c>BQ-005</c> (نطاق رئيس القسم والمندوب)، و§2.15 (إدارة الفروع
/// للمدير والمشرف).
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public sealed class RolePermissionsMatrixTests
{
    /// <summary>
    /// الأدوار الستة المقررة، بترتيب قيم <see cref="UserRole"/>. القائمة صريحة لا
    /// <c>Enum.GetValues</c> كي يفشل أي دور جديد حتى يُقرَّر عمودُه في المصفوفة.
    /// </summary>
    private static readonly UserRole[] DeclaredRoles =
    [
        UserRole.Lawyer,
        UserRole.Head,
        UserRole.Manager,
        UserRole.Admin,
        UserRole.EntityManager,
        UserRole.SubHead,
    ];

    /// <summary>
    /// جدول الحقيقة: لكل قدرة في <see cref="RolePermissions"/> الأدوار التي تحملها.
    /// الترتيب يتبع ترتيب الكتالوج نفسه لتسهيل المقارنة أثناء المراجعة.
    /// </summary>
    private static readonly CapabilityRow[] Rows =
    [
        // ── هوية الدور ──
        new(nameof(RolePermissions.IsHeadOrSubHead), UserRole.Head, UserRole.SubHead),
        new(nameof(RolePermissions.IsLawyer), UserRole.Lawyer),
        // هويات الدور (P2-01c): تصف الدور نفسه لا تمنح قدرة — انظر ثابت التماثل أدناه.
        new(nameof(RolePermissions.IsHead), UserRole.Head),
        new(nameof(RolePermissions.IsSubHead), UserRole.SubHead),
        new(nameof(RolePermissions.IsAdmin), UserRole.Admin),
        new(nameof(RolePermissions.IsEntityManager), UserRole.EntityManager),

        // ── ملفات: كتابة/حالة/حذف ──
        new(nameof(RolePermissions.CanEditDocuments), UserRole.Lawyer),
        new(nameof(RolePermissions.CanChangeDocumentStatus), UserRole.Lawyer),
        new(nameof(RolePermissions.CanDeleteDocuments), UserRole.Lawyer),
        // BQ-001: المدير يرى المحذوفات؛ ورئيس الشعبة يرث رئيس القسم.
        new(
            nameof(RolePermissions.CanViewDeletedDocuments),
            UserRole.Lawyer, UserRole.Head, UserRole.SubHead, UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.CanManageExecutionActions), UserRole.Lawyer),
        new(nameof(RolePermissions.CanRotate), UserRole.Lawyer),
        new(
            nameof(RolePermissions.CanViewPrintCounter),
            UserRole.Head, UserRole.SubHead, UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.HasFullAccess), UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.CanTransferDocuments), UserRole.Head, UserRole.SubHead),
        // BQ-002: رئيس القسم يدير محامي فرعه، والمشرف يدير الكل؛ المدير لم يُمنح هذا
        // الحق صراحةً (فتحاته المقررة: المحذوفات والاقتراحات فقط — RF-004).
        new(
            nameof(RolePermissions.CanManageBranchLawyers),
            UserRole.Head, UserRole.SubHead, UserRole.Admin),

        // ── حوكمة الحسابات والفروع ──
        // BQ-001د: المدير يدير المستخدمين عدا حسابات المشرف (الحد يُفرض في الخدمة).
        new(nameof(RolePermissions.CanManageUsers), UserRole.Manager, UserRole.Admin),
        // §2.15: توسيع مقصود — إدارة الفروع والشعب للمدير والمشرف معًا.
        new(nameof(RolePermissions.CanManageBranches), UserRole.Manager, UserRole.Admin),

        // ── تنبيهات ──
        new(nameof(RolePermissions.CanCreateAlerts), UserRole.Head, UserRole.SubHead),
        new(
            nameof(RolePermissions.CanReceiveAlerts),
            UserRole.Lawyer, UserRole.Head, UserRole.SubHead),
        new(nameof(RolePermissions.CanReadAllAlerts), UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.CanReadDelegationAlerts), UserRole.Lawyer),

        // ── إنابات ──
        new(nameof(RolePermissions.CanManageDelegations), UserRole.Lawyer),
        new(nameof(RolePermissions.CanApproveDelegations), UserRole.Head, UserRole.SubHead),

        // ── استئنافات ──
        new(nameof(RolePermissions.CanManageAppeals), UserRole.Lawyer),
        new(nameof(RolePermissions.CanAssignAppeals), UserRole.Head, UserRole.SubHead),

        // ── أعمدة إدارية وقراءة ──
        new(nameof(RolePermissions.CanSeeAdministrativeBranch), UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.CanCreateReviewLetters), UserRole.Lawyer),
        new(nameof(RolePermissions.CanReplyReviewLetters), UserRole.Head, UserRole.SubHead),
        // مندوب الجهة يكتب مراسلات البوابة فقط، عبر مساراتها المخصصة.
        new(
            nameof(RolePermissions.CanCreateCorrespondences),
            UserRole.Lawyer, UserRole.Head, UserRole.SubHead, UserRole.EntityManager),
        new(
            nameof(RolePermissions.CanSeeAssignedLawyer),
            UserRole.Head, UserRole.SubHead, UserRole.Manager, UserRole.Admin),
        new(
            nameof(RolePermissions.CanSearchByLawyer),
            UserRole.Head, UserRole.SubHead, UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.IsReadOnlyOnDocuments), UserRole.Manager, UserRole.Admin),

        // ── سجل الجهات والبوابة ──
        // د3/د5: إدارة السجل للرؤساء (مقصورًا على محافظتهم عند التنفيذ) وللمدير/المشرف.
        new(
            nameof(RolePermissions.CanManageEntityRegistry),
            UserRole.Head, UserRole.SubHead, UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.CanUseDelegatePortal), UserRole.EntityManager),
        // د11: إدارة مندوبي الجهات للرؤساء وللمدير/المشرف.
        new(
            nameof(RolePermissions.CanManageDelegates),
            UserRole.Head, UserRole.SubHead, UserRole.Manager, UserRole.Admin),
        new(nameof(RolePermissions.CanMergeEntities), UserRole.Manager, UserRole.Admin),

        // ── تذكيرات واقتراحات ──
        new(nameof(RolePermissions.CanManagePersonalReminders), UserRole.Lawyer),
        new(nameof(RolePermissions.CanViewStrikeOffReminders), UserRole.Lawyer),
        new(nameof(RolePermissions.CanViewAppSuggestions), UserRole.Manager, UserRole.Admin),
        new(
            nameof(RolePermissions.CanSuggestApp),
            UserRole.Lawyer, UserRole.Head, UserRole.SubHead),

        // ── دوائر التنفيذ ──
        new(nameof(RolePermissions.CanManageExecutionCircuits), UserRole.Head, UserRole.SubHead),
    ];

    /// <summary>كل خلية كحالة اختبار مستقلة: 42 قدرة × 6 أدوار = 252 خلية.</summary>
    public static TheoryData<UserRole, string, bool> Cells
    {
        get
        {
            var data = new TheoryData<UserRole, string, bool>();
            foreach (var row in Rows)
            {
                foreach (var role in DeclaredRoles)
                {
                    data.Add(role, row.Name, row.Holds(role));
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Capability_MatchesDecidedMatrix(UserRole role, string capability, bool expected)
    {
        var actual = Invoke(capability, role);

        Assert.True(
            actual == expected,
            $"{capability} × {role}: المتوقع {expected} والكتالوج المركزي أعاد {actual}. " +
            "إما انحراف في RolePermissions عن القرار، أو صفٌّ قديم في هذه المصفوفة — " +
            "لا تُحدَّث التوقعات لتطابق الكود بلا قرار مالك صريح.");
    }

    /// <summary>
    /// حارس الاكتمال في الاتجاهين: لا قدرة في الكتالوج بلا صفّ، ولا صفّ بلا قدرة.
    /// بهذا لا تصير «التغطية 100%» ادعاءً ناقصًا عند إضافة <c>Can*</c> جديدة.
    /// </summary>
    [Fact]
    public void Catalog_CoversEveryCapabilityExactlyOnce()
    {
        var discovered = typeof(RolePermissions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var declared = Rows
            .Select(row => row.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            discovered.SequenceEqual(declared, StringComparer.Ordinal),
            "مصفوفة RolePermissions لا تطابق الكتالوج المركزي. " +
            $"في الكتالوج وحدها: [{string.Join(", ", discovered.Except(declared, StringComparer.Ordinal))}] — " +
            $"في المصفوفة وحدها: [{string.Join(", ", declared.Except(discovered, StringComparer.Ordinal))}] — " +
            $"مكرر في المصفوفة: [{string.Join(", ", declared.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key))}].");
    }

    /// <summary>
    /// الشكل الموحّد الذي تفترضه الخلايا: <c>public static bool M(UserRole)</c>.
    /// أي توقيع آخر (قدرة موردية تحتاج فاعلًا/موردًا) لا يصح قياسه هنا ويجب أن
    /// يُفشل الحارس صراحةً بدل أن يُتجاهل بصمت.
    /// </summary>
    [Fact]
    public void Catalog_HasSingleUniformShape()
    {
        var methods = typeof(RolePermissions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var deviations = new List<string>();
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (method.ReturnType != typeof(bool)
                || parameters.Length != 1
                || parameters[0].ParameterType != typeof(UserRole))
            {
                deviations.Add($"{method.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.Name))}) : {method.ReturnType.Name}");
            }
        }

        Assert.True(
            deviations.Count == 0,
            "قدرات في RolePermissions بتوقيع غير bool(UserRole) — وسّع المصفوفة بقرار قبل إضافتها:" +
            Environment.NewLine + string.Join(Environment.NewLine, deviations));
    }

    /// <summary>دور جديد في التعداد يُفشل الاختبار حتى يُقرَّر عموده صراحةً.</summary>
    [Fact]
    public void DeclaredRoles_AreTheSixDecidedRoles()
    {
        Assert.Equal(
            Enum.GetValues<UserRole>().OrderBy(role => (int)role).ToArray(),
            DeclaredRoles.OrderBy(role => (int)role).ToArray());
    }

    /// <summary>
    /// قرار §2.1: رئيس الشعبة يملك صلاحيات رئيس القسم نفسها (لا أكثر ولا أقل)،
    /// والمجموعة السلبية رفضٌ صريح في الجدول لكلٍّ منهما (المدير/المشرف فقط).
    /// <para>
    /// تُستثنى **هويات الدور** (<see cref="RolePermissions.IsHead"/> و
    /// <see cref="RolePermissions.IsSubHead"/>) لأنها تصف الدور نفسه ولا تمنح قدرة؛
    /// كل صفوف القدرات — بلا استثناء — تبقى مشمولة بالثابت.
    /// </para>
    /// </summary>
    [Fact]
    public void SubHead_HoldsExactlyTheHeadCapabilitySet()
    {
        var identityPredicates = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RolePermissions.IsHead),
            nameof(RolePermissions.IsSubHead),
        };

        var deviations = Rows
            .Where(row => !identityPredicates.Contains(row.Name))
            .Where(row => row.Holds(UserRole.SubHead) != row.Holds(UserRole.Head))
            .Select(row => $"{row.Name}: Head={row.Holds(UserRole.Head)} SubHead={row.Holds(UserRole.SubHead)}")
            .ToArray();

        Assert.True(
            deviations.Length == 0,
            "انحراف عن قرار §2.1 (رئيس الشعبة = رئيس القسم):" +
            Environment.NewLine + string.Join(Environment.NewLine, deviations));
    }

    /// <summary>
    /// البوابة الخارجية: مندوب الجهة يحمل سطح البوابة وحده
    /// (<see cref="RolePermissions.CanUseDelegatePortal"/> + كتابة مراسلاته)،
    /// وأي قدرة أخرى تفتح له مسارًا داخليًا = ثغرة سلطة.
    /// <para>
    /// تُستثنى هوية الدور <see cref="RolePermissions.IsEntityManager"/> (تصف الدور نفسه).
    /// </para>
    /// </summary>
    [Fact]
    public void EntityManager_HoldsOnlyThePortalSurface()
    {
        var held = Rows
            .Where(row => row.Name != nameof(RolePermissions.IsEntityManager))
            .Where(row => row.Holds(UserRole.EntityManager))
            .Select(row => row.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            nameof(RolePermissions.CanCreateCorrespondences),
            nameof(RolePermissions.CanUseDelegatePortal),
        };

        Assert.True(
            held.SequenceEqual(expected, StringComparer.Ordinal),
            $"سطح مندوب الجهة يجب أن يكون [{string.Join(", ", expected)}] " +
            $"والمصفوفة تمنحه [{string.Join(", ", held)}].");
    }

    private static bool Invoke(string capability, UserRole role)
    {
        var method = typeof(RolePermissions).GetMethod(
            capability, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"قدرة غير موجودة في RolePermissions: {capability} — المصفوفة تسمّي قدرة محذوفة.");

        return (bool)method.Invoke(null, [role])!;
    }

    /// <summary>صفٌّ في جدول الحقيقة: اسم القدرة والأدوار التي تحملها.</summary>
    private sealed class CapabilityRow
    {
        private readonly UserRole[] _holders;

        public CapabilityRow(string name, params UserRole[] holders)
        {
            Name = name;
            _holders = holders;
        }

        public string Name { get; }

        public bool Holds(UserRole role) => Array.IndexOf(_holders, role) >= 0;
    }
}
