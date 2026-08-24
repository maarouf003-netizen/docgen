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
public sealed class DocumentDelegationService : IDocumentDelegationService
{
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IHeadAlertService _alerts;

    public DocumentDelegationService(
        IDelegationRepository delegations,
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<DocumentRegistrationDate> registrationDates,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IHeadAlertService alerts)
    {
        _delegations = delegations;
        _documents = documents;
        _users = users;
        _branches = branches;
        _registrationDates = registrationDates;
        _occurrences = occurrences;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _alerts = alerts;
    }

    public async Task<DelegationDto> CreateAsync(
        int sourceDocumentId,
        UpsertDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var source = await _documents.GetByIdAsync(sourceDocumentId, ct)
            ?? throw new ArgumentException("الملف المنيب غير موجود");

        ValidateSourceForDelegation(source);
        if (source.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تسطير إنابة على ملف لا تملكه");

        var (court, fields) = await ValidateAndBuildAsync(request, ct);

        var delegation = new DocumentDelegation
        {
            SourceDocumentId = source.Id,
            DelegatedCourt = court,
            IsExternal = fields.IsExternal,
            ExternalBranchId = fields.ExternalBranchId,
            DelegationDate = fields.DelegationDate,
            DelegationText = Normalize(request.DelegationText),
            DepositBookNumber = Normalize(request.DepositBookNumber),
            DepositBookDate = fields.DepositBookDate,
            Status = DelegationStatusCatalog.PendingHead,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyDelegationAssets(delegation, source, request.AssetIds);

        await _tx.RunAsync(async token =>
        {
            await _delegations.AddAsync(delegation, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_delegation",
                source.Id, source.DocumentType,
                $"سطّر إنابة على الملف (رقم {source.Id}) إلى {court}", token);
        }, ct);

        // إشعار رئيس القسم بإنابة معلّقة بانتظار اعتماده عبر نظام تنبيهات رئيس القسم: يُنشأ في
        // فرع الجهة المعنية بالاعتماد (الفرع المناب للإنابة الخارجية، فرع المنيب للداخلية).
        // إشعار فرعي — فشله لا يُفشل التسطير، ويُسجَّل في سجل التدقيق.
        var approvalBranchId = fields.IsExternal ? fields.ExternalBranchId : source.BranchId;
        if (approvalBranchId is null)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر إشعار رئيس القسم بالإنابة المعلّقة (رقم {delegation.Id}): لا يوجد فرع معني بالاعتماد", ct);
        }
        else
        {
            try
            {
                await _alerts.CreateAsync(new CreateHeadAlertRequest(
                    TargetType: "head",
                    DocumentId: source.Id,
                    TargetLawyerId: null,
                    Message: PendingApprovalMessage(source, court),
                    DelegationId: delegation.Id),
                    userId, approvalBranchId.Value, actorName, ct);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(actorName, "head_alert_failed",
                    source.Id, source.DocumentType,
                    $"تعذّر إشعار رئيس القسم بالإنابة المعلّقة (رقم {delegation.Id}): {ex.Message}", ct);
            }
        }

        return ToDto(delegation, source);
    }

    public async Task<DelegationDto?> UpdateAsync(
        int delegationId,
        UpsertDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        EnsurePendingByOwner(delegation, userId);
        var source = delegation.SourceDocument;

        var (court, fields) = await ValidateAndBuildAsync(request, ct);

        delegation.DelegatedCourt = court;
        delegation.IsExternal = fields.IsExternal;
        delegation.ExternalBranchId = fields.ExternalBranchId;
        delegation.DelegationDate = fields.DelegationDate;
        delegation.DelegationText = Normalize(request.DelegationText);
        delegation.DepositBookNumber = Normalize(request.DepositBookNumber);
        delegation.DepositBookDate = fields.DepositBookDate;
        delegation.UpdatedAt = DateTime.UtcNow;
        ApplyDelegationAssets(delegation, source, request.AssetIds);

        await _tx.RunAsync(async token =>
        {
            _delegations.Update(delegation);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_delegation",
                source.Id, source.DocumentType,
                $"عدّل إنابة (رقم {delegation.Id}) إلى {court}", token);
        }, ct);

        // تحديث رسالة تنبيه «بانتظار اعتماد الإنابة» لتطابق البيانات المعدّلة (تُبقى علامات
        // القراءة والمستلمين). إن لم يُنشأ التنبيه سابقًا (فشل إشعار سابق) لا يُستحدث هنا —
        // مسار الإشعار لا يُفشل عملية التعديل ويُسجَّل فشله في سجل التدقيق.
        try
        {
            await _alerts.UpdateDelegationAlertAsync(delegation.Id, PendingApprovalMessage(source, court), ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر تحديث تنبيه الإنابة المعلّقة (رقم {delegation.Id}) بعد تعديلها: {ex.Message}", ct);
        }

        return ToDto(delegation, source);
    }

    public async Task<bool> DeleteAsync(int delegationId, int userId, string? actorName, CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        EnsurePendingByOwner(delegation, userId);
        var source = delegation.SourceDocument;

        await _tx.RunAsync(async token =>
        {
            _delegations.Remove(delegation);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_delegation",
                source.Id, source.DocumentType,
                $"حذف إنابة (رقم {delegation.Id}) إلى {delegation.DelegatedCourt}", token);
        }, ct);

        // تصفية تنبيهات الإنابة بعد حذفها (لم تعد هناك إنابة معلّقة). إشعار فرعي — فشله لا
        // يُفشل الحذف ويُسجَّل في سجل التدقيق.
        try
        {
            await _alerts.DeleteByDelegationAsync(delegation.Id, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر حذف تنبيهات الإنابة المحذوفة (رقم {delegation.Id}): {ex.Message}", ct);
        }

        return true;
    }

    public async Task<List<DelegationDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        // الملف المناب: يُعرض «تشعباته» من إنابته (المصدر)، والملف المنيب: إناباته الصادرة.
        var delegations = doc.SourceDelegationId is not null
            ? await ListOfTargetAsync(doc.Id, ct)
            : await _delegations.ListBySourceAsync(doc.Id, ct);

        return delegations.Select(d => ToDto(d, d.SourceDocument)).ToList();
    }

