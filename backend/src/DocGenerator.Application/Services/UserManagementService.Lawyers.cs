using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class UserManagementService : IUserManagementService
{
    public async Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default)
        => await ListLawyersAsync(branchId, "branch", 0, null, false, true, ct);

    /// <summary>
    /// قائمتا المحامين (§5.4 — قرار §2.9): `branch` (الافتراضي للنوافذ: كل محامي
    /// الفرع — تسريب الأسماء موثق ومقبول) و`mine` (الافتراضي: له ملف في دوائري
    /// أو أنا أنشأته أو قديم بلا منشئ — القدامى يُرون دائمًا). المدير/المشرف
    /// خارج القاعدتين (بلا دوائر): قائمة الفرع الكاملة. `includeInactive`
    /// افتراضيًا صحيح حفظًا للسلوك القائم (إعادة التفعيل تحتاجه) — يُعلن هنا.
    /// </summary>
    public async Task<List<LawyerListItemDto>> ListLawyersAsync(
        int? branchId,
        string? mode,
        int callerUserId,
        int? callerSectionId,
        bool callerIsHeadOrSubHead,
        bool includeInactive,
        CancellationToken ct = default)
    {
        var normalizedMode = (mode ?? "mine").Trim().ToLowerInvariant();
        if (normalizedMode is not ("mine" or "branch"))
            throw new ArgumentException("وضع غير صالح — mine أو branch");

        if (!callerIsHeadOrSubHead || normalizedMode == "branch")
            return await BranchLawyerListAsync(branchId, includeInactive, ct);

        var circuits = await _circuits.ListAsync(ct);
        var myCircuitIds = circuits
            .Where(c => branchId == null || c.BranchId == branchId)
            .Where(c => c.SectionId == callerSectionId)
            .Select(c => c.Id)
            .ToList();
        var ownerIds = await _documents.ListOwnerIdsByCircuitIdsAsync(myCircuitIds, ct);
        var inBranch = await BranchLawyerListAsync(branchId, true, ct);
        // القدامى بلا منشئ ضمن نطاق الفرع (قرار §2.10 — والنطاق الفرعي للendpoint
        // ثابت: `Head_ListsLawyers_OnlyOwnBranch`).
        var veterans = inBranch.Where(l => l.CreatedById == null);
        return inBranch
            .Concat(veterans)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .Where(l => includeInactive || l.IsActive)
            .Where(l => ownerIds.Contains(l.Id) || l.CreatedById == callerUserId || l.CreatedById == null)
            .OrderBy(l => l.FullName)
            .ToList();
    }

    private async Task<List<LawyerListItemDto>> BranchLawyerListAsync(int? branchId, bool includeInactive, CancellationToken ct)
    {
        var lawyers = await _users.ListLawyersAsync(branchId, ct);
        return lawyers
            .Where(l => includeInactive || l.IsActive)
            .OrderBy(l => l.FullName)
            .Select(ToLawyerDto)
            .ToList();
    }

    public async Task<LawyerListItemDto> CreateLawyerAsync(int branchId, CreateLawyerRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null)
    {
        var username = NormalizeUsername(request.Username);
        ValidateUsername(username);
        var candidatePassword = NormalizePassword(request.Password, username);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("الاسم الكامل مطلوب");

        if (await _users.UsernameExistsAsync(username, branchId, null, ct))
            throw new ArgumentException(DuplicateUsernameMessage(branchId));

        var branch = await _branches.GetByIdAsync(branchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var user = new User
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            IsActive = true,
            // منشئ المحامي (قرار §2.10): مباشَرة محامي الصفر ملفات — القدامى `null`.
            CreatedById = actorUserId,
            PasswordHash = _hasher.Hash(candidatePassword),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_user", details: $"أنشأ محامياً: {user.FullName} ({user.Username}) في فرع {branch.Name}", ct: token);
        }, ct);

        return new LawyerListItemDto(user.Id, user.Username, user.FullName, user.IsActive, user.BranchId, branch.Name, null, user.Version);
    }

    public async Task<LawyerListItemDto?> UpdateLawyerAsync(int userId, UpdateLawyerRequest request, int? scopeBranchId, string? actorName, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.Role != UserRole.Lawyer)
            return null;

        // رئيس القسم يعدّل محامي فرعه فقط.
        if (scopeBranchId.HasValue && user.BranchId != scopeBranchId)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(user, request.Version, "حساب المحامي");

        if (string.IsNullOrWhiteSpace(request.FullName) && string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("لا يوجد تغيير لإجرائه — حدّد اسماً جديداً أو كلمة مرور جديدة");

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            // الاسم الثلاثي هو اسم الدخول: تعديل الاسم يحدّث اسم الدخول تلقائياً مع بقاء التفرد ضمن الفرع.
            var newUsername = NormalizeUsername(request.FullName);
            ValidateUsername(newUsername);
            if (newUsername != user.Username
                && await _users.UsernameExistsAsync(newUsername, user.BranchId, user.Id, ct))
                throw new ArgumentException(DuplicateUsernameMessage(user.BranchId));

            // (R2) اسم الدخول جزء من هوية التوكن (Name/UniqueName): تغيّره يُبطل الجلسات كالدور/الفرع.
            if (newUsername != user.Username)
                user.TokenVersion++;
            user.FullName = request.FullName.Trim();
            user.Username = newUsername;
        }

        user.UpdatedAt = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var candidatePassword = NormalizePassword(request.Password, user.Username);
                user.PasswordHash = _hasher.Hash(candidatePassword);
                // إبطال الرموز الصادرة سابقاً عند تغيير كلمة المرور.
                user.TokenVersion++;
            }

            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_user",
                details: $"عدّل المحامي: {user.FullName} ({user.Username})", ct: token);
            return ToLawyerDto(user);
        }, ct);
    }
}
