using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>
/// قائمة المستخدمين الكاملة — إدارة المستخدمين (مشرف/مدير). تُمرَّر رتبة الفاعل
/// لتصفية حسابات المشرف من منظور المدير (`BQ-001د`) في القراءة كما في الكتابة.
/// </summary>
public sealed record ListUsersQuery(UserRole? CallerRole = null) : IQuery<List<UserListItemDto>>;

internal sealed class ListUsersQueryHandler : QueryHandlerBase<ListUsersQuery, List<UserListItemDto>>
{
    private readonly IUserRepository _users;

    public ListUsersQueryHandler(
        IUserRepository users,
        IEnumerable<IPipelineBehavior<ListUsersQuery, List<UserListItemDto>>> behaviors)
        : base(behaviors)
        => _users = users;

    protected override async Task<List<UserListItemDto>> HandleCoreAsync(
        ListUsersQuery query, CancellationToken ct)
    {
        var users = await _users.ListAllUsersAsync(ct);
        // حد المشرف (`BQ-001د`): المدير لا يرى حسابات المشرف إطلاقًا — وإلا استطاع
        // معرفة معرّفاتها ومحاولة المساس بها (الممنوعة كتابةً)، وخرجت القراءة عن
        // تعليق المتحكم نفسه. المشرف يرى الجميع، وغير الإداريين لا يصلون هنا أصلًا.
        if (query.CallerRole == UserRole.Manager)
            users = users.Where(u => u.Role != UserRole.Admin).ToList();
        return users.Select(UserProjection.ToUserDto).ToList();
    }
}
