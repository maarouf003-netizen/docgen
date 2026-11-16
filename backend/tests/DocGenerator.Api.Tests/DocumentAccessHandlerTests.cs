using DocGenerator.Api.Authorization;
using DocGenerator.Api.Authorization.Documents;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-01a — مصفوفة قاعدة الوصول إلى المستند في معالج السياسة: الدور × العملية ×
/// شكل الملف (ملكية/فرع/شعبة/إحالة/متابعة). هي النقل المكافئ لقاعدة
/// <c>DocumentsController.CanAccessAsync</c> السابقة، مثبتة قبل حذفها من المتحكم.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DocumentAccessHandlerTests
{
    /// <summary>أشكال الملفات المرجعية في المصفوفة.</summary>
    public enum DocShape
    {
        /// <summary>الملف أنشأه الفاعل 7 في فرعه 3، بلا دائرة (Lawyer Owner).</summary>
        Owned,
        /// <summary>ملف محامٍ آخر في الفرع 3، بلا دائرة.</summary>
        OtherLawyerSameBranch,
        /// <summary>ملف فرع 3 بلا دائرة ولا شعبة (نطاق رئيس القسم).</summary>
        UnsectionedSameBranch,
        /// <summary>ملف دائرة شعبة 2 في الفرع 3 (شعبة رئيس الشعبة 2).</summary>
        SectionedSameBranch,
        /// <summary>ملف دائرة شعبة 3 في الفرع 3 (شعبة أخرى).</summary>
        OtherSectionSameBranch,
        /// <summary>ملف دائرة شعبة 2 في الفرع 4 (فرع آخر).</summary>
        SectionedOtherBranch,
    }

    public static TheoryData<UserRole, DocumentAction, DocShape, bool, bool, bool> Cells => new()
    {
        // محامٍ: ملكية فقط، وكل عمليات الملفات.
        { UserRole.Lawyer, DocumentAction.Read, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.Read, DocShape.OtherLawyerSameBranch, false, false, false },
        { UserRole.Lawyer, DocumentAction.ReadOrFollow, DocShape.OtherLawyerSameBranch, false, true, true },
        { UserRole.Lawyer, DocumentAction.ReadOrFollow, DocShape.OtherLawyerSameBranch, false, false, false },
        { UserRole.Lawyer, DocumentAction.Edit, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.ChangeStatus, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.Delete, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.Restore, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.Rotate, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.ManageActions, DocShape.Owned, false, false, true },
        { UserRole.Lawyer, DocumentAction.Rotate, DocShape.OtherLawyerSameBranch, false, false, false },
        // النقل بين المحامين ليس من صلاحية المحامي.
        { UserRole.Lawyer, DocumentAction.Transfer, DocShape.Owned, false, false, false },

        // رئيس القسم: غير المسنَد بالدائرة/الشعبة له، والمسنَد يحتاج إحالة سارية.
        { UserRole.Head, DocumentAction.Read, DocShape.UnsectionedSameBranch, false, false, true },
        { UserRole.Head, DocumentAction.Read, DocShape.SectionedSameBranch, false, false, false },
        { UserRole.Head, DocumentAction.Read, DocShape.SectionedSameBranch, true, false, true },
        { UserRole.Head, DocumentAction.Read, DocShape.SectionedOtherBranch, true, false, false },
        { UserRole.Head, DocumentAction.Edit, DocShape.UnsectionedSameBranch, false, false, false },
        { UserRole.Head, DocumentAction.ChangeStatus, DocShape.UnsectionedSameBranch, false, false, false },
        { UserRole.Head, DocumentAction.Delete, DocShape.UnsectionedSameBranch, false, false, false },
        { UserRole.Head, DocumentAction.Transfer, DocShape.UnsectionedSameBranch, false, false, true },

        // رئيس الشعبة: ملفات شعبته في فرعه فقط.
        { UserRole.SubHead, DocumentAction.Read, DocShape.SectionedSameBranch, false, false, true },
        { UserRole.SubHead, DocumentAction.Read, DocShape.OtherSectionSameBranch, false, false, false },
        { UserRole.SubHead, DocumentAction.Read, DocShape.SectionedOtherBranch, false, false, false },
        { UserRole.SubHead, DocumentAction.ChangeStatus, DocShape.SectionedSameBranch, false, false, false },
        { UserRole.SubHead, DocumentAction.Transfer, DocShape.SectionedSameBranch, false, false, true },

        // المدير/المشرف: قراءة كل الفروع، بلا كتابة أو نقل أو حذف.
        { UserRole.Manager, DocumentAction.Read, DocShape.SectionedOtherBranch, false, false, true },
        { UserRole.Manager, DocumentAction.Edit, DocShape.SectionedSameBranch, false, false, false },
        { UserRole.Manager, DocumentAction.Delete, DocShape.SectionedOtherBranch, false, false, false },
        { UserRole.Manager, DocumentAction.Transfer, DocShape.SectionedOtherBranch, false, false, false },
        { UserRole.Admin, DocumentAction.Read, DocShape.SectionedOtherBranch, false, false, true },
        { UserRole.Admin, DocumentAction.Edit, DocShape.UnsectionedSameBranch, false, false, false },

        // مندوب الجهة: لا وصول داخلي (البوابة تحرسه بنيويًا قبل هذا المعالج).
        { UserRole.EntityManager, DocumentAction.Read, DocShape.Owned, false, false, false },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task Requirement_IsSatisfiedOnlyForTheDecidedCell(
        UserRole role,
        DocumentAction action,
        DocShape shape,
        bool forwardedAppeal,
        bool assignedFollower,
        bool expected)
    {
        var handler = new DocumentAccessHandler(
            new UserCapabilities(),
            new FakeReader(forwardedAppeal, assignedFollower));
        var requirement = new DocumentAccessRequirement(action);
        var context = new AuthorizationHandlerContext(
            [requirement],
            UserCapabilitiesTests.Principal(role),
            new DocumentAccessContext(Document(shape), CancellationToken.None));

        await handler.HandleAsync(context);

        Assert.True(
            context.HasSucceeded == expected,
            $"{role} × {action} × {shape} (إحالة={forwardedAppeal}, متابعة={assignedFollower}): " +
            $"المتوقع {expected} والمعالج أعاد {context.HasSucceeded}.");
    }

    /// <summary>قيم مطابقة لأشكال المصفوفة؛ المعرّف 10 والفاعل 7.</summary>
    private static DocumentAccessInfo Document(DocShape shape) => shape switch
    {
        DocShape.Owned => new(10, 7, 3, null, null),
        DocShape.OtherLawyerSameBranch => new(10, 8, 3, null, null),
        DocShape.UnsectionedSameBranch => new(10, 8, 3, null, null),
        DocShape.SectionedSameBranch => new(10, 8, 3, 5, 2),
        DocShape.OtherSectionSameBranch => new(10, 8, 3, 6, 3),
        DocShape.SectionedOtherBranch => new(10, 8, 4, 5, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "شكل غير معرّف"),
    };

    private sealed class FakeReader(bool forwardedAppeal, bool assignedFollower) : IDocumentAccessReader
    {
        public Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default)
            => throw new NotSupportedException("المعالج يستقبل الإسقاط ولا يحلّ المورد بنفسه.");

        public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
            => Task.FromResult(forwardedAppeal);

        public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
            => Task.FromResult(assignedFollower);
    }
}
