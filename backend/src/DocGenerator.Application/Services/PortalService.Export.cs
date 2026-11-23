using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public sealed partial class PortalService : IPortalService
{
    public async Task<Stream> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default, int? entryId = null)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        var entryIds = ResolveEffectiveIds(scope, entryId);

        var total = await _portal.CountScopedAsync(entryIds, query, status, ct);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var docs = await _portal.ExportScopedAsync(entryIds, query, status, ct);

        // عمود «فرع الجهة» مصدره النطاق المضيَّق إلى معرّفات القواعد التي رُشِّحت
        // بها الصفوف فعلًا (`entryIds`)، لا النطاق الكامل: وإلا عرض عمود الفروع
        // فروعًا لم تدخل الصفوف أصلًا فنناقض معلومة التصدير نفسها. (والقائمة تُبقي
        // نطاقها كاملًا لعرضه مختصرًا في البطاقة — قرار يخصّ العرض لا التصدير.)
        var scopedForRows = NarrowScope(scope, entryIds);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var rows = docs
            .Select(row =>
            {
                // الكيان المؤقت من صف القائمة المُسقَط (`P3-04d`) ثم `FromEntity` نفسه —
                // لا نسخة ثانية لقواعد العرض، ولا شجرة تحميل كاملة في التصدير.
                var response = DocumentResponse.FromEntity(row.ToDocumentStub(), currentYear);
                // طبقتان لا واحدة: التحكم الأساسي في التصدير هو **قائمة الأعمدة
                // المغلقة** (`ExcelExportService.PortalColumns` + `BuildPortalValues`)،
                // فحقل داخلي لا يظهر أصلًا وإن لم تحجبْه التنقية؛ والتنقية هنا طبقة
                // دفاع ثانية تُبطل أي حقل محجوب قبل أن يقرأه عمودٌ مستقبلي بالخطأ،
                // وهي اليوم ما يجعل عمود «الإجراءات والملاحظات» علنيًّا خالصًا:
                // أول `ExecutionActions` بعدها هو أحدث إجراء من النوع `action` حتمًا.
                ScrubForPortal(response);
                var matched = BuildMatchedEntries(
                    row.ApplicantPublicEntities.Select(a => a.RegistryId),
                    row.ExecutedPublicEntities.Select(e => e.RegistryId),
                    scopedForRows);
                return new PortalWorkbookRow(response, matched);
            })
            .ToList();

        await _audit.LogAsync(viewerName, "export_entity_portal_excel",
            details: entryId.HasValue
                ? $"صدّر {rows.Count} ملفًا من بوابة الجهة إلى Excel (فرع {entryId.Value})"
                : $"صدّر {rows.Count} ملفًا من بوابة الجهة إلى Excel", ct: ct);

        // مصنّف البوابة عقد منفصل عن مصنّف المدير: «ملحق العقد» و«الإجراءات
        // والملاحظات» غير موجودين فيه بحكم التصميم، و«فرع الجهة» فروع نطاق المندوب
        // لا فرع الإدارة الداخلي.
        return _excel.BuildPortalWorkbook(rows);
    }

    /// <summary>
    /// نسخة من النطاق مقيَّدة بمعرّفات معيّنة (القيود الفعّالة بعد مُرشِّح التصدير
    /// أو القائمة) — تُغذّي `BuildMatchedEntries` فلا يعرض أي مسار فروعًا خارج
    /// ما طلبه الفلتر. النطاق الفارغ أو غير المربوط يُبقي الدالة بلا أثر.
    /// </summary>
}
