using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الحالات (مستخرجة من `DocumentService` — المرحلة 4): آلة حالات نظام
/// «طالبة تنفيذ» + حالات وضع «منفذ عليه»/«عرض وايداع» + التجديد/الشطب +
/// توريث/استرداد المناب. تنادي تنبيهات الإنابة عبر `IDocumentDelegationService`،
/// والنواة تستدعي كاتبي الوقوعات عبر هذه الواجهة (نواة←حالات — مسموح).
/// </summary>
public interface IDocumentStatusService
{
    Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default, long? version = null);

    /// <summary>
    /// «اعتبار الملف منفذًا كاملًا بهذا البيع» (نظام «طالبة تنفيذ»): إغلاق «منفذ جبريا (منفذ
    /// جزئيا)» الذي فُعّل تلقائيًا بإتمام إنابة إلى «منفذ كاملا» — يُخصم من ملفٍ منفذ جزئيًا
    /// وفيه إنابة منفذة، ويُلزم إدخال «تاريخ تحويل بدل المبيع للجهة العامة» (و«رقم الإشعار»
    /// اختياريًا)، ويُسجَّل وقعة «منفذ جبريا» في وقوعات الملف. حينها فقط يدخل بدل الإنابة
    /// ضمن «إحصاءات منفذ جبريا» مرة واحدة (عبر مسار DelegationSalesAmount القائم).
    /// </summary>
    Task<bool> ConsiderExecutedByDelegationAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default, long? version = null);

    /// <summary>
    /// «التراجع» في نظام «طالبة تنفيذ»: إعادة الملف إلى المتداول من تريث أو منفذ بالتسوية أو منفذ
    /// جبريا بموجب كتاب الجهة العامة بالسير بالملف (رقم وتاريخ الكتاب وورودهما إلزاميان)،
    /// ويُسجَّل وقعة «تراجع» بحقولها في وقوعات الملف.
    /// </summary>
    Task<bool> RevertStatusAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default, long? version = null);

    /// <summary>
    /// العودة من «محال الى البداية» (نقطة «return-referred-to-start») بنتيجتين حسب اللازمة
    /// (§2-10 — بلا عمود جديد): إن حمل الملف «منفذ جزئيا» عاد «منفذ جبريا» مع بقاء عائلة
    /// الجبريا كما هي، وإلا عاد «متداول» مع مسح بقية العائلات. حقول التجديد موحّدة في
    /// المسارين (رقم الملف الجديد يفعّل التجديد)، و«تاريخ الشطب» الاختياري لنتيجة-متداول فقط.
    /// تُسجَّل وقعة «تراجع» بسرد آلي + حقول الشطب/التجديد إن وُجدت، وبلا تنبيهات مرآة عمدًا
    /// (قرار 9 يجعل الإنابة السارية على «محال» مستحيلة).
    /// </summary>
    Task<bool> ReturnFromReferredToStartAsync(int documentId, ReturnReferredToStartRequest request, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// تعيين حالة وضع «منفذ عليه» (ExecutedStatusCatalog): منفذ/مشطوب. عند الشطب يُثبَّت
    /// StruckOffDate (لحظة الشطب UTC) فيُخفى الملف من القوائم والتصدير ويظهر في صفحة المشطوبة.
    /// إعادة الحالة إلى متداول (سلسلة فارغة) تُبقي StruckOffDate محفوظًا لعرضه بعد الإعادة.
    /// </summary>
    Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// تعيين حالة وضع «منفذ عليه» مع حقولها (ExecutedStatusRequest): عند الانتقال إلى «منفذ»
    /// تُحفظ حقوله (المبلغ/كيفية التنفيذ/تاريخ الإيداع)، وعند الانتقال إلى «مشطوب» يُحفظ
    /// تاريخ الشطب المُرسَل إن وُجد وإلا توقيت الانتقال. عند العودة من مشطوب إلى متداول
    /// يُطبَّق بيان التجديد (رقم الملف الجديد إلزامي). إعادة الحالة إلى متداول (سلسلة فارغة)
    /// تُبقي تاريخ الشطب محفوظًا لعرضه بعد الإعادة.
    /// </summary>
    Task<bool> UpdateExecutedStatusAsync(int documentId, string status, ExecutedStatusRequest? request, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// إعادة ملف مشطوب إلى المتداول في وضع «منفذ عليه» (فك الشطب): تُصفَّر ExecutedStatus
    /// وتبقى StruckOffDate محفوظة (تُعرض في تفاصيل الملف بعد الإعادة).
    /// </summary>
    Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// إعادة ملف مشطوب إلى المتداول مع تجديد الملف برقم ملف جديد لسنة الإعادة (إلزامي):
    /// يُطبَّق بيان التجديد ويُسجَّل رقم أساس لسنة الإعادة الحالية فيعود الملف بالرقم والنوع الجديدين.
    /// </summary>
    Task<bool> RestoreStruckOffAsync(int documentId, RenewalRequest renewal, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// تطبيق بيان تجديد الملف المشطوب: رقم الملف الجديد إلزامي (ومعه سنة الإعادة في نظام
    /// «طالبة تنفيذ»)، وتُفسَّر التواريخ النصية الحرة، ويُسجَّل رقم أساس لسنة الإعادة فيعود
    /// الملف بالرقم والنوع الجديدين — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId, CancellationToken ct);

    /// <summary>
    /// تسجيل وقعة الشطب في «وقوعات الملف» عند انتقال ملف «منفذ عليه»/«عرض وايداع»
    /// إلى الحالة «مشطوب» — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct);
}
