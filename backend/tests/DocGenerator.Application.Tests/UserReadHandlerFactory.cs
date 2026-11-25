using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Users;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حزمة معالجات قراءات الهيكل التنظيمي (`P4-01cs`) على قاعدة اختبارية واحدة:
/// المحامون، والمستخدمون، وسجل التعاقب — بدل تكرار ربط السلوكيات الفارغة.
/// </summary>
public sealed record UserReadHandlers(
    IQueryHandler<ListLawyersQuery, List<LawyerListItemDto>> Lawyers,
    IQueryHandler<ListUsersQuery, List<UserListItemDto>> Users,
    IQueryHandler<ListSuccessionQuery, List<HeadSuccessionDto>> Succession);

public static class UserReadHandlerFactory
{
    public static UserReadHandlers Create(DocGeneratorDbContext db)
        => new(
            new ListLawyersQueryHandler(
                new UserRepository(db),
                new Repository<ExecutionCircuit>(db),
                new DocumentRepository(db),
                Array.Empty<IPipelineBehavior<ListLawyersQuery, List<LawyerListItemDto>>>()),
            new ListUsersQueryHandler(
                new UserRepository(db),
                Array.Empty<IPipelineBehavior<ListUsersQuery, List<UserListItemDto>>>()),
            new ListSuccessionQueryHandler(
                new Repository<HeadSuccession>(db),
                new Repository<Branch>(db),
                new Repository<Section>(db),
                new UserRepository(db),
                Array.Empty<IPipelineBehavior<ListSuccessionQuery, List<HeadSuccessionDto>>>()));
}
