using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public sealed partial class PortalService : IPortalService
{
    public async Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default)
    {
        if (!await IsInScopeAsync(_portal, userId, documentId, ct))
            return null;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        // تدقيق دخول جلسة العرض: مرة عند فتح الملف، لا مع كل صفحة/تفصيلة (§4).
        await _audit.LogAsync(viewerName, "view_entity_portal_files", documentId,
            details: "عرض ملف في بوابة الجهة العامة", ct: ct);

        // ق7 سلكيًا لا عرضيًا: كل حقل لا تستهلكه واجهة البوابة (`PortalFileDetail`
        // وكل المشتركات المعروضة فيه — مُدقَّق حقلًا بحقل ضد الاستهلاك الفعلي) يُحجب
        // قبل مغادرة الخادم — الواجهة ليست خط الدفاع الوحيد.
        // المحجوب: الملاحظات الداخلية (`Notes`/`ImmediateActions`), العدّادات
        // التشغيلية (`PrintCount`)، الهوية الإدارية (`AdministrativeBranchName`/
        // `BranchId`)، هوية المنشئ (`CreatedById`/`CreatedByName`)، حقل `Lawyer` الحر،
        // وشارات قوائم المحامين (`NeedsRotation`/`HasAppeals`/`MatchedAppealId`)، ورابط
        // الإنابة الداخلي (`SourceDelegationId` — عرضه عبر نقطة التشعبات المخصصة)،
        // ومعرّفات الأصول المباعة (`SoldAssetIds`)، وتاريخ قرار الإحالة القطعية
        // (`ForcedExecutionDate`)، والتسمية المشتقة (`GeneralEntitySideLabel` —
        // لا يستهلكها العرض)، وختم الحذف (`DeletedAt` — دائم الفراغ هنا أصلًا)،
        // واسم الفرع (`BranchName` — العرض يستدعي `FileDataCard` بـ `showBranch={false}`).
        // المحفوظ قصدًا رغم الشبهة (مستهلك فعلًا في العرض): `Assignments` (ومنها
        // `AssignedByName` — يعرضه `TransferHistoryModal` «أحالها»)، و`ReferredFromLawyer`
        // و`UnderFilingNumber` و`FileIncoming*` و`FileArrival*` و`FileReceipt*` و`ExecutedDescription`
        // (يعرضها `FileDataCard`)، و`ExecutedRequired/Paid*` (يعرضها `ExecutoryDocumentCard`
        // و`FileDataCard`)، و`Baraet/Tarith/Sayer/NoFunds/StartReferral/Renewal/ForcibleTransfer`
        // و`Collected*` (يعرضها `OccurrencesModal` و`StatusCard`)، و`GeneralEntitySide`.
        // أي حقل جديد في `DocumentResponse` بلا تصريف صريح يُفشل اختبار الحارس
        // (`PortalDetailResponse_EveryPropertyHasExplicitDisposition`) — لا تسرب صامت.
        // سياسة الحجب الواحدة (A): الدالة نفسها يستدعيها مسار التفاصيل ومسار
        // التصدير معًا — فلا يتباعدا بصمت كما حدث مع عمود «الفرع».
        var response = DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
        ScrubForPortal(response);

        return response;
    }

    /// <summary>
    /// سياسة حجب البوابة الواحدة: كل حقل لا تستهلكه واجهة البوابة يُصفَّر هنا.
    /// يستدعيها <see cref="GetFileAsync"/> و<see cref="ExportWorkbookAsync"/> معًا —
    /// أي حقل جديد يُحسم هنا مرة واحدة فيغطي التفاصيل والتصدير معًا.
    /// </summary>
    private static void ScrubForPortal(DocumentResponse response)
    {
        // RF-010: عدّاد التزامن بلا معنى لقارئ البوابة (لا سطح كتابة فيها) — فيُصفَّر مع البقية.
        response.Version = 0;
        response.Notes = null;
        response.ImmediateActions = null;
        response.PrintCount = 0;
        response.AdministrativeBranchName = null;
        response.BranchId = null;
        response.CreatedById = 0;
        response.CreatedByName = null;
        response.Lawyer = null;
        response.NeedsRotation = false;
        response.HasAppeals = false;
        response.MatchedAppealId = null;
        response.SourceDelegationId = null;
        response.SoldAssetIds = new List<int>();
        response.ForcedExecutionDate = null;
        response.GeneralEntitySideLabel = null;
        response.DeletedAt = null;
        // `BranchName` هو فرع الإدارة الداخلي (نص حر يكتبه المحامي) ولا تُعرض منه
        // واجهة البوابة ولا مصنّف تصديرها — فيُحجب في المسارين. أما ما تعرضه البوابة
        // من فروع فهو فروع *نطاق المندوب* (`MatchedEntries` في
        // `PortalScopeEntryDto`)، وهي بيانات نطاقات لا حقول هذا الملف — فلا يمسّها
        // هذا الحجب أصلًا. (`FileDataCard` يُستدعى هنا بـ `showBranch={false}`.)
        response.BranchName = null;
        // سجل الدوائر: المعرف المرجعي داخلي (لا منتقي دوائر في البوابة) فيُحجب،
        // وعلم «بانتظار إعادة القيد» سير عمل داخلي للمحامي فيُصفَّر (البوابة قرائية).
        // والشعبة المالكة ومعرفها داخليان للنطاق (§5) فيُحجبان مع الدائرة.
        response.ExecutionCircuitId = null;
        response.SectionId = null;
        response.SectionName = null;
        response.NeedsRegistration = false;
        // بطاقة «تذكيرات الشطب» داخلية للمحامي: تاريخ آخر نشاط الملف ومصدره لا يُعرضان
        // في البوابة القرائية (لا مستهلك لها في واجهة المندوب).
        response.LastActivityDate = null;
        response.LastActivitySource = null;
        response.ExecutionActions = response.ExecutionActions
            .Where(a => a.Type == "action")
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a with { ReminderDuration = null, ReminderColor = null })
            .ToList();
    }

    /// <summary>
    /// معرّفات الفروع الفعّالة: كامل النطاق افتراضيًا، أو الفرع المختار وحده بعد التحقق
    /// أنه ضمن نطاق المندوب (تقاطع لا توسيع — خارج النطاق يُرفض).
    /// </summary>
    internal static List<int> ResolveEffectiveIds(PortalScopeResolution? scope, int? entryId)
    {
        var ids = scope?.EntryIds?.ToList() ?? new List<int>();
        if (!entryId.HasValue)
            return ids;
        if (!ids.Contains(entryId.Value))
            throw new UnauthorizedAccessException("الفرع المختار خارج نطاقك");
        return new List<int> { entryId.Value };
    }

    internal static async Task<bool> IsInScopeAsync(
        IPortalRepository portal, int userId, int documentId, CancellationToken ct)
    {
        var scope = await portal.ResolveForUserAsync(userId, ct);
        return await portal.IsDocumentInScopeAsync(documentId, scope?.EntryIds ?? new List<int>(), ct);
    }

    /// <summary>ملف داخل نطاق المندوب (لا يكشف الوجود خارج النطاق) — null عند خروجه أو غيابه.</summary>
    internal static async Task<Document?> GetScopedDocumentAsync(
        IPortalRepository portal, IRepository<Document> documents, int userId, int documentId, CancellationToken ct)
    {
        if (!await IsInScopeAsync(portal, userId, documentId, ct))
            return null;

        return await documents.GetByIdAsync(documentId, ct);
    }

    /// <summary>
    /// فروع النطاق المطابقة للملف (تقاطع RegistryIds الثلاثة مع معرّفات النطاق) —
    /// تُغذّي السطر الثاني «فرع الجهة العامة» في بطاقة الملف (مثال: المصرف التجاري
    /// — اللاذقية/فرع 1)، وعمود «فرع الجهة» في مصنّف تصدير البوابة.
    ///
    /// مصدران يتشاركان هذه الدالة، فقواعد إلزامية على أي مراجعة:
    /// (1) لا تُقرأ `Registry.*` إطلاقًا (لا `ThenInclude` في `ExportScopedAsync`
    /// ولا تحميل كسول) — المرجع `RegistryId` والمدى `scope.Entries` وحدهما؛
    /// (2) لا يخرج عن نطاق المندوب أبدًا، فالبوابة عاجزة بنيويًا عن عرض فرع جهة
    /// أخرى (فرع إداري أو فرع طرف ثالث) مهما بلغ عدد أطراف الملف.
    /// </summary>
    internal static IReadOnlyList<PortalScopeEntryDto> BuildMatchedEntries(PortalListRow d, PortalScopeResolution? scope)
        => BuildMatchedEntries(
            d.ApplicantEntities.Select(a => a.RegistryId),
            d.ExecutedEntities.Select(e => e.RegistryId),
            scope);

    internal static IReadOnlyList<PortalScopeEntryDto> BuildMatchedEntries(
        IEnumerable<int?> applicantRegistryIds,
        IEnumerable<int?> executedRegistryIds,
        PortalScopeResolution? scope)
    {
        if (scope is null)
            return Array.Empty<PortalScopeEntryDto>();
        var byId = scope.Entries.ToDictionary(e => e.Id);
        var matchedIds = new HashSet<int>();
        foreach (var registryId in applicantRegistryIds)
            if (registryId.HasValue && byId.ContainsKey(registryId.Value))
                matchedIds.Add(registryId.Value);
        foreach (var registryId in executedRegistryIds)
            if (registryId.HasValue && byId.ContainsKey(registryId.Value))
                matchedIds.Add(registryId.Value);
        return matchedIds
            .Select(id => byId[id])
            // الترتيب العربي الوحيد نفسه (`PortalScopeOrdering`): خلية التصدير
            // وبطاقة الملف وقائمة الفرع ثلاثتها بترتيب واحد لا ثلاثة.
            .OrderBy(e => e.Governorate, PortalScopeOrdering.ArabicDisplay)
            .ThenBy(e => e.BranchName, PortalScopeOrdering.ArabicDisplay)
            .Select(e => new PortalScopeEntryDto(e.Id, e.Governorate, e.BranchName, e.IsActive))
            .ToList();
    }
}
