using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class CorrespondenceService : ICorrespondenceService
{
    public async Task<List<CorrespondenceTargetDto>> SearchTargetsAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int? documentId,
        CancellationToken ct = default, int? actorSectionId = null)
    {
        if (!CanWrite(role))
            throw new UnauthorizedAccessException("الدور غير مخوّل لتسطير المراسلات");

        Document? document = null;
        if (documentId is not null)
        {
            document = await _documents.GetByIdAsync(documentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            // بوابة البحث = صلاحية التسطير على هذا الملف (نفس فحص الإنشاء) فلا نكشف
            // مرشحين لمن لا يملك الكتابة عليه مهما كان دوره.
            if (!await MayAttachAsync(document, actorUserId, role, actorBranchId, actorSectionId, ct))
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مراسلة على هذا الملف");
        }

        return await ResolveEligibleTargetsAsync(actorUserId, role, q, document, ct);
    }

    /// <summary>
    /// مصدر الحقيقة الوحيد لأهلية المستلمين: عامة بلا ملف = البحث الحر المعتاد؛
    /// مربوطة بملف = مندوب←محامو الملف، محامٍ/رئيس←مناديب نطاق الملف.
    /// يُستخدم من البحث والإنشاء معًا فلا يمكن أن يُترشح مستلمٌ ثم يُرفض إنشاؤه أو العكس.
    /// </summary>
    /// <summary>
    /// تجميد المستلم الرئيس (§10.2 + قرار 24): المرتبطة بملف لا تصل لرئيس أبدًا
    /// (مثبت بالاختبارات — الرئيس يستلم عبر نطاق دائرته لا بالتسمية)؛ وبلا ملف
    /// تُجمَّد الشعبة المختارة (فارغةٌ تعني رئيس القسم): الشعبة نشطة بفرع المستلم
    /// ومستلمُها رئيسُها المفعّل، والافتراضي رئيس قسم فرع المنشئ — وإلا
    /// «يجب اختيار المستلم».
    /// </summary>
    private async Task<int?> ResolveHeadRecipientSectionAsync(
        int? recipientSectionId, User target, Document? document, UserRole role,
        int? actorBranchId, CancellationToken ct)
    {
        if (document is not null)
            return null;
        if (recipientSectionId is null)
        {
            // الافتراضي رئيس القسم (حسب الاعتماد): رئيس قسم فرع المنشئ المفعّل حصرًا.
            if (target.Role != UserRole.Head || target.BranchId != actorBranchId || !target.IsActive)
            {
                // مستلم الشعبة المسمّى بالاسم: تُشتق شعبته من حسابه (قرار 24) —
                // شعبة نشطة ورئيسها المفعّل هو المستلم نفسه، وإلا «يجب اختيار المستلم».
                if (target.Role != UserRole.SubHead || !target.IsActive || target.SectionId is null)
                    throw new ArgumentException("يجب اختيار المستلم — حدّد رئيس القسم أو شعبة نشطة برئيس مفعّل");
                var ownSection = await _sections.GetByIdAsync(target.SectionId.Value, ct);
                var ownHead = ownSection is not null
                    ? await _users.FindActiveHeadAsync(UserRole.SubHead, ownSection.BranchId, ownSection.Id, ct)
                    : null;
                if (ownSection is null || !ownSection.IsActive || ownHead?.Id != target.Id)
                    throw new ArgumentException("يجب اختيار المستلم — حدّد رئيس القسم أو شعبة نشطة برئيس مفعّل");
                return ownSection.Id;
            }
            return null;
        }
        var section = await _sections.GetByIdAsync(recipientSectionId.Value, ct);
        var head = section is not null
            ? await _users.FindActiveHeadAsync(UserRole.SubHead, section.BranchId, section.Id, ct)
            : null;
        if (section is null || !section.IsActive || head?.Id != target.Id)
            throw new ArgumentException("يجب اختيار المستلم — حدّد رئيس القسم أو شعبة نشطة برئيس مفعّل");
        return section.Id;
    }

    private async Task<List<CorrespondenceTargetDto>> ResolveEligibleTargetsAsync(
        int actorUserId, UserRole role, string? q, Document? document, CancellationToken ct)
    {
        if (document is null)
        {
            var users = await _users.SearchCorrespondenceTargetsAsync(actorUserId, q, TargetsLimit, ct);
            return users.Select(ToTargetDto).ToList();
        }

        if (role == UserRole.EntityManager)
        {
            var lawyers = await _users.SearchDocumentLawyerTargetsAsync(
                document.Id, document.CreatedById, actorUserId, q, TargetsLimit, ct);
            return lawyers.Select(ToTargetDto).ToList();
        }

        // محامٍ/رئيس قسم: مناديب نطاق الملف فقط — لا رؤساء ولا محامين آخرين.
        var scope = await _portal.GetDocumentScopeKeysAsync(document.Id, ct);
        var delegates = await _users.SearchScopeDelegateTargetsAsync(
            scope.EntryIds, scope.GroupIds, actorUserId, q, TargetsLimit, ct);
        return delegates.Select(ToTargetDto).ToList();
    }

    /// <summary>هل المستلم المؤهل فعلًا لهذه المراسلة؟ (العامة دائمًا نعم؛ المربوطة حسب الدور).</summary>
    private async Task<bool> IsEligibleTargetAsync(
        Document document, UserRole role, int targetUserId, CancellationToken ct)
    {
        if (role == UserRole.EntityManager)
            return await _users.IsDocumentLawyerTargetAsync(
                document.Id, document.CreatedById, targetUserId, ct);

        var scope = await _portal.GetDocumentScopeKeysAsync(document.Id, ct);
        return await _users.IsScopeDelegateTargetAsync(
            scope.EntryIds, scope.GroupIds, targetUserId, ct);
    }

    private static CorrespondenceTargetDto ToTargetDto(User u) => new(
        u.Id,
        u.FullName,
        u.Role.ToString().ToLowerInvariant(),
        u.Branch?.Name,
        u.Branch?.Governorate ?? u.PortalEntry?.Governorate);
}
