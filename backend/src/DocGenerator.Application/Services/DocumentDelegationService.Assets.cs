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
    /// <summary>
    /// مواءمة الحجوزات اليتيمة (B3 — شفاء ذاتي في نقطة واحدة): تحذف صفوف الحجوزات التي
    /// لم تعد لإنابة حاجبة (منفذة/مناب نهائي) قبل الفحص — فلا يحتاج أي مسار تحرر
    /// (إتمام/استرداد/شطب-مناب/حذف) خطافًا خاصًا قد يُنسى (عين علة التنبيهات D3)، وأي
    /// انحراف (طفر مباشر، فشل لاحق الالتزام) يُشفى عند أول فحص لاحق. الحذف والفحص
    /// والإدراج اللاحق في معاملة واحدة فلا تسمم متقاطع.
    ///
    /// <para>الحكم على «حاجبة» يتم داخل جملة الحذف من حالة القاعدة اللحظية، لا من لقطة
    /// <c>source</c> — وإلا محا سباقٌ متزامن حجز إنابةٍ التزمت بعد قراءة اللقطة
    /// (واقع `READ COMMITTED` على `Postgres`).</para>
    /// </summary>
    private async Task PruneStaleReservationsAsync(
        Document source, CancellationToken ct)
        => await _reservations.DeleteStaleAsync(source.Id, ct);

    /// <summary>حارس التسطير عند الإنشاء/التعديل: يرفض الكفالة (لا إنابة عليها أبدًا)
    /// ويرفض الأموال بلا تاريخ القاء حجز (لا إنابة عليها قانونًا) ويرفض الأموال
    /// المحجوبة بإنابة سارية أخرى — مسح اللقطات حصرًا (مطابقة واحد-لواحد بالنوع والوصف —
    /// مرآة matchDelegationAssets الأمامية — فالتوأم السليم لا يُحجب). ترتيب الرسائل:
    /// كفالة ← حجز ← حجب. صفوف الحجوزات (B3)
    /// عمدًا خارج الحارس: مرجعها فيزيائي والمسح وصفي، واتحادهما يختلف على أي توأم
    /// محجوب فيحجب التوأمين معًا — القيد الفريد وحده يحسم السباق عند الكتابة.
    /// تُستثنى الإنابة ذاتها عند التعديل. يعمل على قراءة طازجة بلا تتبع
    /// (B2) — لا يُمرَّر له كيان متتبع هنا أبدًا.</summary>
    private static Task ValidateAssetsNotBlockedAsync(
        Document source, List<int>? assetIds, int? excludeDelegationId, CancellationToken ct)
    {
        var ids = (assetIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return Task.CompletedTask; // الفراغ يرفضه ApplyDelegationAssets برسالتها («يجب اختيار الأموال موضوع الإنابة»).
        var selected = source.Assets.Where(a => ids.Contains(a.Id)).ToList();
        if (selected.Any(a => a.AssetKind == AssetKindCatalog.SalaryGuarantee))
            throw new ArgumentException("كفالة الرواتب لا يجري عليها إنابة — أزلها من الأموال موضوع الإنابة");
        // قاعدة «لا إنابة على مالٍ بلا حجز»: تُفحص بعد الكفالة وقبل الحجب.
        ValidateAssetsHaveSeizureDate(source, assetIds);
        var blockedIds = ComputeBlockedIds(source, excludeDelegationId);
        if (blockedIds.Count == 0)
            return Task.CompletedTask;
        var hit = selected.Where(a => blockedIds.Contains(a.Id)).ToList();
        if (hit.Count > 0)
            throw new ArgumentException(BlockedAssetsMessage(hit));
        return Task.CompletedTask;
    }

    /// <summary>حارس «لا إنابة على مالٍ بلا حجز»: يرفض أي أصلٍ مختار بلا تاريخ القاء حجز
    /// مخزَّن (الفارغ/الأبيض في الطلب يؤول إلى null في BuildAsset، فالمقياس هنا null حصريًا).
    /// فاحص مستقل يُستدعى من حارس التسطير بعد فحص الكفالة وقبل فحص الحجب.</summary>
    private static void ValidateAssetsHaveSeizureDate(Document source, List<int>? assetIds)
    {
        var ids = (assetIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return; // الفراغ يرفضه ApplyDelegationAssets برسالتها («يجب اختيار الأموال موضوع الإنابة»).
        var missing = source.Assets
            .Where(a => ids.Contains(a.Id) && a.SeizureDate is null)
            .ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"يرجى ادخال تاريخ القاء الحجز لتسطير الانابة: {string.Join("، ", missing.Select(AssetDisplay.Label))}");
    }

    /// <summary>
    /// ربط اللقطات بالأصول الفيزيائية (B3/C1): مطابقة واحد-لواحد جشعة على مسبح مرتب
    /// بالمفتاح — اللقطة تستهلك أول أصل حر مطابق. حتمية تامة لمدخل ثابت (بما فيه التوائم).
    /// تُستخدم للحارس (لقطات الحواجب) ولاشتقاق صفوف الحجز للقطات الجديدة — اللغة
    /// المشتركة الوحيدة بين المرجع الوصفي والمرجع الفيزيائي (التوائم قابلة للتبادل
    /// ولا تحمل اللقطات معرفاتها، فأي اختلاف بينهما يحجب توأمًا سليمًا).
    /// </summary>
    private static HashSet<int> ComputeBlockedIds(Document source, int? excludeDelegationId) =>
        ComputeBlockedIds(source.Assets, source.Delegations, excludeDelegationId);

    /// <summary>المطابقة نفسها على مجموعات مفكوكة — لقفل «الحجز بعد التسطير» في
    /// DocumentService حيث تُجلب الإنابات مع مناباتها (لحالاتها فقط) بقراءة مستقلة.</summary>
    internal static HashSet<int> ComputeBlockedIds(
        IEnumerable<Asset> assets, IEnumerable<DocumentDelegation> delegations, int? excludeDelegationId)
    {
        // مطابقة واحد-لواحد على كامل أموال المنيب (لا على المحدد فقط): لقطة الحاجب تستهلك
        // توأمًا واحدًا من المسبح فيبقى التوأم السليم حرًا.
        // C1: الفرز بالمفتاح قبل الجشع — بدونه يعتمد أي توأم يُستهلك على ترتيب التحميل.
        var remaining = assets.OrderBy(a => a.Id).ToList();
        var blockedIds = new HashSet<int>();
        var blockers = delegations
            .Where(d => (excludeDelegationId is null || d.Id != excludeDelegationId) && DelegationActivityPolicy.IsAssetBlocking(d))
            .OrderBy(d => d.Id)
            .ToList();
        foreach (var blocker in blockers)
            ConsumeSnapshots(remaining, blocker.Assets.OrderBy(s => s.Id), blockedIds);
        return blockedIds;
    }

    /// <summary>استهلاك لقطات من المسبح (تزيل المستهلك وتسجله) — يُرجع المعرفات المستهلكة.</summary>
    private static List<int> ConsumeSnapshots(
        List<Asset> remaining, IEnumerable<DelegationAsset> snapshots, HashSet<int> blockedIds)
    {
        var consumed = new List<int>();
        foreach (var snap in snapshots)
        {
            var match = remaining.FirstOrDefault(a =>
                !blockedIds.Contains(a.Id)
                && a.AssetKind == snap.AssetKind
                && AssetDisplay.Label(a) == snap.AssetLabel);
            if (match is null)
                continue;
            blockedIds.Add(match.Id);
            remaining.Remove(match);
            consumed.Add(match.Id);
        }
        return consumed;
    }

    /// <summary>رسالة الحجب الموحدة (الحارس وترجمة تعارض القيد في B3).</summary>
    private static string BlockedAssetsMessage(Document source, List<int>? assetIds)
    {
        var ids = (assetIds ?? new List<int>()).Distinct().ToList();
        return BlockedAssetsMessage(source.Assets.Where(a => ids.Contains(a.Id)).ToList());
    }

    private static string BlockedAssetsMessage(List<Asset> hit) =>
        $"الأموال التالية عليها إنابة سارية: {string.Join("، ", hit.Select(AssetDisplay.Label))}";
}
