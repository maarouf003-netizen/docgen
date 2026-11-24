using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Appeals;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// بناء معالجَي كتابة الاستئناف في الاختبارات فوق سياق واحد — يمنع تكرار الاعتماديات
/// ويُبقي الاستدعاءات القديمة عبر امتدادات التوقيع.
/// </summary>
public static class AppealWriteHandlerFactory
{
    public static CreateDocumentAppealCommandHandler Create(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        return new CreateDocumentAppealCommandHandler(
            new DocumentRepository(db),
            new AppealRepository(db),
            new UserRepository(db),
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<CreateDocumentAppealCommand, AppealDto>>());
    }

    public static UpdateDocumentAppealCommandHandler Update(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        return new UpdateDocumentAppealCommandHandler(
            new AppealRepository(db),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<UpdateDocumentAppealCommand, AppealDto?>>());
    }

    public static AssignDocumentAppealCommandHandler Assign(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        return new AssignDocumentAppealCommandHandler(
            new AppealRepository(db),
            new UserRepository(db),
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<AssignDocumentAppealCommand, AppealDto?>>());
    }

    public static TransferDocumentAppealCommandHandler Transfer(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        return new TransferDocumentAppealCommandHandler(
            new AppealRepository(db),
            new UserRepository(db),
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<TransferDocumentAppealCommand, AppealDto?>>());
    }

    public static TransferAllDocumentAppealsCommandHandler TransferAll(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        return new TransferAllDocumentAppealsCommandHandler(
            new AppealRepository(db),
            new UserRepository(db),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            Array.Empty<IPipelineBehavior<TransferAllDocumentAppealsCommand, int>>());
    }
}
