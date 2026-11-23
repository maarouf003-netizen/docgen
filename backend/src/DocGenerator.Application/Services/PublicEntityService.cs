using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة السجل المرجعي للجهات العامة (نموذج الحوكمة الجديد): أي جهة يُدخلها
/// محامٍ تُخزَّن بـ Status=Final لكنها تبقى «بحاجة مراجعة» (NeedsReview=true) فلا
/// تظهر لبوات المندوبين قبل اعتماد/تعديل رئيس قسمها (المواءمة السلوكية §6bis)؛
/// الاعتماد يقفل المراجعة بصمت، والتعديل — وتغيير التسمية تحديدًا — يبلّغ
/// المُدخِل بالاسم القديم والجديد. الإدارة تعدّل كل السجل بتنفيذ فوري.
/// إعادة التسمية الجماعية تزامن الأعمدة النصية ضمن معاملة واحدة (د5)، وأداة
/// الاستيراد التاريخي تعتمد نهائيًا مباشرة (د12).
/// </summary>
public sealed partial class PublicEntityService : IPublicEntityService
{
    /// <summary>الاسم الافتراضي لفرع قيد الجهة الأم — من الكتالوج لا من حرفي مكرر.</summary>
    private const string DefaultBranchName = PublicEntityBranchCatalog.ParentBranchName;

    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IHeadAlertRepository _headAlerts;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IRepository<PublicEntityChangeEvent> _changeEvents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<ParentEditSuggestion> _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IUserRepository _users;
    private readonly IAppealRepository _appeals;
    private readonly IIdempotencyStore? _idempotency;

    public PublicEntityService(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IHeadAlertRepository headAlerts,
        IHeadAlertOutbox alertOutbox,
        IRepository<PublicEntityChangeEvent> changeEvents,
        IRepository<DocumentOccurrence> occurrences,
        IRepository<ParentEditSuggestion> suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IUserRepository users,
        IAppealRepository appeals,
        IIdempotencyStore? idempotency = null)
    {
        _entities = entities;
        _branches = branches;
        _headAlerts = headAlerts;
        _alertOutbox = alertOutbox;
        _changeEvents = changeEvents;
        _occurrences = occurrences;
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _users = users;
        _appeals = appeals;
        _idempotency = idempotency;
    }
}
