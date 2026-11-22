using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class HeadAlertService : IHeadAlertService
{
    public async Task<PagedResult<HeadAlertSummaryDto>> ListForActorAsync(
        HeadAlertActor actor, HeadAlertListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);

        // التفويض قبل التحقق من الشكل (عقد ما قبل توحيد الصلاحيات): دور غير مخوّل أو
        // رئيس بلا فرع يُرفض 403 حتى مع ترقيم غير صالح، و«غير المقروء» للمدير/المشرف
        // 400 برسالته الخاصة قبل أي تحقق آخر — لا انعكاس 403/400 غير مقصود.
        if (!actor.IsManagerOrAdmin && !actor.IsLawyer && !actor.IsHeadOrSubHead)
            throw new UnauthorizedAccessException("الدور غير مخوّل لقوائم التنبيهات");

        if (actor.IsManagerOrAdmin && query.UnreadOnly)
            throw new ArgumentException("خيار «غير المقروء» غير متاح للمدير/المشرف");

        var branchId = actor.IsHeadOrSubHead ? RequireBranch(actor) : (int?)null;

        query.Page.Validate();
        // شكل المؤشر يُفحص بعد الدور/الفرع أيضًا (403 قبل 400)، ويُحلَّل مرة واحدة.
        var cursor = AlertCursor.ParseOptional(query.Cursor);

        if (actor.IsManagerOrAdmin)
        {
            var page = await _alerts.ListSummariesForOversightAsync(
                query.Archive,
                query.DelegationId,
                cursor,
                query.Page.Skip,
                query.Page.PageSize,
                ct);
            return ToPagedResult(page, query.Page, cursor);
        }

        if (actor.IsLawyer)
        {
            var page = await _alerts.ListSummariesForRecipientAsync(
                actor.UserId,
                branchId: null,
                query.Archive,
                query.UnreadOnly,
                query.DelegationId,
                cursor,
                query.Page.Skip,
                query.Page.PageSize,
                includeCounters: false,
                ct);
            return ToPagedResult(page, query.Page, cursor);
        }

        var headPage = await _alerts.ListSummariesForRecipientAsync(
            actor.UserId,
            branchId,
            query.Archive,
            query.UnreadOnly,
            query.DelegationId,
            cursor,
            query.Page.Skip,
            query.Page.PageSize,
            includeCounters: true,
            ct);
        return ToPagedResult(headPage, query.Page, cursor);
    }

    public async Task<PagedResult<HeadAlertSummaryDto>> ListSentAsync(
        HeadAlertActor actor,
        AlertArchiveFilter archive,
        AlertPageRequest page,
        string? cursor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(page);

        // الدور والفرع قبل التحقق من الشكل: مدير/غير رئيس ⇒ 403، ورئيس بلا فرع ⇒ 403
        // (عقد ما قبل التوحيد)، ثم الترقيم/المؤشر غير الصالح ⇒ 400.
        if (!actor.IsHeadOrSubHead)
            throw new UnauthorizedAccessException("قائمة الصادر مخصصة لرئيس القسم/الشعبة");

        var branchId = RequireBranch(actor);

        page.Validate();
        var parsedCursor = AlertCursor.ParseOptional(cursor);

        var result = await _alerts.ListSummariesSentAsync(
            actor.UserId, branchId, archive, parsedCursor, page.Skip, page.PageSize, ct);
        return ToPagedResult(result, page, parsedCursor);
    }

    public async Task<PagedResult<HeadAlertSummaryDto>> ListByDelegationAsync(
        int delegationId, HeadAlertActor actor, AlertPageRequest page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(page);

        // الدور ثم الطرفية — قرار الخدمة وحده (D8/`ح‑1`): المستودع لا يقيّد النتيجة
        // بالمستخدم، فلو بقي الفحص في المتحكم لكان أي متصل آخر يقرأ تنبيهات إنابةٍ
        // لا تخصّه. الترتيب: دور (403) ← ترقيم (400) ← طرفية (403)، فالدور غير المخوّل
        // لا يتلقى 400 عن شكل طلب قبل رفض صلاحيته، ويبقى عقد التحقق من الترقيم محفوظًا.
        if (!RolePermissions.CanReadDelegationAlerts(actor.Role))
            throw new UnauthorizedAccessException("تنبيهات الإنابة متاحة لطرفها المحامي فقط");

        page.Validate();

        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct);
        if (delegation is null || !DelegationParties.IsParty(delegation, actor.UserId))
            throw new UnauthorizedAccessException("لست طرفًا في هذه الإنابة");

        var result = await _alerts.ListSummariesByDelegationAsync(
            delegationId, actor.UserId, page.Skip, page.PageSize, ct);
        return ToPagedResult(result, page);
    }

    public async Task<HeadAlertDto?> GetDetailsAsync(
        int id, HeadAlertActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // صف واحد مسقط في SQL — لا «اجلب القائمة وابحث فيها» ولا كيانات محمَّلة (§9.2).
        var row = await _alerts.GetDetailRowAsync(id, actor.UserId, ct);
        if (row is null)
            return null;

        var isCreator = row.CreatedById == actor.UserId;
        var isOversight = actor.IsManagerOrAdmin;

        // المستلم أولًا بأولوية مطلقة (بلا شرط فرع — إنابات خارجية)، ثم المنشئ،
        // ثم الإشراف؛ غير ذلك 404 بلا كشف وجود (D2/§12).
        if (!row.IsRecipient && !isCreator && !isOversight)
            return null;

        return ToDetailDto(row, canViewRecipients: isOversight || isCreator);
    }

    public async Task<List<HeadAlertRecipientDto>?> GetRecipientsAsync(
        int id, HeadAlertActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null)
            return null;

        // منشئ أو مدير/مشرف فقط؛ أي دور آخر 404 بلا تسريب وجود (§9.3/§12).
        if (alert.CreatedById != actor.UserId && !actor.IsManagerOrAdmin)
            return null;

        var recipients = await _alerts.ListRecipientsWithUserAsync(id, ct);
        return recipients.Select(r => new HeadAlertRecipientDto(
            r.Id,
            r.UserId,
            r.User?.FullName ?? string.Empty,
            r.User?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            r.IsRead,
            r.ReadAt)).ToList();
    }

    public async Task<int> CountUnreadAsync(
        HeadAlertActor actor, AlertUnreadScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // مفتاح صريح مغلق (مراجعة المرحلة 4 `ح‑22`): قيمة enum غير معروفة من مستدعٍ
        // داخلي تُرفض 400 بدل السقوط الصامت إلى النطاق الشخصي.
        return scope switch
        {
            AlertUnreadScope.User => await CountUserScopeAsync(actor, ct),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope), scope, "نطاق عدّاد غير معروف — استخدم user"),
        };
    }

    /// <summary>
    /// `scope=user` (الافتراضي، `D7`): صفوف الفاعل نفسه؛ المحامي بلا قيد فرع،
    /// والرئيس/الشعبة داخل فرعه كي يطابق العدّاد قائمته.
    /// </summary>
    private async Task<int> CountUserScopeAsync(HeadAlertActor actor, CancellationToken ct)
    {
        if (!RolePermissions.CanReceiveAlerts(actor.Role))
            throw new UnauthorizedAccessException("عدّاد غير المقروء مخصص للمستلمين (محامٍ أو رئيس نطاق)");

        var branchId = actor.IsHeadOrSubHead ? RequireBranch(actor) : (int?)null;
        return await _alerts.CountUnreadForUserAsync(actor.UserId, branchId, ct);
    }

    /// <summary>
    /// يبني العقد القائم <see cref="PagedResult{T}"/> بلا حقول جديدة. عند طلب
    /// المؤشر (<paramref name="cursor"/> غير <c>null</c>) لا معنى لرقم الصفحة
    /// المزاح، فيُعاد <c>page = 1</c> صراحةً بدل صدق رقم لا يصف النافذة.
    /// </summary>
    private static PagedResult<HeadAlertSummaryDto> ToPagedResult(
        HeadAlertSummaryPage page, AlertPageRequest request, AlertCursor? cursor = null) => new()
        {
            Items = page.Items.ToList(),
            Page = cursor is null ? request.Page : 1,
            PerPage = request.PageSize,
            TotalCount = page.TotalCount,
        };

    /// <summary>الفرع إلزامي لرئيس القسم/الشعبة — وإلا فالحساب غير صالح للاستخدام.</summary>
    private static int RequireBranch(HeadAlertActor actor) =>
        actor.BranchId ?? throw new UnauthorizedAccessException("حساب رئيس القسم/الشعبة غير مرتبط بفرع");

    /// <summary>
    /// تحويل صف التفاصيل المسقط إلى العقد الكامل (الخطة §9.2): النص الكامل هنا وحده،
    /// و<c>IsRead</c> حالة القارئ نفسه (أو <c>null</c> لغير المستلم)، و<c>CanViewRecipients</c>
    /// قرار خادم — لا يملك العميل تعيينه.
    /// </summary>
    private static HeadAlertDto ToDetailDto(HeadAlertDetailRow row, bool canViewRecipients) => new(
        row.Id,
        row.Kind,
        row.Message,
        row.TargetType.ToString().ToLowerInvariant(),
        row.DocumentId,
        HeadAlertTitle.Build(
            row.DocumentId,
            row.DocumentExists,
            row.BorrowerName,
            row.BorrowerFather,
            row.BorrowerFamily,
            row.DocumentType),
        row.TargetLawyerId,
        row.TargetLawyerName,
        row.DelegationId,
        row.AppealId,
        row.ReviewLetterId,
        row.PublicEntityId,
        row.ExecutionCircuitId,
        row.CreatedAt,
        row.LastContentChangedAt,
        row.IsArchived,
        row.ArchivedAt,
        row.CreatedByName,
        row.IsRead,
        canViewRecipients);
}
