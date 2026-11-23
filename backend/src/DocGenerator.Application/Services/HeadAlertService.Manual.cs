using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class HeadAlertService : IHeadAlertService
{
    public async Task<HeadAlertSummaryDto> CreateAsync(
        CreateHeadAlertRequest request,
        HeadAlertActor actor,
        string? actorName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // الدور قرار خدمة (D8): المتحكم لم يعد يفحصه.
        if (!RolePermissions.CanCreateAlerts(actor.Role))
            throw new UnauthorizedAccessException("الإصدار اليدوي مخصص لرئيس قسم/شعبة");

        // D11: الكتابة بلا فرع ⇒ 400 (عقد `POST` القائم) لا 403.
        var branchId = RequireBranchForWrite(actor);

        // تحقّق القاعدة يبقى دفاعًا ثانيًا: حساب معطّل، أو صار دوره غير مخوّل، أو رمز
        // بفرع متقادم — يُرفض 400 كما كان قبل نقل الصلاحية (لا تغيير عقد).
        var user = await _users.GetByIdAsync(actor.UserId, ct);
        if (user is null || !user.IsActive
            || !RolePermissions.CanCreateAlerts(user.Role)
            || user.BranchId != branchId)
            throw new ArgumentException("الإصدار اليدوي مخصص لرئيس قسم/شعبة مفعّل في فرعه");

        var rawMessage = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("نص التنبيه مطلوب");

        // الحدّ على نص المستخدم قبل البادئة، ثم سقف الأمان على النص النهائي.
        var withinLimit = HeadAlertTextLimits.EnsureWithinManualLimit(rawMessage);
        var message = HeadAlertTextLimits.EnsureWithinSafety(
            HeadAlertMessages.ManualMessage(user.Role, withinLimit));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب");

        var branch = await _branches.GetByIdAsync(branchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var targetType = ParseManualTargetType(request.TargetType);
        var recipients = await ResolveRecipientsAsync(targetType, request, branchId, ct);
        if (recipients.Count == 0)
            throw new ArgumentException("لا يوجد مستلمون للتنبيه");

        var now = DateTime.UtcNow;
        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = actor.UserId,
            TargetType = targetType,
            Kind = ManualKindFor(targetType),
            DedupeKey = null,
            DocumentId = targetType == HeadAlertTargetType.Document ? request.DocumentId : null,
            TargetLawyerId = targetType == HeadAlertTargetType.Lawyer ? request.TargetLawyerId : null,
            Message = message,
            CreatedAt = now,
            LastContentChangedAt = now,
            Recipients = recipients.Select(u => new HeadAlertRecipient { UserId = u.Id }).ToList(),
        };

        // جسم 201 (D3) يجب أن يطابق صفَّ القائمة لاحقًا حقلًا بحقل، ولذلك يُعاد قراءته
        // بالإسقاط الواحد نفسه الذي تبنيه القوائم (لا تحويل الكيان)، **داخل المعاملة**
        // كي لا يبقى صفٌّ مثبَّت يعقبه 500 إن فشل الجلب (فيتكرر الإنشاء من العميل).
        var created = await _tx.RunAsync(async token =>
        {
            await _alerts.AddAsync(alert, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_alert",
                details: $"أصدر تنبيهاً يدوياً في فرع {branch.Name} بنطاق {targetType}: "
                    + HeadAlertTextLimits.PreviewForAudit(message),
                ct: token);

            return await _alerts.GetSummaryAsync(
                    alert.Id, recipientUserId: null, includeCounters: true, token)
                ?? throw new InvalidOperationException("تعذّر جلب ملخص التنبيه بعد إنشائه");
        }, ct);

        return created;
    }

    public async Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default)
    {
        var removed = 0;
        await _tx.RunAsync(async token =>
        {
            removed = await _alerts.RemoveAppealRecipientAsync(appealId, userId, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return removed;
    }

    private async Task<List<User>> ResolveRecipientsAsync(
        HeadAlertTargetType targetType,
        CreateHeadAlertRequest request,
        int branchId,
        CancellationToken ct)
    {
        switch (targetType)
        {
            case HeadAlertTargetType.Document:
                {
                    if (request.DocumentId is null)
                        throw new ArgumentException("يجب تحديد الملف المرتبط بالتنبيه");

                    var doc = await _documents.GetByIdAsync(request.DocumentId.Value, ct);
                    // رسالة واحدة للملف غير الموجود وللملف خارج الفرع (§5، `P3-5`):
                    // لا يُكشف وجود ملف فرع آخر عبر اختلاف النص.
                    if (doc is null || doc.BranchId != branchId)
                        throw new ArgumentException("الملف غير متاح");
                    if (doc.CreatedBy is null || doc.CreatedBy.Role != UserRole.Lawyer || !doc.CreatedBy.IsActive)
                        throw new ArgumentException("لا يوجد محامٍ مختص نشط لهذا الملف");

                    return new List<User> { doc.CreatedBy };
                }
            case HeadAlertTargetType.Lawyer:
                {
                    if (request.TargetLawyerId is null)
                        throw new ArgumentException("يجب تحديد المحامي المستلم");

                    var lawyer = await _users.GetByIdAsync(request.TargetLawyerId.Value, ct);
                    if (lawyer is null || lawyer.Role != UserRole.Lawyer
                        || lawyer.BranchId != branchId || !lawyer.IsActive)
                        throw new ArgumentException("المحامي غير موجود ضمن فرعك");

                    return new List<User> { lawyer };
                }
            case HeadAlertTargetType.Branch:
                return await _alerts.ListActiveLawyersAsync(branchId, ct);
            default:
                throw new ArgumentException("نوع التنبيه غير صالح");
        }
    }

    /// <summary>
    /// تحليل صريح لنطاق الإصدار اليدوي — لا <c>Enum.TryParse</c> (يرفض الأرقام)
    /// ولا النطاق النظامي <c>head</c> (لا يملك العميل دورة حياته).
    /// </summary>
    private static HeadAlertTargetType ParseManualTargetType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "document" => HeadAlertTargetType.Document,
        "lawyer" => HeadAlertTargetType.Lawyer,
        "branch" => HeadAlertTargetType.Branch,
        _ => throw new ArgumentException("نوع التنبيه غير صالح"),
    };

    private static string ManualKindFor(HeadAlertTargetType targetType) => targetType switch
    {
        HeadAlertTargetType.Document => HeadAlertKindCatalog.ManualToDocument,
        HeadAlertTargetType.Lawyer => HeadAlertKindCatalog.ManualToLawyer,
        HeadAlertTargetType.Branch => HeadAlertKindCatalog.ManualToBranch,
        _ => throw new ArgumentException("نوع التنبيه غير صالح"),
    };

    /// <summary>
    /// الفرع إلزامي للكتابة، لكن عقده <c>400</c> لا <c>403</c> (D11): طلب كتابة بحساب
    /// ناقص فرعًا خطأ إدخال في الطلب لا رفض صلاحية — ويُترجم في المتحكم إلى <c>400</c>.
    /// </summary>
    private static int RequireBranchForWrite(HeadAlertActor actor) =>
        actor.BranchId ?? throw new ArgumentException("رئيس القسم دون فرع لا يمكنه إصدار تنبيهات");
}
