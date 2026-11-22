using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// تنفيذ خدمة الاستئنافات: دورة حياة الاستئناف من التسطير حتى الحسم أو الشطب،
/// مع الإسناد والنقل والتدوير والإجراءات المستقلة. كل كتابة ضمن معاملة واحدة مع
/// سجل التدقيق وتسجيل التنبيهات في صندوق الصادر — فشل التسجيل يُرجع المعاملة كاملة.
/// </summary>
public sealed partial class DocumentAppealService : IDocumentAppealService
{
    private readonly IAppealRepository _appeals;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IHeadAlertService _alerts;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDbExceptionClassifier? _dbErrors;

    public DocumentAppealService(
        IAppealRepository appeals,
        IDocumentRepository documents,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IHeadAlertService alerts,
        IHeadAlertOutbox alertOutbox,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDbExceptionClassifier? dbErrors = null)
    {
        _appeals = appeals;
        _documents = documents;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _alerts = alerts;
        _alertOutbox = alertOutbox;
        _clock = clock;
        _timeZone = timeZone;
        _dbErrors = dbErrors;
    }
}
