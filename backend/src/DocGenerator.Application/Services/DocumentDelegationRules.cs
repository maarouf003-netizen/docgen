using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد الإنابة المشتركة بين الخدمة ومراحل `P4-01` — حرّاس الملكية/الحالة، والتحقق
/// وبناء الحقول، واستهداف اعتماد التنبيه، وتطهير الحجوزات اليتيمة.
/// </summary>
internal static class DocumentDelegationRules
{
    /// <summary>
    /// فحص الملكية أولًا (403) قبل حالة الإنابة (400) — لا نكشف حالة إنابة ليست
    /// للمستخدم برسالة 400، ولا نخلط فشل الصلاحية بخطأ مدخلات (`P2-02e`).
    /// </summary>
    internal static void EnsurePendingByOwner(DocumentDelegation delegation, int userId)
    {
        if (delegation.SourceDocument.CreatedById != userId)
            throw new UnauthorizedAccessException("لا يمكنك تعديل أو حذف إنابة على ملف لا تملكه");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن تعديل أو حذف إنابة لم تعد معلّقة");
    }

    /// <summary>حقول الإنابة المنبثقة من الطلب بعد التحقق (الجهة الخارجية وتواريخها النصية الحرة).</summary>
    internal sealed record DelegationFields(
        bool IsExternal,
        int? ExternalBranchId,
        DateTime? DelegationDate,
        DateTime? DepositBookDate);

    internal static async Task<(string Court, int? CircuitId, DelegationFields Fields)> ValidateAndBuildAsync(
        UpsertDelegationRequest request, int? sourceBranchId,
        IRepository<Branch> branches, IRepository<ExecutionCircuit> circuits, CancellationToken ct)
    {
        var isExternal = request.IsExternal;
        int? externalBranchId;
        string court;
        int? circuitId = null;
        if (isExternal)
        {
            // الاستثناء الخارجي: دائرتها خارج المحافظة وخارج السجل — تبقى نصًا حرًا.
            court = Normalize(request.DelegatedCourt)
                ?? throw new ArgumentException("الدائرة المنابة مطلوبة");
            if (request.DelegatedCircuitId is not null)
                throw new ArgumentException("الإنابة الخارجية لا تُربط بسجل الدوائر");
            externalBranchId = request.ExternalBranchId;
            if (externalBranchId is null)
                throw new ArgumentException("الإنابة الخارجية تتطلب تحديد الفرع المناب في المحافظة الأخرى");
            var branch = await branches.GetByIdAsync(externalBranchId.Value, ct);
            if (branch is null)
                throw new ArgumentException("الفرع المناب غير موجود");
        }
        else
        {
            externalBranchId = null;
            // قفل تسطير الإنابة (S2): الداخلي يتحقق من السجل منذ الإنشاء —
            // الوجود + النشاط + محافظة فرع الدائرة == محافظة فرع المنيب
            // (مرآة فحص الاعتماد، البند 38)، وإلا تسرّب اسم دائرة فرع آخر
            // في المسودة ثم يفشل الاعتماد متأخرًا.
            if (request.DelegatedCircuitId is not null)
            {
                var circuit = await circuits.GetByIdAsync(request.DelegatedCircuitId.Value, ct)
                    ?? throw new ArgumentException("الدائرة المنابة المختارة غير موجودة");
                if (!circuit.IsActive)
                    throw new ArgumentException("الدائرة المنابة معطلة — اختر دائرة نشطة");
                var draftSourceBranch = await branches.GetByIdAsync(sourceBranchId ?? 0, ct);
                var draftCircuitBranch = await branches.GetByIdAsync(circuit.BranchId, ct);
                var draftSourceGov = draftSourceBranch?.Governorate?.Trim() ?? string.Empty;
                var draftCircuitGov = draftCircuitBranch?.Governorate?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(draftSourceGov) && !string.IsNullOrWhiteSpace(draftCircuitGov)
                    && !string.Equals(draftSourceGov, draftCircuitGov, StringComparison.Ordinal)
                    && !GovernorateCatalog.IsGovernorate(draftSourceGov))
                {
                    throw new ArgumentException("محافظة الفرع غير معتمدة — ثبّتها من الإدارة قبل الإنابة");
                }
                if (!string.IsNullOrWhiteSpace(draftSourceGov) && !string.IsNullOrWhiteSpace(draftCircuitGov)
                    && !string.Equals(draftSourceGov, draftCircuitGov, StringComparison.Ordinal))
                    throw new ArgumentException("دائرة المناب ليست ضمن محافظة الملف المنيب");
                circuitId = circuit.Id;
                court = circuit.Name;
            }
            else
            {
                // انتقالي: نص حر يُقبل فقط حين لا يملك النطاق أي دائرة بعد.
                court = Normalize(request.DelegatedCourt)
                    ?? throw new ArgumentException("الدائرة المنابة مطلوبة");
                var all = await circuits.ListAsync(ct);
                if (all.Count > 0)
                    throw new ArgumentException("الدائرة المنابة خارج السجل — اختر من قائمة المحافظة");
            }
        }

