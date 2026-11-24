using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// بناء معالجَي كتابة الإنابة في الاختبارات فوق سياق واحد — يمنع تكرار قائمة
/// الاعتماديات في كل ملف ويُبقي الاستدعاءات القديمة عبر امتدادات التوقيع.
/// </summary>
public static class DelegationWriteHandlerFactory
{
    public static CreateDocumentDelegationCommandHandler Create(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var branches = new Repository<Branch>(db);
        var delegations = new DelegationRepository(db);
        return new CreateDocumentDelegationCommandHandler(
            documents,
            delegations,
            new DelegationReservationRepository(db),
            users,
            branches,
            new Repository<ExecutionCircuit>(db),
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            new DbExceptionClassifier(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<CreateDocumentDelegationCommand, DelegationDto>>());
    }

    public static UpdateDocumentDelegationCommandHandler Update(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var branches = new Repository<Branch>(db);
        var delegations = new DelegationRepository(db);
        return new UpdateDocumentDelegationCommandHandler(
            documents,
            delegations,
            new DelegationReservationRepository(db),
            users,
            branches,
            new Repository<ExecutionCircuit>(db),
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            new DbExceptionClassifier(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<UpdateDocumentDelegationCommand, DelegationDto?>>());
    }

    public static AssignDocumentDelegationCommandHandler Assign(
        DocGeneratorDbContext db, FakeAuditLogger? audit = null)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var branches = new Repository<Branch>(db);
        var delegations = new DelegationRepository(db);
        return new AssignDocumentDelegationCommandHandler(
            delegations,
            users,
            branches,
            new Repository<ExecutionCircuit>(db),
            documents,
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            audit ?? new FakeAuditLogger(),
            new DbExceptionClassifier(),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<AssignDocumentDelegationCommand, DelegationDto?>>());
    }
}
