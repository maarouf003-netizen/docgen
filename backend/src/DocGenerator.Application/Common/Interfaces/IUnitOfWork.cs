namespace DocGenerator.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// يفصل الكيانات المتتبّعة عن السياق — يُستخدم حصرًا بعد تراجع معاملة فاشلة
    /// لإعادة قراءة الحالة من القاعدة (مسار إعادة محاولة التعاقب)، لا لمسح تعديلات
    /// عمل غير محفوظة.
    /// </summary>
    void ClearChanges();
}