    public async Task<List<DelegationDto>> ListPendingForHeadAsync(int branchId, CancellationToken ct = default)
    {
        var delegations = await _delegations.ListPendingByBranchAsync(branchId, ct);
        return delegations.Select(d => ToDto(d, d.SourceDocument)).ToList();
    }

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

        var source = delegation.SourceDocument;
        var isExternal = delegation.IsExternal;
        var externalBranchId = delegation.ExternalBranchId;

        // الإنابة الخارجية: يختار المحامي رئيس قسم الفرع المناب (الفرع المستلم).
        // الداخلية: رئيس قسم الفرع المنيب (الفرع الذي يملك الملف).
        var requiredBranch = isExternal ? externalBranchId : source.BranchId;
        if (requiredBranch is null || requiredBranch != headBranchId)
            throw new ArgumentException("لا يمكنك اعتماد هذه الإنابة — ليست ضمن فرعك");

        var lawyer = await _users.GetByIdAsync(request.AssignedLawyerId, ct);
        if (lawyer is null || lawyer.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي المختص غير موجود");
        if (!isExternal && lawyer.BranchId != source.BranchId)
            throw new ArgumentException("المحامي المختص ليس ضمن فرع الملف المنيب");
        if (isExternal && lawyer.BranchId != externalBranchId)
            throw new ArgumentException("المحامي المختص ليس ضمن الفرع المناب");

        var targetBranch = isExternal ? externalBranchId : source.BranchId;

        Document? target = null;
        await _tx.RunAsync(async token =>
        {
            // إنشاء الملف المناب تلقائيًا: نفس السند التنفيذي والأطراف، بنوع «انابة»،
            // موكولاً للمحامي المختص في الفرع المناب، مرتبطًا بإنابته (SourceDelegationId).
            target = new Document
            {
                CreatedById = lawyer.Id,
                BranchId = targetBranch,
                BranchName = isExternal ? delegation.ExternalBranch?.Name ?? source.BranchName : source.BranchName,
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

            await _documents.AddAsync(target, token);
            await _uow.SaveChangesAsync(token);
            _delegations.Update(delegation);
            await _uow.SaveChangesAsync(token);

            await _documents.AddAssignmentAsync(target.Id, AssignmentKindCatalog.Create,
                lawyer.FullName, actorName, DateTime.UtcNow, token);

            await _audit.LogAsync(actorName, "assign_delegation",
                source.Id, source.DocumentType,
                $"اعتمد إنابة (رقم {delegation.Id}) إلى {delegation.DelegatedCourt} وكلّف المحامي {lawyer.FullName}", token);
        }, ct);

        // تصفية تنبيه «بانتظار اعتماد الإنابة» بعد الاعتماد (أنجز مهمّته). إشعار فرعي —
        // فشله لا يُفشل الاعتماد، ويُسجَّل في سجل التدقيق.
        try
        {
            await _alerts.DeleteByDelegationAsync(delegation.Id, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                source.Id, source.DocumentType,
                $"تعذّر حذف تنبيه الإنابة المعلّقة (رقم {delegation.Id}) بعد اعتمادها: {ex.Message}", ct);
        }

        // تنبيه المحامي المختص عبر نظام تنبيهات رئيس القسم: إشعار بإنشاء الملف المناب
        // وتكليفه به. إشعار فرعي — فشله لا يُفشل الاعتماد، ويُسجَّل في سجل التدقيق.
        if (target is not null)
        {
            try
            {
                await _alerts.CreateAsync(new CreateHeadAlertRequest(
                    TargetType: "document",
                    DocumentId: target.Id,
                    TargetLawyerId: null,
                    Message: $"أحال إليك رئيس القسم ملف إنابة لقيده أصولًا في {delegation.DelegatedCourt} (ملف {SourceLabel(source)})"),
                    userId, targetBranch!.Value, actorName, ct);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(actorName, "head_alert_failed",
                    source.Id, source.DocumentType,
                    $"تعذّر إشعار المحامي {lawyer.FullName} بإنشاء الملف المناب: {ex.Message}", ct);
            }
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument)
            : null;
    }

    public async Task<DelegationDto?> RegisterAsync(
        int delegationId,
        RegisterDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        if (delegation.Status != DelegationStatusCatalog.Assigned)
            throw new ArgumentException("لا يمكن تسجيل إنابة لم تُعتمد");
        var target = delegation.TargetDocument
            ?? throw new ArgumentException("الملف المناب غير موجود");
        if (target.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تسجيل هذا الملف المناب");

        var fileNumber = Normalize(request.FileNumber);
        if (string.IsNullOrWhiteSpace(fileNumber))
            throw new ArgumentException("رقم أساس الإنابة مطلوب");
        var fileYear = Normalize(request.FileYear);
        if (string.IsNullOrWhiteSpace(fileYear))
            throw new ArgumentException("سنة قيد الإنابة مطلوبة");
        var registrationDate = FreeDateParser.Parse(request.FileRegistrationDate, "تاريخ قيد الإنابة");
        if (registrationDate is null)
            throw new ArgumentException("تاريخ قيد الإنابة مطلوب");

        await _tx.RunAsync(async token =>
        {
            target.FileNumber = fileNumber;
            target.FileYear = fileYear;
            target.IsDraft = false;
            target.UpdatedAt = DateTime.UtcNow;
            await ApplyRegistrationDateAsync(target, registrationDate.Value, token);
            _documents.Update(target);

            delegation.Status = DelegationStatusCatalog.Registered;
            delegation.UpdatedAt = DateTime.UtcNow;
            _delegations.Update(delegation);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actorName, "register_delegation",
                delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                $"سجّل إنابة (رقم {delegation.Id}) أصولًا برقم أساس {fileNumber}", token);
        }, ct);

        // إشعار رئيس القسم بإتمام التسجيل وبقاء الإنابة بانتظار الإتمام (بيع الأموال وإعادة
        // الملف): يُنشأ في فرع الملف المناب (فرع متابعة الإتمام). إشعار فرعي — فشله لا يُفشل
        // التسجيل، ويُسجَّل في سجل التدقيق.
        if (target.BranchId is null)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                $"تعذّر إشعار رئيس القسم بإنابة مسجّلة بانتظار الإتمام (رقم {delegation.Id}): الملف المناب بلا فرع", ct);
        }
        else
        {
            try
            {
                await _alerts.CreateAsync(new CreateHeadAlertRequest(
                    TargetType: "head",
                    DocumentId: target.Id,
                    TargetLawyerId: null,
                    Message: $"بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس {fileNumber} عن الإنابة على الملف ({SourceLabel(delegation.SourceDocument)}) إلى دائرة {delegation.DelegatedCourt}",
                    DelegationId: delegation.Id),
                    userId, target.BranchId.Value, actorName, ct);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(actorName, "head_alert_failed",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"تعذّر إشعار رئيس القسم بالإنابة المسجّلة بانتظار الإتمام (رقم {delegation.Id}): {ex.Message}", ct);
            }
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument)
            : null;
    }

    public async Task<DelegationDto?> CompleteAsync(
        int delegationId,
        CompleteDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");

        if (delegation.Status != DelegationStatusCatalog.Registered)
            throw new ArgumentException("لا يمكن إتمام إنابة لم تُسجَّل أصولًا");
        var target = delegation.TargetDocument
            ?? throw new ArgumentException("الملف المناب غير موجود");
        if (target.CreatedById != userId)
            throw new ArgumentException("لا يمكنك إتمام هذا الملف المناب");

        var returnDate = FreeDateParser.Parse(request.ReturnDate, "تاريخ إعادة الملف للدائرة المنيبة");
        if (returnDate is null)
            throw new ArgumentException("تاريخ إعادة الملف للدائرة المنيبة مطلوب");

        // «تاريخ قرار الإحالة القطعية»: يدخله محامي الملف المناب مع تاريخ الإعادة وبدل المبيع
        // (إلزامي)، ويُحفظ على الملف المنيب عند تفعيله «منفذ جبريا» (نص حر مُوحَّد الأرقام).
        var forcedExecutionDateRaw = (request.ForcedExecutionDate ?? string.Empty).Trim();
        if (FreeDateParser.Parse(forcedExecutionDateRaw, "تاريخ قرار الإحالة القطعية") is null)
            throw new ArgumentException("تاريخ قرار الإحالة القطعية مطلوب");
        var forcedExecutionDate = ArabicDigitNormalizer.Normalize(forcedExecutionDateRaw);

        var sales = request.Sales ?? new List<DelegationSaleDto>();
        var salesByAssetId = sales.ToDictionary(s => s.DelegationAssetId);
        foreach (var asset in delegation.Assets)
        {
            if (!salesByAssetId.TryGetValue(asset.Id, out var sale))
                throw new ArgumentException($"يجب إدخال بدل المبيع لكل أصل موضوع الإنابة ({asset.AssetLabel})");
            if (sale.SalePrice <= 0)
                throw new ArgumentException($"بدل المبيع غير صالح للأصل ({asset.AssetLabel})");
            asset.SalePrice = sale.SalePrice;
        }

        await _tx.RunAsync(async token =>
        {
            delegation.ReturnDate = returnDate;
            delegation.Status = DelegationStatusCatalog.Executed;
            delegation.UpdatedAt = DateTime.UtcNow;
            _delegations.Update(delegation);

            // الملف المناب يُصبح «منفذ إنابة»: حالة نهائية تُعامل منفذًا في القوائم والإحصاءات.
            target.ExecStatus = ExecutionStatusCatalog.DelegationExecuted;
            target.UpdatedAt = DateTime.UtcNow;
            _documents.Update(target);

            // قاعدة «يسري على المنيب»: يُفعَّل المنيب تلقائيًا «منفذ جبريا (منفذ جزئيا)» فيُحتسب
            // متداولًا في القوائم والإحصاءات، وعلى محامي المنيب لاحقًا «اعتبار الملف منفذًا
            // كاملًا بهذا البيع» (تاريخ تحويل البدل ورقم الإشعار) من نافذة تغيير الحالة — وحينها
            // فقط يدخل مبلغ الإنابة ضمن «إحصاءات منفذ جبريا» مرة واحدة (مسار DelegationSalesAmount).
            var source = delegation.SourceDocument;
            var markApplied = false;
            if (source is not null)
            {
                var alreadyMarked = source.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    && source.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted;
                if (!alreadyMarked)
                {
                    source.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
                    source.ExecSubStatus = ExecutionStatusCatalog.SubPartiallyExecuted;
                    source.ForcedExecutionDate = forcedExecutionDate;
                    ClearTarithAndSayerFields(source);
                    source.UpdatedAt = DateTime.UtcNow;
                    _documents.Update(source);
                    markApplied = true;
                }
            }
            await _uow.SaveChangesAsync(token);

            // وقعة تغيير حالة المنيب (تفعيل تلقائي «منفذ جبريا — منفذ جزئيا») ضمن المعاملة نفسها،
            // لتُسجَّل في «وقوعات الملف» كأي وقعة «منفذ جبريا» (تُسجَّل مرة واحدة لكل تفعيل).
            if (markApplied && source is not null)
            {
                await _occurrences.AddAsync(new DocumentOccurrence
                {
                    DocumentId = source.Id,
                    OccurrenceType = OccurrenceTypeCatalog.Forcible,
                    EventDate = DateTime.UtcNow,
                    Details = SerializeDetails(new Dictionary<string, string>
                    {
                        ["execSubStatus"] = ExecutionStatusCatalog.SubPartiallyExecuted,
                        ["forcedExecutionDate"] = source.ForcedExecutionDate ?? string.Empty,
                    }),
                    CreatedById = source.CreatedById,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, token);
                await _uow.SaveChangesAsync(token);
            }

            await _audit.LogAsync(actorName, "complete_delegation",
                delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                $"أتم إنابة (رقم {delegation.Id}): بيع الأموال موضوع الإنابة وإعادة الملف للدائرة المنيبة — اعتُبر الملف المنيب «منفذ جبريا (منفذ جزئيا)» تلقائيًا حتى اعتباره منفذًا كاملًا بهذا البيع", token);
        }, ct);

        // تصفية تنبيه «بانتظار الإتمام» بعد إتمام الإنابة (أنجز مهمّته). إشعار فرعي —
        // فشله لا يُفشل الإتمام، ويُسجَّل في سجل التدقيق.
        try
        {
            await _alerts.DeleteByDelegationAsync(delegation.Id, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                $"تعذّر حذف تنبيه الإنابة بانتظار الإتمام (رقم {delegation.Id}) بعد إتمامها: {ex.Message}", ct);
        }

        // إشعار محامي المنيب بإتمام إنابته عبر نظام تنبيهات رئيس القسم: يُنشأ التنبيه في فرع
        // الملف المنيب (ليصل صاحبه ويراه رئيس قسمه حتى في الإنابة الخارجية). إشعار فرعي —
        // فشله لا يُفشل الإتمام، ويُسجَّل في سجل التدقيق.
        var sourceBranchId = delegation.SourceDocument.BranchId;
        if (sourceBranchId is null)
        {
            await _audit.LogAsync(actorName, "head_alert_failed",
                delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                $"تعذّر إشعار محامي المنيب بإتمام الإنابة (رقم {delegation.Id}): الملف المنيب بلا فرع", ct);
        }
        else
        {
            try
            {
                var targetLabel = TargetFileLabel(target);
                var assetsLine = string.Join(" و", delegation.Assets.Select(a => a.AssetLabel));
                await _alerts.CreateAsync(new CreateHeadAlertRequest(
                    TargetType: "document",
                    DocumentId: delegation.SourceDocumentId,
                    TargetLawyerId: null,
                    Message: $"نفذت إنابتك في {targetLabel} للتنفيذ على {assetsLine} وأُعيد الملف للدائرة المنيبة، يرجى المراجعة والمتابعة أصولًا"),
                    userId, sourceBranchId.Value, actorName, ct);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(actorName, "head_alert_failed",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"تعذّر إشعار محامي المنيب بإتمام الإنابة (رقم {delegation.Id}): {ex.Message}", ct);
            }
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? ToDto(reloaded, reloaded.SourceDocument)
            : null;
    }

    /// <summary>التسمية المعروضة للملف المناب في إشعار الإتمام: «ملف الرقم/السنة» عبر قاعدة
    /// DisplayFileNumber نفسها (رقم أساس سنة التدوير الحالية إن وُجد، وإلا رقم ملفه الأصلي)،
    /// وإلا رقمه الداخلي.</summary>
    private static string TargetFileLabel(Document? target)
    {
        if (target is null) return "الملف المناب";
        var number = SourceFileNumber(target);
        var year = SourceFileYear(target);
        if (!string.IsNullOrWhiteSpace(number))
            return string.IsNullOrWhiteSpace(year) ? $"ملف {number}" : $"ملف {number}/{year}";
        return $"ملف {target.Id}";
    }

    // ── أدوات مساعدة ─────────────────────────────────────────────

    private static void ValidateSourceForDelegation(Document source)
    {
        // الإنابة على ملفات «طالبة تنفيذ» فقط (البيع بالمزاد والأموال المرهونة موجودة فيها حصرًا).
        if (GeneralEntitySideCatalog.IsExecutedLike(source.GeneralEntitySide))
            throw new ArgumentException("الإنابة تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");
        if (ExecutionStatusCatalog.IsExecuted(source.ExecStatus, source.ExecSubStatus)
            || source.ExecStatus == ExecutionStatusCatalog.StateStruckOff)
            throw new ArgumentException("لا يمكن تسطير إنابة على ملف منفَّذ أو مشطوب");
    }

    private void EnsurePendingByOwner(DocumentDelegation delegation, int userId)
    {
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن تعديل أو حذف إنابة لم تعد معلّقة");
        if (delegation.SourceDocument.CreatedById != userId)
            throw new ArgumentException("لا يمكنك تعديل أو حذف إنابة على ملف لا تملكه");
    }

    private async Task<(string Court, DelegationFields Fields)> ValidateAndBuildAsync(
        UpsertDelegationRequest request, CancellationToken ct)
    {
        var court = Normalize(request.DelegatedCourt);
        if (string.IsNullOrWhiteSpace(court))
            throw new ArgumentException("الدائرة المنابة مطلوبة");

        var isExternal = request.IsExternal;
        int? externalBranchId;
        if (isExternal)
        {
            externalBranchId = request.ExternalBranchId;
            if (externalBranchId is null)
                throw new ArgumentException("الإنابة الخارجية تتطلب تحديد الفرع المناب في المحافظة الأخرى");
            var branch = await _branches.GetByIdAsync(externalBranchId.Value, ct);
            if (branch is null)
                throw new ArgumentException("الفرع المناب غير موجود");
        }
        else
        {
            externalBranchId = null;
        }

        var delegationDate = FreeDateParser.Parse(request.DelegationDate, "تاريخ الإنابة");
        if (delegationDate is null)
            throw new ArgumentException("تاريخ الإنابة مطلوب");

        return (court, new DelegationFields(
            isExternal,
            externalBranchId,
            delegationDate,
            FreeDateParser.Parse(request.DepositBookDate, "تاريخ كتاب إيداع رئيس القسم")));
    }

    /// <summary>حقول الإنابة المنبثقة من الطلب بعد التحقق (الجهة الخارجية وتواريخها النصية الحرة).</summary>
    private sealed record DelegationFields(
        bool IsExternal,
        int? ExternalBranchId,
        DateTime? DelegationDate,
        DateTime? DepositBookDate);

    private void ApplyDelegationAssets(DocumentDelegation delegation, Document source, List<int>? assetIds)
    {
        var ids = (assetIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new ArgumentException("يجب اختيار الأموال موضوع الإنابة");

        var sourceIds = new HashSet<int>(source.Assets.Select(a => a.Id));
        foreach (var id in ids)
            if (!sourceIds.Contains(id))
                throw new ArgumentException("أصل من الأموال موضوع الإنابة لا يتبع الملف المنيب");

        delegation.Assets.Clear();
        foreach (var asset in source.Assets.Where(a => ids.Contains(a.Id)))
        {
            delegation.Assets.Add(new DelegationAsset
            {
                AssetKind = asset.AssetKind,
                AssetLabel = AssetDisplay.Label(asset),
            });
        }
    }

    /// <summary>
    /// لقطة مجمّدة من الملف المنيب إلى الملف المناب (منفصلة عنه): أطرافه (المقترض والكفلاء
    /// والجهات العامة وورثتهم) وبيانات سنده وكتبه — فلا يظهر أي صف أطراف أو كتاب فارغًا
    /// على الملف المناب رغم وجوده على الملف المنيب. الإنابة تخص ملفات «طالبة تنفيذ» فقط
    /// (ValidateSourceForDelegation)، فأطرافها هي: المقترض والكفلاء والورثة والجهات العامة.
    /// </summary>
    private static void CopySourcePartiesAndBond(Document source, Document target)
    {
        CopyBorrower(source, target);
        CopyBond(source, target);
        CopyBooks(source, target);

        // الكفلاء.
        foreach (var g in source.Guarantors)
            target.Guarantors.Add(CopyGuarantor(g));

        // ورثة المقترض/الكفلاء.
        foreach (var h in source.Heirs)
            target.Heirs.Add(CopyHeir(h));

        // الجهات العامة طالبة التنفيذ — مع ربط السجل المرجعي ونسخة التسريع للفلترة.
        foreach (var e in source.ApplicantPublicEntities)
            target.ApplicantPublicEntities.Add(new ApplicantPublicEntity
            {
                Name = e.Name,
                Branch = e.Branch,
                Governorate = e.Governorate,
                RegistryId = e.RegistryId,
            });
        target.ApplicantRegistryId = target.ApplicantPublicEntities
            .Select(a => a.RegistryId)
            .FirstOrDefault(id => id.HasValue);
    }

    private static void CopyBorrower(Document source, Document target)
    {
        target.BorrowerName = source.BorrowerName;
        target.BorrowerFather = source.BorrowerFather;
        target.BorrowerFamily = source.BorrowerFamily;
        target.BorrowerMother = source.BorrowerMother;
        target.BorrowerBirth = source.BorrowerBirth;
        target.BorrowerRegister = source.BorrowerRegister;
        target.BorrowerNationalId = source.BorrowerNationalId;
        target.BorrowerAddress = source.BorrowerAddress;
        target.BorrowerAddressType = source.BorrowerAddressType;
        target.BorrowerNature = source.BorrowerNature;
        target.BorrowerRegistrationNumber = source.BorrowerRegistrationNumber;
        target.BorrowerRepresentedBy = source.BorrowerRepresentedBy;

        target.BorrowerRepresentativeName = source.BorrowerRepresentativeName;
        target.BorrowerRepresentativeFather = source.BorrowerRepresentativeFather;
        target.BorrowerRepresentativeFamily = source.BorrowerRepresentativeFamily;
        target.BorrowerRepresentativeCapacity = source.BorrowerRepresentativeCapacity;
        target.BorrowerRepresentativeAddressType = source.BorrowerRepresentativeAddressType;
        target.BorrowerRepresentativeAddress = source.BorrowerRepresentativeAddress;
    }

    private static void CopyBond(Document source, Document target)
    {
        target.ContractType = source.ContractType;
        target.ContractTypeSelector = source.ContractTypeSelector;
        target.ContractNumber = source.ContractNumber;
        target.ContractDate = source.ContractDate;
        target.AnnexType = source.AnnexType;
        target.AnnexNumber = source.AnnexNumber;
        target.AnnexDate = source.AnnexDate;
        target.InclusionText = source.InclusionText;

        target.AmountNumeric = source.AmountNumeric;
        target.AmountWords = source.AmountWords;
        target.Currency = source.Currency;
        target.Amount2Numeric = source.Amount2Numeric;
        target.Amount2Words = source.Amount2Words;
        target.Currency2 = source.Currency2;
        target.Amount3Numeric = source.Amount3Numeric;
        target.Amount3Words = source.Amount3Words;
        target.Currency3 = source.Currency3;
        target.InclusionAmountNumeric = source.InclusionAmountNumeric;
        target.InclusionAmountWords = source.InclusionAmountWords;
        target.InclusionCurrency = source.InclusionCurrency;
        target.InclusionAmount2Numeric = source.InclusionAmount2Numeric;
        target.InclusionAmount2Words = source.InclusionAmount2Words;
        target.InclusionCurrency2 = source.InclusionCurrency2;
        target.InclusionAmount3Numeric = source.InclusionAmount3Numeric;
        target.InclusionAmount3Words = source.InclusionAmount3Words;
        target.InclusionCurrency3 = source.InclusionCurrency3;

        target.Court = source.Court;
        target.Applicant = source.Applicant;
    }

    /// <summary>كتب الملف المنيب التي تنتقل معه إلى الملف المناب: ورود الملف وكتاب الجهة العامة ورقم تحت رفع.</summary>
    private static void CopyBooks(Document source, Document target)
    {
        target.FileArrivalNumber = source.FileArrivalNumber;
        target.FileArrivalDate = source.FileArrivalDate;
        target.FileIncoming = source.FileIncoming;
        target.FileIncomingDate = source.FileIncomingDate;
        target.UnderFilingNumber = source.UnderFilingNumber;
        target.FileReceiptNumber = source.FileReceiptNumber;
        target.FileReceiptDate = source.FileReceiptDate;
    }

    private static Guarantor CopyGuarantor(Guarantor g) => new()
    {
        GuarantorNumber = g.GuarantorNumber,
        GuarantorName = g.GuarantorName,
        GuarantorFather = g.GuarantorFather,
        GuarantorFamily = g.GuarantorFamily,
        GuarantorMother = g.GuarantorMother,
        GuarantorBirth = g.GuarantorBirth,
        GuarantorRegister = g.GuarantorRegister,
        GuarantorNationalId = g.GuarantorNationalId,
        GuarantorAddress = g.GuarantorAddress,
        AddressType = g.AddressType,
        GuarantorNature = g.GuarantorNature,
        GuarantorRegistrationNumber = g.GuarantorRegistrationNumber,
        GuarantorRepresentedBy = g.GuarantorRepresentedBy,
        RepresentativeName = g.RepresentativeName,
        RepresentativeFather = g.RepresentativeFather,
        RepresentativeFamily = g.RepresentativeFamily,
        RepresentativeCapacity = g.RepresentativeCapacity,
        RepresentativeAddressType = g.RepresentativeAddressType,
        RepresentativeAddress = g.RepresentativeAddress,
    };

    private static Heir CopyHeir(Heir h) => new()
    {
        GuarantorNumber = h.GuarantorNumber,
        HeirName = h.HeirName,
        HeirFather = h.HeirFather,
        HeirFamily = h.HeirFamily,
        HeirCapacity = h.HeirCapacity,
        AddressType = h.AddressType,
        HeirAddress = h.HeirAddress,
    };

    /// <summary>
    /// يُسجَّل تاريخ قيد الملف المناب (DocumentRegistrationDate 1:1 بمفتاح DocumentId):
    /// يُضاف عند غيابه ويُحدَّث إن وُجد.
    /// </summary>
    private async Task ApplyRegistrationDateAsync(Document doc, DateTime parsed, CancellationToken token)
    {
        var dateText = parsed.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (doc.RegistrationDate is null)
            await _registrationDates.AddAsync(new DocumentRegistrationDate
            {
                DocumentId = doc.Id,
                Date = dateText,
                DateParsed = parsed,
            }, token);
        else
        {
            doc.RegistrationDate.Date = dateText;
            doc.RegistrationDate.DateParsed = parsed;
        }
    }

    private async Task<List<DocumentDelegation>> ListOfTargetAsync(int targetDocumentId, CancellationToken ct)
    {
        var delegation = await _delegations.FindByTargetAsync(targetDocumentId, ct);
        return delegation is null ? new List<DocumentDelegation>() : new List<DocumentDelegation> { delegation };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// تطهير حقول «التريث» و«كتاب السير بالملف» عند التفعيل التلقائي «منفذ جبريا (منفذ جزئيا)»
    /// — بنفس معاملة مسار «تغيير الحالة» اليدوية (منفذ جبريا ينظف حقول التريث والسير).
    /// </summary>
    private static void ClearTarithAndSayerFields(Document source)
    {
        source.TarithNumber = null;
        source.TarithDate = null;
        source.TarithRegNumber = null;
        source.TarithRegDate = null;
        source.SayerNumber = null;
        source.SayerDate = null;
        source.SayerRegNumber = null;
        source.SayerRegDate = null;
    }

    private static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    private static DelegationDto ToDto(DocumentDelegation d, Document source) => new(
        d.Id,
        d.SourceDocumentId,
        SourceLabel(source),
        SourceFileNumber(source),
        SourceFileYear(source),
        d.TargetDocument?.Id,
        d.DelegatedCourt,
        d.IsExternal,
        d.ExternalBranchId,
        d.ExternalBranch?.Name,
        FreeDateParser.ToResponse(d.DelegationDate),
        d.DelegationText,
        d.DepositBookNumber,
        FreeDateParser.ToResponse(d.DepositBookDate),
        d.AssignedLawyerId,
        d.AssignedLawyer?.FullName,
        FreeDateParser.ToResponse(d.ReturnDate),
        d.Status,
        d.CreatedAt,
        d.CreatedBy?.FullName,
        d.Assets.Select(a => new DelegationAssetDto(a.Id, a.AssetKind, a.AssetLabel, a.SalePrice, a.SnapshotAdjusted)).ToList(),
        d.CreatedById);

    private static string SourceLabel(Document source)
    {
        var parts = new[] { source.BorrowerName, source.BorrowerFather, source.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v));
        var name = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(name) ? source.DocumentType ?? $"ملف {source.Id}" : name;
    }

    /// <summary>رسالة تنبيه «بانتظار اعتماد الإنابة» التي تُنشأ وقت التسطير وتُحدَّث عند التعديل.</summary>
    private static string PendingApprovalMessage(Document source, string court)
        => $"بانتظار اعتماد الإنابة — سطّر المحامي {source.CreatedBy?.FullName} إنابة على الملف ({SourceLabel(source)}) إلى دائرة {court}";

    /// <summary>
    /// رقم أساس الملف المنيب الحالي كما يظهر في صفحته: رقم أساس سنة التدوير الحالية إن وُجد
    /// (سجل DocumentBaseNumber) وإلا رقم ملفه الأصلي — نفس قاعدة DisplayFileNumber للملف المنيب.
    /// </summary>
    private static string? SourceFileNumber(Document source)
    {
        var currentYear = DateTime.Today.Year;
        var baseNumber = source.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.BaseNumber;
        return string.IsNullOrWhiteSpace(baseNumber) ? Normalize(source.FileNumber) : baseNumber.Trim();
    }

    /// <summary>سنة رقم الأساس المعروض للمنيب: سنة التدوير إن وُجدت وإلا سنة رقم ملفه الأصلي.</summary>
    private static string? SourceFileYear(Document source)
    {
        var currentYear = DateTime.Today.Year;
        var baseYear = source.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.Year;
        return baseYear?.ToString() ?? Normalize(source.FileYear);
    }
}
