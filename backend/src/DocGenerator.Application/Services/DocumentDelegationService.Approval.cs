using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService : IDocumentDelegationService
{
    public async Task<DelegationDto?> AssignAsync(
        int delegationId,
        AssignDelegationRequest request,
        int userId,
        int? headBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن اعتماد إنابة لم تعد معلّقة");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("لا يمكن اعتماد إنابة مرفوضة — بانتظار تصحيح المحامي");
        if (request.Version.HasValue && delegation.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا");

        var source = delegation.SourceDocument;
        if (source.NeedsRegistration)
            throw new ArgumentException("لا يمكن اعتماد إنابة على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");

        var caller = await DocumentDelegationRules.LoadHeadCallerAsync(userId, headBranchId, _users, ct);
        var callerBranch = caller.BranchId ?? headBranchId
            ?? throw new ArgumentException("الرئيس دون فرع لا يمكنه اعتماد الإنابات");

        var isExternal = delegation.IsExternal;
        var externalBranchId = delegation.ExternalBranchId;

        // نطاق الاعتماد (§7.1 — قلب القاعدة): الاعتماد لمالك الدائرة المنابة لا المنيب.
        var (approvalBranchId, approvalSectionId) =
            await DocumentDelegationRules.ResolveApprovalScopeAsync(delegation, _circuits, ct);
        DocumentDelegationRules.EnsureApprover(caller, callerBranch, approvalBranchId, approvalSectionId);

        ExecutionCircuit? approvalCircuit = null;
        if (!isExternal && delegation.DelegatedCircuitId is not null)
            approvalCircuit = delegation.DelegatedCircuit
                ?? await _circuits.GetByIdAsync(delegation.DelegatedCircuitId.Value, ct);

        var lawyer = await _users.GetByIdAsync(request.AssignedLawyerId, ct);
        if (lawyer is null || lawyer.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي المختص غير موجود");
        // قاعدة الفرع (375-378): المحامي المختص من فرع الدائرة المنابة دائمًا —
        // للداخلية بدائرة فرع الدائرة (قد يعبر الفروع)، وللخارجية الفرع المناب،
        // وللداخلية بلا دائرة (قديمة) فرع الملف المنيب.
        if (lawyer.BranchId != approvalBranchId)
            throw new ArgumentException(isExternal
                ? "المحامي المختص ليس ضمن الفرع المناب"
                : approvalCircuit is not null
                    ? "المحامي المختص ليس ضمن فرع الدائرة المنابة"
                    : "المحامي المختص ليس ضمن فرع الملف المنيب");

        var targetBranch = approvalBranchId;

        // استنتاج دائرة المناب عند الاعتماد من DelegatedCircuitId (فحص المحافظة لا الفرع):
        // المناب قد يُسجَّل بدائرة فرع آخر؛ Document.BranchId يبقى فرع المحامي.
        int? targetCircuitId = null;
        string targetCourt;
        string? targetBranchName;
        if (isExternal)
        {
            targetCourt = Normalize(delegation.DelegatedCourt) ?? source.Court ?? string.Empty;
            targetBranchName = delegation.ExternalBranch?.Name;
        }
        else
        {
            // انتقالي: إنابات قديمة/اختبارية بلا دائرة مرجعية تُعتمد بنصها الحر
            // (صفوف ما قبل السجل) — الجديد يُلزم بالسجل في ValidateAndBuild.
            if (approvalCircuit is null)
            {
                targetCircuitId = null;
                targetCourt = Normalize(delegation.DelegatedCourt) ?? source.Court ?? string.Empty;
                targetBranchName = source.BranchName;
            }
            else
            {
                var targetCircuit = approvalCircuit;
                if (!targetCircuit.IsActive)
                    throw new ArgumentException("الدائرة المنابة معطلة — اختر دائرة نشطة");
                // فحص المحافظة فقط لا الفرع: محافظة فرع الدائرة == محافظة فرع المنيب.
                var sourceBranch = await _branches.GetByIdAsync(source.BranchId ?? 0, ct);
                var circuitBranch = await _branches.GetByIdAsync(targetCircuit.BranchId, ct);
                var sourceGov = sourceBranch?.Governorate?.Trim() ?? string.Empty;
                var circuitGov = circuitBranch?.Governorate?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(sourceGov) && !string.IsNullOrWhiteSpace(circuitGov)
                    && !string.Equals(sourceGov, circuitGov, StringComparison.Ordinal)
                    && !GovernorateCatalog.IsGovernorate(sourceGov))
                {
                    // محافظة الفرع غير معتمدة — يُرفض بصمت مفسَّر (البند 38).
                    throw new ArgumentException("محافظة الفرع غير معتمدة — ثبّتها من الإدارة قبل الإنابة");
                }
                if (!string.IsNullOrWhiteSpace(sourceGov) && !string.IsNullOrWhiteSpace(circuitGov)
                    && !string.Equals(sourceGov, circuitGov, StringComparison.Ordinal))
                    throw new ArgumentException("دائرة المناب ليست ضمن محافظة الملف المنيب");
                targetCircuitId = targetCircuit.Id;
                targetCourt = targetCircuit.Name;
                // هوية المناب من الدائرة (قرار §2.25): فرع الدائرة لا فرع المنيب.
                targetBranchName = (await _branches.GetByIdAsync(targetCircuit.BranchId, ct))?.Name
                    ?? source.BranchName;
            }
        }

        Document? target = null;
        try
        {
            await _tx.RunAsync(async token =>
            {
                // إنشاء الملف المناب تلقائيًا: نفس السند التنفيذي والأطراف، بنوع «انابة»،
                // موكولاً للمحامي المختص في الفرع المناب، مرتبطًا بإنابته (SourceDelegationId).
                // هوية المناب مستقلة: دائرته هي الدائرة المنابة المسجَّل فيها (موطن المال مكانيًا)،
                // ورقم أساسه مستقل (تسجيل أصولًا)، ونوعه «إنابة» دائمًا — لا دائرة المنيب.
                // المحامي المختص (Lawyer) يُضبط باسم المحامي الموكول — كمصدر العرض والفلترة
                // في «الملفات التنفيذية» — بنفس صيغة الإنشاء العادي (الاسم الكامل وإلا الدخول).
                target = new Document
                {
                    CreatedById = lawyer.Id,
                    Lawyer = string.IsNullOrWhiteSpace(lawyer.FullName) ? lawyer.Username : lawyer.FullName,
                    BranchId = targetBranch,
                    BranchName = targetBranchName ?? source.BranchName,
                    ExecutionCircuitId = targetCircuitId,
                    Court = targetCourt,
                    CourtNorm = string.IsNullOrWhiteSpace(targetCourt) ? null : ArabicNameNormalizer.Normalize(targetCourt),
                    GeneralEntitySide = source.GeneralEntitySide,
                    IsDraft = true,
                    FileType = FileTypeCatalog.Delegation,
                    SourceDelegationId = delegation.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                CopySourcePartiesAndBond(source, target);

                delegation.AssignedLawyerId = lawyer.Id;
                delegation.Status = DelegationStatusCatalog.Assigned;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;

                await _documents.AddAsync(target, token);
                await _uow.SaveChangesAsync(token);

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.DelegationAssignedToLawyer,
                        HeadAlertMessages.DelegationAssignedToLawyer(new(
                            delegation.DelegatedCourt ?? string.Empty, SourceLabel(source))),
                        HeadAlertAudience.DocumentCreator,
                        targetBranch,
                        userId,
                        DocumentId: target.Id,
                        DelegationId: delegation.Id),
                    new HeadAlertSupersedePlan(
                        delegation.Id,
                        AppealId: null,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_assigned"),
                    occurredAt: DateTime.UtcNow,
                    ct: token);

                await _uow.SaveChangesAsync(token);

                await _documents.AddAssignmentAsync(target.Id, AssignmentKindCatalog.Create,
                    lawyer.FullName, actorName, DateTime.UtcNow, token);

                await _audit.LogAsync(actorName, "assign_delegation",
                    source.Id, source.DocumentType,
                    $"اعتمد إنابة (رقم {delegation.Id}) إلى {delegation.DelegatedCourt} وكلّف المحامي {lawyer.FullName}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            // §7.7: سباق الاعتماد المزدوج — وحدانية `SourceDelegationId` تحمي التوأم،
            // والترجمة ودية (409 بالمجال) لا 500.
            throw new DocumentConflictException("الإنابة مأخوذة مسبقًا — اعتُمدت من مستخدم آخر", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}
