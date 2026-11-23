using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ReviewLetterService : IReviewLetterService
{
    public async Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, int page, int perPage,
        string? administrativeBranch, CancellationToken ct = default, int? actorSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        // المدير/المشرف لا يرى أي كتاب قبل اختيار فرع الإدارة (حجب تام على مستوى الخدمة).
        if (role is UserRole.Manager or UserRole.Admin
            && string.IsNullOrWhiteSpace(administrativeBranch))
        {
            return new PagedResult<ReviewLetterListItemDto>
            {
                Page = page,
                PerPage = perPage,
            };
        }

        var (items, totalCount) = role switch
        {
            UserRole.Lawyer => await _letters.SearchForLawyerAsync(actorUserId, q, page, perPage, ct),
            // نطاق المالك (§10): القسم لدوائر القسم وبلا دائرة، والشعبة لدوائر شعبته.
            UserRole.Head when actorBranchId is not null
                => await _letters.SearchForScopeAsync(actorBranchId.Value, null, q, page, perPage, ct),
            UserRole.Head
                => throw new UnauthorizedAccessException("رئيس القسم دون فرع لا يمكنه عرض كتب المطالعة"),
            UserRole.SubHead when actorBranchId is not null && actorSectionId is not null
                => await _letters.SearchForScopeAsync(actorBranchId.Value, actorSectionId, q, page, perPage, ct),
            UserRole.SubHead
                => throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(administrativeBranch, q, page, perPage, ct),
            _ => throw new UnauthorizedAccessException("الدور غير مخوّل لعرض كتب المطالعة"),
        };

        // حالة الإطلاع (هل طالع محامي الكتاب الرد؟) معلومة خاصة بمحامي الكتاب نفسه،
        // فلا تُكشف لرئيس القسم ولا للإدارة حفاظًا على خصوصية المحامي.
        var revealUnseen = role == UserRole.Lawyer;

        return new PagedResult<ReviewLetterListItemDto>
        {
            Items = items.Select(l => ToListItem(l, revealUnseen)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
        };
    }

    public async Task<ReviewLetterDto> GetByIdAsync(int id, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(id, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, actorSectionId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على كتاب المطالعة هذا");

        return ToDto(letter);
    }

    public Task<int> CountUnseenRepliesForLawyerAsync(int actorUserId, CancellationToken ct = default)
        => _letters.CountUnseenReplyLettersForLawyerAsync(actorUserId, ct);

    public Task<int> CountPendingForHeadAsync(int branchId, int? ownerSectionId = null, CancellationToken ct = default)
        => _letters.CountPendingForScopeAsync(branchId, ownerSectionId, ct);

    public Task<List<string>> GetAdministrativeBranchesAsync(CancellationToken ct = default)
        => _letters.GetAdministrativeBranchesAsync(ct);

    public async Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(int documentId,
        int actorUserId, UserRole role, int? actorBranchId, CancellationToken ct = default,
        int? actorSectionId = null)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException("الملف غير موجود");

        var isOwnerOrSupervisor = role is UserRole.Manager or UserRole.Admin
            || document.CreatedById == actorUserId;

        if (!isOwnerOrSupervisor &&
            !await FollowsDocumentAsync(documentId, actorUserId, ct) &&
            !await InFileScopeAsync(document, role, actorBranchId, actorSectionId, ct))
        {
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على كتب هذا الملف");
        }

        var letters = await _letters.ListByDocumentAsync(documentId, ct);
        return letters
            .Select(l => ToListItem(
                l,
                revealUnseen: role == UserRole.Lawyer && l.CreatedById == actorUserId))
            .ToList();
    }

    private async Task<bool> CanViewAsync(ReviewLetter letter, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Lawyer:
                // محامي الكتاب، أو أي محامٍ يتابع الملف المربوط (إحالة/إنابة/استئناف).
                if (letter.CreatedById == actorUserId)
                    return true;
                return letter.DocumentId is not null
                    && await FollowsDocumentAsync(letter.DocumentId.Value, actorUserId, ct);

            case UserRole.Head:
                if (letter.BranchId != actorBranchId)
                    return false;
                return await OwnerSectionOfAsync(letter, ct) is null;

            case UserRole.SubHead:
                if (letter.BranchId != actorBranchId || actorSectionId is null)
                    return false;
                return await OwnerSectionOfAsync(letter, ct) == actorSectionId;

            case UserRole.Manager or UserRole.Admin:
                return true;

            default:
                return false;
        }
    }
}