        var delegationDate = FreeDateParser.Parse(request.DelegationDate, "تاريخ الإنابة");
        if (delegationDate is null)
            throw new ArgumentException("تاريخ الإنابة مطلوب");

        return (court, circuitId, new DelegationFields(
            isExternal,
            externalBranchId,
            delegationDate,
            FreeDateParser.Parse(request.DepositBookDate, "تاريخ كتاب إيداع رئيس القسم")));
    }

    /// <summary>
    /// هدف تنبيه «بانتظار الاعتماد» (§7.1): فرع الاعتماد + مالكه — فرع الدائرة
    /// المنابة ومالكها (رئيس شعبته وإلا قسمه) للداخلية بدائرة، والفرع المناب
    /// ورئيس قسمه للخارجية، وفرع المنيب ورئيس قسمه بلا دائرة. `null` للمالك
    /// حين لا يوجد رئيس فعّال (يبقى التنبيه محفوظًا لفرع صحيح).
    /// </summary>
    internal static async Task<(int AlertBranch, int? OwnerId)> ResolvePendingAlertTargetAsync(
        bool isExternal, int? externalBranchId, int? sourceBranchId, int? circuitId,
        IUserRepository users, IRepository<ExecutionCircuit> circuits, CancellationToken ct)
    {
        if (isExternal)
        {
            var branch = externalBranchId
                ?? throw new ArgumentException("الإنابة الخارجية بلا فرع مناب — تعذّر الاستهداف");
            return (branch, (await users.FindActiveHeadAsync(UserRole.Head, branch, null, ct))?.Id);
        }
        var circuit = circuitId.HasValue ? await circuits.GetByIdAsync(circuitId.Value, ct) : null;
        if (circuit is not null)
        {
            if (circuit.SectionId.HasValue)
                return (circuit.BranchId, ((await users.FindActiveHeadAsync(UserRole.SubHead, circuit.BranchId, circuit.SectionId, ct))
                    ?? await users.FindActiveHeadAsync(UserRole.Head, circuit.BranchId, null, ct))?.Id);
            return (circuit.BranchId, (await users.FindActiveHeadAsync(UserRole.Head, circuit.BranchId, null, ct))?.Id);
        }
        var srcBranch = sourceBranchId
            ?? throw new ArgumentException("الملف بلا فرع — تعذّر الاستهداف");
        return (srcBranch, (await users.FindActiveHeadAsync(UserRole.Head, srcBranch, null, ct))?.Id);
    }

    /// <summary>
    /// مواءمة الحجوزات اليتيمة (B3 — شفاء ذاتي في نقطة واحدة): تحذف صفوف الحجوزات التي
    /// لم تعد لإنابة حاجبة قبل الفحص. الحكم على «حاجبة» داخل جملة الحذف من حالة القاعدة
    /// اللحظية، لا من لقطة المصدر — وإلا محا سباقٌ متزامن حجز إنابةٍ التزمت بعد القراءة.
    /// </summary>
    internal static Task PruneStaleReservationsAsync(
        Document source, IDelegationReservationRepository reservations, CancellationToken ct)
        => reservations.DeleteStaleAsync(source.Id, ct);

    /// <summary>
    /// نطاق الاعتماد المحسوب (§7.1): فرع الاعتماد + شعبته (null للقسم).
    /// داخلية بدائرة → فرع الدائرة وشعبتها؛ داخلية بلا دائرة (قديمة/انتقالية/
    /// مفكوكة) → فرع المنيب بلا شعبة (قاعدته القديمة)؛ خارجية → الفرع المناب
    /// مع الشعبة الموجَّه لها (null قبل التوجيه).
    /// </summary>
    internal static async Task<(int BranchId, int? SectionId)> ResolveApprovalScopeAsync(
        DocumentDelegation delegation, IRepository<ExecutionCircuit> circuits, CancellationToken ct)
    {
        if (!delegation.IsExternal && delegation.DelegatedCircuitId is not null)
        {
            var circuit = delegation.DelegatedCircuit
                ?? await circuits.GetByIdAsync(delegation.DelegatedCircuitId.Value, ct);
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
    internal static void EnsureApprover(User caller, int callerBranch, int approvalBranch, int? approvalSection)
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

    internal static async Task<User> LoadHeadCallerAsync(
        int userId, int? headBranchId, IUserRepository users, CancellationToken ct)
    {
        var caller = await users.GetByIdAsync(userId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        if (caller.Role != UserRole.Head && caller.Role != UserRole.SubHead)
            throw new ArgumentException("اعتماد الإنابات للرؤساء فقط");
        if (caller.Role == UserRole.SubHead && caller.SectionId is null)
            throw new ArgumentException("حسابك بلا شعبة — أعد الدخول");
        if (caller.BranchId is null && headBranchId is null)
            throw new ArgumentException("الرئيس دون فرع لا يمكنه اعتماد الإنابات");
        return caller;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

