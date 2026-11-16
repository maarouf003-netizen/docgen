namespace DocGenerator.Api.Authorization.Documents;

/// <summary>
/// العملية المطلوبة على مستند واحد — تُقيَّم كمتطلب موردي واحد
/// (<see cref="DocumentAccessRequirement"/>) بدل فحص دور/ملكية متفرق في المتحكم.
///
/// <para>
/// <c>Read</c>/<c>ReadOrFollow</c> تفحصان النطاق فقط (مالك/فرع/شعبة/استثناء الإحالة)،
/// وبقية الأفعال تضيف قدرة الكتالوج المقررة: التعديل والمتدخلات، تغيير الحالة،
/// الحذف والاستعادة، التدوير، إدارة إجراءات التنفيذ، والنقل بين المحامين.
/// </para>
/// </summary>
public enum DocumentAction
{
    Read,
    ReadOrFollow,
    Edit,
    ChangeStatus,
    Delete,
    Restore,
    Rotate,
    ManageActions,
    Transfer,
}
