namespace DocGenerator.Domain.Entities;

/// <summary>
/// عقد موحّد لعدّاد التزامن المتفائل (P0-06): كل كيان جذري قابل للتعديل يحمل
/// <c>long Version</c> يُعرَّف في EF كـ<c>IsConcurrencyToken</c>، فيُضمَّن في
/// <c>WHERE</c> عند الحفظ ويُرفَض التعارض بـ<c>409</c> بدل المحو الصامت.
/// </summary>
/// <remarks>
/// <para>
/// الزيادة مضمونة مركزيًا في <c>DocGeneratorDbContext.SaveChanges</c> (إن لم يغيّرها
/// الكاتب صراحةً)، فحتى مسار كتابة ينسى الزيادة اليدوية لا يفقد الحماية.
/// </para>
/// <para>
/// الحدّ الأدنى للعقد متعمّد: الواجهة لا تعرف كيف يُحسب العدّاد ولا متى يُزاد؛
/// تعرف فقط أن تغيّر القيمة يعني تغيّر الصف.
/// </para>
/// </remarks>
public interface IVersionedEntity
{
    /// <summary>عدّاد التزامن المتفائل — يبدأ من صفر ويُزاد عند كل كتابة.</summary>
    long Version { get; set; }
}
