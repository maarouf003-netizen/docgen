using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class HeadAlertService : IHeadAlertService
{
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
