using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Alerts;
using DocGenerator.Application.UseCases.Appeals;
using DocGenerator.Application.UseCases.Branches;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Application.UseCases.Documents;
using DocGenerator.Application.UseCases.ExecutionActions;
using DocGenerator.Application.UseCases.ExecutionCircuits;
using DocGenerator.Application.UseCases.Forum;
using DocGenerator.Application.UseCases.Interventions;
using DocGenerator.Application.UseCases.Reminders;
using DocGenerator.Application.UseCases.ReviewLetters;
using DocGenerator.Application.UseCases.Sections;
using DocGenerator.Application.UseCases.Suggestions;
using DocGenerator.Application.UseCases.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // ساعة النظام المحقونة في كل خدمات الطلبات: قرارات السنة والطوابع الزمنية الموحدة
        // تأخذ منها. TimeProvider.System آمن للمشاركة بين النطاقات (قراءات سلاسل فقط).
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        // P1-02b: مصدر العشوائية المحقون — يمكن استبداله كليًا في الاختبارات/البيئات.
        services.AddSingleton<IEntropySource>(SystemEntropySource.Instance);
        // منطقة زمن النظام من الإعدادات (TimeZone:Id — IANA أو Windows) — قرار السنة يعتمدها.
        var timeZoneId = configuration?["TimeZone:Id"];
        services.AddSingleton(ServerClock.ResolveTimeZone(timeZoneId));
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDocumentService, DocumentService>();
        // P2-01a: بوابة القراءة الضيقة لسياسات الوصول إلى المستند (إسقاط F-01 + سؤالا
        // الاستئناف) — يستهلكها معالج السياسة في Api بدل استدعاء المستودعات مباشرة.
        services.AddScoped<IDocumentAccessReader, DocumentAccessReader>();
        // لا مفتاح إعداد للإنتاج: السلوك الانتقالي للاختبارات يُحقن مباشرة في مصنع الاختبار.
        services.AddSingleton(new NumberRegistryOptions());
        services.AddScoped<IDocumentNumberRegistry, DocumentNumberRegistry>();
        services.AddScoped<IExecutionActionService, ExecutionActionService>();
        services.AddScoped<IDocumentSearchService, DocumentSearchService>();
        services.AddScoped<IDocumentStatusService, DocumentStatusService>();
        services.AddScoped<IDocumentWriteGuard, DocumentWriteGuard>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBranchManagementService, BranchManagementService>();
        services.AddScoped<IHeadAlertService, HeadAlertService>();
        services.AddScoped<IHeadAlertSuccessionService, HeadAlertSuccessionService>();
        services.AddScoped<IHeadAlertOutbox, HeadAlertOutbox>();
        services.AddScoped<IPersonalReminderService, PersonalReminderService>();
        services.AddScoped<IAppSuggestionService, AppSuggestionService>();
        services.AddScoped<IPublicEntityService, PublicEntityService>();
        services.AddScoped<IPortalService, PortalService>();
        services.AddScoped<IEntityDelegateService, EntityDelegateService>();
        services.AddScoped<IReviewLetterService, ReviewLetterService>();
        services.AddScoped<ICorrespondenceService, CorrespondenceService>();
        services.AddScoped<IForumService, ForumService>();
        services.AddScoped<IForumRetentionService, ForumRetentionService>();
        services.AddScoped<IDocumentDelegationService, DocumentDelegationService>();
        services.AddScoped<IDocumentAppealService, DocumentAppealService>();
        services.AddScoped<IExecutionCircuitService, ExecutionCircuitService>();
        services.AddScoped<IStrikeOffReminderService, StrikeOffReminderService>();
        services.AddScoped<ISectionService, SectionService>();
        services.AddScoped<IDocumentContextBuilder, DocumentContextBuilder>();
        services.AddScoped<IDocumentRenderer, WordTemplateRenderer>();
        services.AddScoped<IWordDocumentGenerator, WordDocumentGenerator>();
        services.AddScoped<IExcelExportService, ExcelExportService>();

        // P4-01a: خط أوامر/استعلامات ضيق — السلوكيات المسجّلة بالترتيب (الأول خارجي).
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddScoped<IQueryHandler<GetDocumentQuery, DocumentResponse?>, GetDocumentQueryHandler>();
        services.AddScoped<ICommandHandler<CreateDocumentCommand, DocumentResponse>, CreateDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDocumentCommand, DocumentResponse?>, UpdateDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteDocumentCommand, bool>, DeleteDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<TransferDocumentCommand, DocumentResponse>, TransferDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<TransferAllDocumentsCommand, int>, TransferAllDocumentsCommandHandler>();
        services.AddScoped<ICommandHandler<SaveDocumentBaseNumbersCommand, bool>, SaveDocumentBaseNumbersCommandHandler>();
        services.AddScoped<ICommandHandler<CorrectDocumentBaseNumberCommand, bool>, CorrectDocumentBaseNumberCommandHandler>();
        services.AddScoped<ICommandHandler<UndoDocumentRotationCommand, bool>, UndoDocumentRotationCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteDocumentDelegationCommand, bool>, DeleteDocumentDelegationCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteDocumentAppealCommand, bool>, DeleteDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<CreateDocumentDelegationCommand, DelegationDto>, CreateDocumentDelegationCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDocumentDelegationCommand, DelegationDto?>, UpdateDocumentDelegationCommandHandler>();
        services.AddScoped<ICommandHandler<CreateDocumentAppealCommand, AppealDto>, CreateDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDocumentAppealCommand, AppealDto?>, UpdateDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateAppealRegistrationCommand, AppealDto?>, UpdateAppealRegistrationCommandHandler>();
        services.AddScoped<ICommandHandler<AssignDocumentAppealCommand, AppealDto?>, AssignDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<TransferDocumentAppealCommand, AppealDto?>, TransferDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<TransferAllDocumentAppealsCommand, int>, TransferAllDocumentAppealsCommandHandler>();
        services.AddScoped<ICommandHandler<DecideDocumentAppealCommand, AppealDto?>, DecideDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<StrikeDocumentAppealCommand, AppealDto?>, StrikeDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<ForwardDocumentAppealCommand, AppealDto?>, ForwardDocumentAppealCommandHandler>();
        services.AddScoped<ICommandHandler<RecallAppealForwardCommand, AppealDto?>, RecallAppealForwardCommandHandler>();
        services.AddScoped<ICommandHandler<AddAppealActionCommand, AppealActionDto>, AddAppealActionCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateAppealActionCommand, AppealActionDto?>, UpdateAppealActionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteAppealActionCommand, bool>, DeleteAppealActionCommandHandler>();
        services.AddScoped<ICommandHandler<SetAppealReminderCommand, AppealActionDto>, SetAppealReminderCommandHandler>();
        services.AddScoped<ICommandHandler<RescheduleAppealReminderCommand, AppealActionDto>, RescheduleAppealReminderCommandHandler>();
        services.AddScoped<ICommandHandler<ClearAppealReminderCommand, bool>, ClearAppealReminderCommandHandler>();
        services.AddScoped<ICommandHandler<SaveAppealBaseNumbersCommand, bool>, SaveAppealBaseNumbersCommandHandler>();
        services.AddScoped<ICommandHandler<CorrectAppealBaseNumberCommand, bool>, CorrectAppealBaseNumberCommandHandler>();
        services.AddScoped<ICommandHandler<DismissStrikeOffReminderCommand, bool>, DismissStrikeOffReminderCommandHandler>();
        services.AddScoped<ICommandHandler<MarkReviewRepliesSeenCommand, bool>, MarkReviewRepliesSeenCommandHandler>();
        services.AddScoped<ICommandHandler<SetLawyerActiveCommand, bool>, SetLawyerActiveCommandHandler>();
        services.AddScoped<ICommandHandler<MarkHeadAlertReadCommand, bool>, MarkHeadAlertReadCommandHandler>();
        services.AddScoped<ICommandHandler<MarkAppSuggestionReadCommand, bool>, MarkAppSuggestionReadCommandHandler>();
        services.AddScoped<ICommandHandler<DeletePersonalReminderCommand, bool>, DeletePersonalReminderCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteBranchCommand, bool>, DeleteBranchCommandHandler>();
        services.AddScoped<ICommandHandler<CreateAppSuggestionCommand, AppSuggestionDto>, CreateAppSuggestionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteForumMessageCommand, bool>, DeleteForumMessageCommandHandler>();
        services.AddScoped<ICommandHandler<ToggleForumPinCommand, ForumMessageDto>, ToggleForumPinCommandHandler>();
        services.AddScoped<ICommandHandler<PostForumMessageCommand, ForumMessageDto>, PostForumMessageCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateForumMessageCommand, ForumMessageDto>, UpdateForumMessageCommandHandler>();
        services.AddScoped<ICommandHandler<MarkForumReadCommand, int>, MarkForumReadCommandHandler>();
        services.AddScoped<ICommandHandler<SetPersonalReminderOccurrenceCommand, PersonalReminderDto?>, SetPersonalReminderOccurrenceCommandHandler>();
        services.AddScoped<ICommandHandler<CreateBranchCommand, BranchDto>, CreateBranchCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteExecutionActionCommand, bool>, DeleteExecutionActionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteDocumentInterventionCommand, bool>, DeleteDocumentInterventionCommandHandler>();
        services.AddScoped<ICommandHandler<AddDocumentInterventionCommand, DocumentInterventionDto>, AddDocumentInterventionCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDocumentInterventionCommand, DocumentInterventionDto?>, UpdateDocumentInterventionCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateBranchCommand, BranchDto?>, UpdateBranchCommandHandler>();
        services.AddScoped<ICommandHandler<CreatePersonalReminderCommand, PersonalReminderDto>, CreatePersonalReminderCommandHandler>();
        services.AddScoped<ICommandHandler<UpdatePersonalReminderCommand, PersonalReminderDto?>, UpdatePersonalReminderCommandHandler>();
        services.AddScoped<ICommandHandler<CreateSectionCommand, SectionDto>, CreateSectionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteSectionCommand, bool>, DeleteSectionCommandHandler>();
        services.AddScoped<ICommandHandler<RenameSectionCommand, SectionDto?>, RenameSectionCommandHandler>();
        services.AddScoped<ICommandHandler<SetSectionActiveCommand, SectionDto?>, SetSectionActiveCommandHandler>();
        services.AddScoped<ICommandHandler<AddExecutionActionCommand, ExecutionActionDto>, AddExecutionActionCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateExecutionActionCommand, ExecutionActionDto?>, UpdateExecutionActionCommandHandler>();
        services.AddScoped<ICommandHandler<ClearActionReminderCommand, bool>, ClearActionReminderCommandHandler>();
        services.AddScoped<ICommandHandler<SetActionReminderCommand, ExecutionActionDto>, SetActionReminderCommandHandler>();
        services.AddScoped<ICommandHandler<RescheduleActionReminderCommand, ExecutionActionDto>, RescheduleActionReminderCommandHandler>();
        services.AddScoped<ICommandHandler<RestoreDocumentCommand, bool>, RestoreDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<CreateExecutionCircuitCommand, ExecutionCircuitDto>, CreateExecutionCircuitCommandHandler>();
        services.AddScoped<ICommandHandler<SetExecutionCircuitActiveCommand, ExecutionCircuitDto>, SetExecutionCircuitActiveCommandHandler>();
        services.AddScoped<ICommandHandler<RenameExecutionCircuitCommand, ExecutionCircuitDto>, RenameExecutionCircuitCommandHandler>();
        services.AddScoped<ICommandHandler<CompletePendingRegistrationsCommand, int>, CompletePendingRegistrationsCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteExecutionCircuitCommand, bool>, DeleteExecutionCircuitCommandHandler>();
        services.AddScoped<ICommandHandler<TransferExecutionCircuitCommand, ExecutionCircuitDto>, TransferExecutionCircuitCommandHandler>();
        services.AddScoped<ICommandHandler<ReferCircuitFilesCommand, ReferCircuitFilesResult>, ReferCircuitFilesCommandHandler>();
        return services;
    }
}
