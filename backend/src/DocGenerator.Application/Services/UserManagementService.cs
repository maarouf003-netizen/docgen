using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Users;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة حسابات المحامين والمستخدمين (P4-01bg/P4-01bh) — يستهلكها
/// `CreateLawyer`/`UpdateLawyer`/`CreateUser`/`UpdateUser` فقط. قراءات القوائم
/// نُقلت إلى معالجات استعلام في `UseCases/Users/` (`P4-01cs`).
/// </summary>
public sealed partial class UserManagementService : IUserManagementLawyerWriteRules, IUserManagementUserWriteRules
{
    private static readonly UserRole[] BranchRoles = { UserRole.Lawyer, UserRole.Head, UserRole.SubHead };

    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IRepository<Correspondence> _correspondences;
    private readonly IRepository<CorrespondenceMessage> _messages;
    private readonly IDocumentRepository _documents;
    private readonly IHeadAlertSuccessionService _alertSuccession;
    private readonly IUnitOfWork _uow;

    public UserManagementService(
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IRepository<HeadSuccession> successions,
        IRepository<Correspondence> correspondences,
        IRepository<CorrespondenceMessage> messages,
        IDocumentRepository documents,
        IHeadAlertSuccessionService alertSuccession,
        IUnitOfWork uow)
    {
        _users = users;
        _branches = branches;
        _sections = sections;
        _successions = successions;
        _correspondences = correspondences;
        _messages = messages;
        _documents = documents;
        _alertSuccession = alertSuccession;
        _uow = uow;
    }

    // ── تنفيذ عقد مساعدات كتابة المحامين المشتركة (P4-01bg) ───────────────

    string IUserManagementLawyerWriteRules.NormalizeUsername(string username)
        => NormalizeUsername(username);

    void IUserManagementLawyerWriteRules.ValidateUsername(string username)
        => ValidateUsername(username);

    string IUserManagementLawyerWriteRules.NormalizePassword(string? password, string? username)
        => NormalizePassword(password, username);

    string IUserManagementLawyerWriteRules.DuplicateUsernameMessage(int? branchId)
        => DuplicateUsernameMessage(branchId);

    LawyerListItemDto IUserManagementLawyerWriteRules.ToLawyerDto(User user)
        => UserProjection.ToLawyerDto(user);

    // ── تنفيذ عقد مساعدات كتابة المستخدمين المشتركة (P4-01bh) ─────────────

    string IUserManagementUserWriteRules.NormalizeUsername(string username)
        => NormalizeUsername(username);

    void IUserManagementUserWriteRules.ValidateUsername(string username)
        => ValidateUsername(username);

    string IUserManagementUserWriteRules.NormalizePassword(string? password, string? username)
        => NormalizePassword(password, username);

    string IUserManagementUserWriteRules.DuplicateUsernameMessage(int? branchId)
        => DuplicateUsernameMessage(branchId);

    UserRole IUserManagementUserWriteRules.ParseRole(string? role)
        => ParseRole(role);

    Task IUserManagementUserWriteRules.GuardManagerAdminBoundaryAsync(
        int? actorUserId, bool requestedIsAdmin, bool targetIsAdmin, CancellationToken ct)
        => GuardManagerAdminBoundaryAsync(actorUserId, requestedIsAdmin, targetIsAdmin, ct);

    Task<int?> IUserManagementUserWriteRules.ResolveBranchAsync(int? branchId, UserRole role, CancellationToken ct)
        => ResolveBranchAsync(branchId, role, ct);

    Task<int?> IUserManagementUserWriteRules.ResolveSectionAsync(
        UserRole role, int? branchId, int? sectionId, CancellationToken ct)
        => ResolveSectionAsync(role, branchId, sectionId, ct);

    Task IUserManagementUserWriteRules.GuardSingleActiveHeadAsync(
        UserRole role, int? branchId, int? sectionId, int? excludeUserId, CancellationToken ct)
        => GuardSingleActiveHeadAsync(role, branchId, sectionId, excludeUserId, ct);

    Task<User> IUserManagementUserWriteRules.ResolveSuccessorAsync(User user, int successorId, CancellationToken ct)
        => ResolveSuccessorAsync(user, successorId, ct);

    Task IUserManagementUserWriteRules.ApplySuccessionAsync(
        User predecessor, User successor, string? actorName, CancellationToken ct)
        => ApplySuccessionAsync(predecessor, successor, actorName, ct);

    Task<UserListItemDto> IUserManagementUserWriteRules.ToUserDtoAsync(User user, CancellationToken ct)
        => ToUserDtoAsync(user, ct);
}
