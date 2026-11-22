using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IUserManagementService
{
    Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default);
    Task<List<LawyerListItemDto>> ListLawyersAsync(
        int? branchId,
        string? mode,
        int callerUserId,
        int? callerSectionId,
        bool callerIsHeadOrSubHead,
        bool includeInactive,
        CancellationToken ct = default);
    Task<LawyerListItemDto> CreateLawyerAsync(int branchId, CreateLawyerRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null);
    Task<LawyerListItemDto?> UpdateLawyerAsync(int userId, UpdateLawyerRequest request, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<bool> SetLawyerActiveAsync(int userId, bool isActive, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<List<UserListItemDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserListItemDto> CreateUserAsync(CreateUserRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null);
    Task<UserListItemDto?> UpdateUserAsync(int userId, UpdateUserRequest request, int actorUserId, string? actorName, CancellationToken ct = default);
    Task<List<HeadSuccessionDto>> ListSuccessionAsync(int? branchId, CancellationToken ct = default);
}

/// <summary>
/// إدارة محامي الفرع (رئيس القسم/مشرف) وإدارة المستخدمين الكاملة (مشرف).
/// التحقق من الصلاحية/النطاق في المتحكم، والتحقق المنطقي والكتابة هنا.
/// </summary>
public sealed partial class UserManagementService : IUserManagementService
{
    private static readonly UserRole[] BranchRoles = { UserRole.Lawyer, UserRole.Head, UserRole.SubHead };

    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IRepository<Correspondence> _correspondences;
    private readonly IRepository<CorrespondenceMessage> _messages;
    private readonly IDocumentRepository _documents;
    private readonly IHeadAlertSuccessionService _alertSuccession;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UserManagementService(
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IRepository<ExecutionCircuit> circuits,
        IRepository<HeadSuccession> successions,
        IRepository<Correspondence> correspondences,
        IRepository<CorrespondenceMessage> messages,
        IDocumentRepository documents,
        IHeadAlertSuccessionService alertSuccession,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _users = users;
        _branches = branches;
        _sections = sections;
        _circuits = circuits;
        _successions = successions;
        _correspondences = correspondences;
        _messages = messages;
        _documents = documents;
        _alertSuccession = alertSuccession;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }
}
