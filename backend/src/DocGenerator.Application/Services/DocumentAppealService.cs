using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

/// <summary>
/// حرّاس الوصول القرائي لاستئنافات الملفات التنفيذية (متابعة الإسناد والإحالة المفتوحة)،
/// ومساعدات الكتابة المشتركة التي تستدعيها معالجات الأوامر (تحقق النصوص والاتجاه ولقطات
/// الأطراف والتخطيط) — مصدر واحد بلا تكرار. أسطح القراءة/القوائم/العدّ/التاريخ ملكيتها
/// لمعالجات الاستعلام تحت <c>UseCases/Appeals</c>.
/// </summary>
public sealed partial class DocumentAppealService : IDocumentAppealService
{
    private readonly IAppealRepository _appeals;

    public DocumentAppealService(IAppealRepository appeals) => _appeals = appeals;
}
