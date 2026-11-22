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

        var caller = await LoadHeadCallerAsync(userId, headBranchId, ct);
        var callerBranch = caller.BranchId ?? headBranchId
            ?? throw new ArgumentException("الرئيس دون فرع لا يمكنه اعتماد الإنابات");

        var isExternal = delegation.IsExternal;
        var externalBranchId = delegation.ExternalBranchId;

        // نطاق الاعتماد (§7.1 — قلب القاعدة): الاعتماد لمالك الدائرة المنابة لا المنيب.
        var (approvalBranchId, approvalSectionId) = await ResolveApprovalScopeAsync(delegation, ct);
        EnsureApprover(caller, callerBranch, approvalBranchId, approvalSectionId);

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

    /// <summary>
    /// نطاق الاعتماد المحسوب (§7.1): فرع الاعتماد + شعبته (null للقسم).
    /// داخلية بدائرة → فرع الدائرة وشعبتها؛ داخلية بلا دائرة (قديمة/انتقالية/
    /// مفكوكة) → فرع المنيب بلا شعبة (قاعدته القديمة)؛ خارجية → الفرع المناب
    /// مع الشعبة الموجَّه لها (null قبل التوجيه).
    /// </summary>
    private async Task<(int BranchId, int? SectionId)> ResolveApprovalScopeAsync(
        DocumentDelegation delegation, CancellationToken ct)
    {
        if (!delegation.IsExternal && delegation.DelegatedCircuitId is not null)
        {
            var circuit = delegation.DelegatedCircuit
                ?? await _circuits.GetByIdAsync(delegation.DelegatedCircuitId.Value, ct);
            if (circuit is null)
                throw new ArgumentException("الدائرة المنابة غير موجودة — أعد ربط الإنابة بدائرة نشطة");
            return (circuit.BranchId, circuit.SectionId);
        }
        if (!delegation.IsExternal)
        {
            return (delegation.SourceDocument.BranchId
                ?? throw new ArgumentException("لا يمكن اعتماد هذه الإنابة — الملف المنيب بلا فرع"), null);
        }
        return (delegation.ExternalBranchId
            ?? throw new ArgumentException("الإنابة الخارجية بلا فرع مناب — تعذّر الاعتماد"),
            delegation.RedirectedToSectionId);
    }

    /// <summary>
    /// حارس المعتمد (§7.1): رئيس القسم لنطاق القسم (بلا شعبة)، ورئيس الشعبة
    /// لشعبتها حصرًا — رمز بلا شعبة مرفوض بلا تدهور لنطاق القسم.
    /// </summary>
    private static void EnsureApprover(User caller, int callerBranch, int approvalBranch, int? approvalSection)
    {
        if (caller.Role != UserRole.Head && caller.Role != UserRole.SubHead)
            throw new ArgumentException("اعتماد الإنابات للرؤساء فقط");
        if (callerBranch != approvalBranch)
            throw new ArgumentException("لا يمكنك اعتماد هذه الإنابة — ليست ضمن فرعك");
        if (caller.Role == UserRole.Head)
        {
            if (approvalSection.HasValue)
                throw new ArgumentException("الدائرة ليست ضمن نطاقك");
        }
        else if (caller.SectionId is null || approvalSection != caller.SectionId)
        {
            throw new ArgumentException("الدائرة ليست ضمن نطاقك");
        }
    }

    private async Task<User> LoadHeadCallerAsync(int userId, int? headBranchId, CancellationToken ct)
    {
        var caller = await _users.GetByIdAsync(userId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        if (caller.Role != UserRole.Head && caller.Role != UserRole.SubHead)
            throw new ArgumentException("اعتماد الإنابات للرؤساء فقط");
        if (caller.Role == UserRole.SubHead && caller.SectionId is null)
            throw new ArgumentException("حسابك بلا شعبة — أعد الدخول");
        if (caller.BranchId is null && headBranchId is null)
            throw new ArgumentException("الرئيس دون فرع لا يمكنه اعتماد الإنابات");
        return caller;
    }

    /// <summary>
    /// توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب (§7.3) — رئيس قسم الفرع
    /// المناب فقط، والشعبة من فرعه ونشطة ولها رئيس مفعّل. يُستبدل تنبيه القسم
    /// بتنبيه موجَّه لرئيس الشعبة، ويُعاد للقسم بالتراجع قبل الإسناد.
    /// </summary>
    public async Task<DelegationDto?> RedirectToSectionAsync(
        int delegationId,
        RedirectDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن توجيه إنابة لم تعد معلّقة");
        if (delegation.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن توجيه إنابة بعد إسنادها");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("لا يمكن توجيه إنابة مرفوضة — بانتظار تصحيح المحامي");
        if (!delegation.IsExternal)
            throw new ArgumentException("التوجيه للشعبة للإنابات الخارجية فقط");
        if (delegation.RedirectedToSectionId is not null)
            throw new ArgumentException("الإنابة موجَّهة لشعبة سلفًا — تراجع عن التوجيه أولًا");
        if (request.Version.HasValue && delegation.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا");

        var caller = await _users.GetByIdAsync(userId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var approvalBranch = delegation.ExternalBranchId
            ?? throw new ArgumentException("الإنابة الخارجية بلا فرع مناب — تعذّر التوجيه");
        if (caller.Role != UserRole.Head || caller.BranchId != approvalBranch)
            throw new ArgumentException("التوجيه للشعبة لرئيس قسم الفرع المناب فقط");

        var section = await _sections.GetByIdAsync(request.SectionId, ct)
            ?? throw new ArgumentException("الشعبة الموجَّه لها غير موجودة");
        if (section.BranchId != approvalBranch)
            throw new ArgumentException("التوجيه لشعبة في الفرع المناب نفسه");
        if (!section.IsActive)
            throw new ArgumentException("الشعبة الموجَّه لها معطلة — اختر شعبة نشطة");
        var subHead = await _users.FindActiveHeadAsync(UserRole.SubHead, approvalBranch, section.Id, ct)
            ?? throw new ArgumentException("الشعبة بلا رئيس مفعّل — عيّن رئيسًا قبل التوجيه");

        try
        {
            await _tx.RunAsync(async token =>
            {
                delegation.RedirectedToSectionId = section.Id;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;
                _delegations.Update(delegation);

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.DelegationSentToSubhead,
                        HeadAlertMessages.DelegationSentToSubhead(new(
                            delegation.Id, SourceLabel(delegation.SourceDocument))),
                        HeadAlertAudience.SingleHead,
                        approvalBranch,
                        userId,
                        DocumentId: delegation.SourceDocumentId,
                        DelegationId: delegation.Id,
                        RecipientUserId: subHead.Id),
                    new HeadAlertSupersedePlan(
                        delegation.Id,
                        AppealId: null,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_redirected"),
                    occurredAt: DateTime.UtcNow,
                    ct: token);

                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "redirect_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"وجّه الإنابة (رقم {delegation.Id}) لشعبة {section.Name}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // توجيهان متزامنان حقيقيان للإنابة نفسها — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }

    /// <summary>
    /// التراجع عن توجيه الشعبة قبل الإسناد (§7.3) — رئيس قسم فرع الاعتماد
    /// (آمن للتعاقب: أي رئيس قسم حالي للفرع نفسه). يُستبدل تنبيه الشعبة بتنبيه القسم.
    /// </summary>
    public async Task<DelegationDto?> RecallRedirectAsync(
        int delegationId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن التراجع عن توجيه إنابة لم تعد معلّقة");
        if (delegation.AssignedLawyerId is not null)
            throw new ArgumentException("تعذّر التراجع بعد الإسناد");
        if (delegation.RedirectedToSectionId is null)
            throw new ArgumentException("لا يوجد توجيه مفتوح لهذه الإنابة");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("لا يمكن التراجع عن توجيه إنابة مرفوضة — بانتظار تصحيح المحامي");

        var caller = await _users.GetByIdAsync(userId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var approvalBranch = delegation.IsExternal
            ? delegation.ExternalBranchId
            : delegation.SourceDocument.BranchId;
        if (caller.Role != UserRole.Head || caller.BranchId != approvalBranch)
            throw new ArgumentException("التراجع عن التوجيه لرئيس قسم فرع الاعتماد فقط");

        try
        {
            await _tx.RunAsync(async token =>
            {
                delegation.RedirectedToSectionId = null;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;
                _delegations.Update(delegation);

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.DelegationPendingApproval,
                        HeadAlertMessages.DelegationPendingApproval(new(
                            delegation.SourceDocument.CreatedBy?.FullName ?? string.Empty,
                            SourceLabel(delegation.SourceDocument),
                            AlertCourtAfterDaira(delegation.DelegatedCourt))),
                        HeadAlertAudience.SingleHead,
                        approvalBranch!.Value,
                        userId,
                        DocumentId: delegation.SourceDocumentId,
                        DelegationId: delegation.Id,
                        RecipientUserId: userId),
                    new HeadAlertSupersedePlan(
                        delegation.Id,
                        AppealId: null,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_redirect_recalled"),
                    occurredAt: DateTime.UtcNow,
                    ct: token);

                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "recall_redirect_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"تراجع عن توجيه الإنابة (رقم {delegation.Id}) للشعبة — عادت لرئيس القسم", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // تراجعان متزامنان حقيقيان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }

    /// <summary>
    /// رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — المعتمد الحالي
    /// فقط (نطاق الاعتماد نفسه). تبقى معلّقة بفلتر «مرفوض بانتظار التصحيح»،
    /// ويُشعَر محامي المنيب (بطاقة ملفه + تنبيه)، وتحرّرها تعديله.
    /// </summary>
    public async Task<DelegationDto?> RejectAsync(
        int delegationId,
        RejectDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن رفض إنابة لم تعد معلّقة");
        if (delegation.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن رفض إنابة بعد إسنادها");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("الإنابة مرفوضة سلفًا — بانتظار تصحيح المحامي");
        if (request.Version.HasValue && delegation.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is null)
            throw new ArgumentException("سبب الرفض مطلوب — يُعرض للمحامي للتصحيح");
        if (reason.Length > 1000)
            throw new ArgumentException("طول سبب الرفض يتجاوز الحد الأقصى (1000 حرف)");

        var caller = await LoadHeadCallerAsync(userId, null, ct);
        var callerBranch = caller.BranchId
            ?? throw new ArgumentException("الرئيس دون فرع لا يمكنه رفض الإنابات");
        var (approvalBranch, approvalSection) = await ResolveApprovalScopeAsync(delegation, ct);
        EnsureApprover(caller, callerBranch, approvalBranch, approvalSection);
        var sourceBranch = delegation.SourceDocument.BranchId;

        try
        {
            await _tx.RunAsync(async token =>
            {
                delegation.RejectReason = reason;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;
                _delegations.Update(delegation);

                if (sourceBranch is not null)
                {
                    await _alertOutbox.EnqueueSystemAsync(
                        new CreateSystemHeadAlertRequest(
                            HeadAlertKindCatalog.DelegationRejectedWrongCourt,
                            HeadAlertMessages.DelegationRejectedWrongCourt(new(
                                delegation.Id,
                                CourtWithExecutionPrefix(delegation.DelegatedCourt),
                                SourceLabel(delegation.SourceDocument),
                                reason)),
                            HeadAlertAudience.SingleLawyer,
                            sourceBranch.Value,
                            userId,
                            DocumentId: delegation.SourceDocumentId,
                            TargetLawyerId: delegation.CreatedById,
                            DelegationId: delegation.Id,
                            RecipientUserId: delegation.CreatedById),
                        new HeadAlertSupersedePlan(
                            delegation.Id,
                            AppealId: null,
                            HeadAlertKindCatalog.DelegationKinds,
                            "delegation_rejected"),
                        occurredAt: DateTime.UtcNow,
                        ct: token);
                }
                else
                {
                    await _alerts.ArchiveByDelegationAsync(
                        delegation.Id,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_rejected_no_branch",
                        token);
                }

                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "reject_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"رفض الإنابة (رقم {delegation.Id}) للدائرة الخطأ — السبب: {reason}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // رفضان متزامنان حقيقيان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}
