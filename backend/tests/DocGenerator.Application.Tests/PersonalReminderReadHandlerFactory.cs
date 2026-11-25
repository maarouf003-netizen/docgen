using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Reminders;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>حزمة معالجات قراءات التذكيرات الشخصية (`P4-01cu`) على قاعدة اختبارية واحدة.</summary>
public sealed record PersonalReminderReadHandlers(
    IQueryHandler<ListPersonalRemindersQuery, List<PersonalReminderDto>> List,
    IQueryHandler<GetPersonalReminderQuery, PersonalReminderDto?> Get);

public static class PersonalReminderReadHandlerFactory
{
    public static PersonalReminderReadHandlers Create(DocGeneratorDbContext db)
        => new(
            new ListPersonalRemindersQueryHandler(
                new PersonalReminderRepository(db),
                Array.Empty<IPipelineBehavior<ListPersonalRemindersQuery, List<PersonalReminderDto>>>()),
            new GetPersonalReminderQueryHandler(
                new PersonalReminderRepository(db),
                Array.Empty<IPipelineBehavior<GetPersonalReminderQuery, PersonalReminderDto?>>()));
}
