using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// تنفيذ خدمة الإنابات التنفيذية: دورة حياة الإنابة من التسطير حتى الإتمام بالبيع وإعادة الملف.
/// كل كتابة ضمن معاملة واحدة مع سجل التدقيق، وتحقق صارم من الصلاحيات والحالات المسموحة.
/// </summary>
public sealed partial class DocumentDelegationService : IDocumentDelegationService
{
    private readonly IDelegationRepository _delegations;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IHeadAlertService _alerts;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IDocumentNumberRegistry? _numberRegistry;

    public DocumentDelegationService(
        IDelegationRepository delegations,
        IDbExceptionClassifier dbErrors,
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<DocumentRegistrationDate> registrationDates,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IHeadAlertService alerts,
        IHeadAlertOutbox alertOutbox,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IRepository<ExecutionCircuit>? circuits = null,
        IDocumentNumberRegistry? numberRegistry = null)
    {
        _delegations = delegations;
        _dbErrors = dbErrors;
        _documents = documents;
        _users = users;
        _branches = branches;
        _registrationDates = registrationDates;
        _occurrences = occurrences;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _alerts = alerts;
        _alertOutbox = alertOutbox;
        _clock = clock;
        _timeZone = timeZone;
        _circuits = circuits ?? new UnconfiguredCircuitRepository();
        _numberRegistry = numberRegistry;
    }
}
