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
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IHeadAlertService _alerts;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DocumentDelegationService(
        IDelegationRepository delegations,
        IDocumentRepository documents,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IHeadAlertService alerts,
        IHeadAlertOutbox alertOutbox,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _delegations = delegations;
        _documents = documents;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _alerts = alerts;
        _alertOutbox = alertOutbox;
        _clock = clock;
        _timeZone = timeZone;
    }
}
