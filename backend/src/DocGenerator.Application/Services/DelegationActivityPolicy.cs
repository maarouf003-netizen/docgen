using System.Linq.Expressions;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// المصدر الوحيد لسياسة «سارية/حاجبة/نهائية» للإنابات (D2): يفصل تعريف «السارية»
/// الواسع (أي حال سوى «منفذ إنابة» — لحارس الشطب S1) عن تعريف «الحاجبة» الأضيق
/// (السارية بمناب غير نهائي — لحارس التسطير C) عن «نهائية المناب» (للعرض والتحرر).
/// تنبيه تسمية: «منفذ إنابة» لفظٌ لمعنيين مختلفين — <see cref="DelegationStatusCatalog.Executed"/>
/// (حالة الإنابة نفسها) مقابل <see cref="ExecutionStatusCatalog.DelegationExecuted"/> (حالة
/// الملف المناب) — لذا تُلزم الثوابت حرفيًا هنا ولا يُعتمد اللفظ في أي فحص.
/// </summary>
public static class DelegationActivityPolicy
{
    /// <summary>
    /// شرط «حاجبة» كشجرة تعبير يفهمها `EF` — مصدرٌ واحد للدلالة يُستعمل في الذاكرة
    /// (‏<see cref="IsAssetBlocking"/>) وفي استعلام حذف الحجوزات اليتيمة على القاعدة.
    /// الكتابة المكررة نصًا في SQL كانت ستُباعد دلالة الحارس عن دلالة التنظيف.
    /// </summary>
    public static readonly Expression<Func<DocumentDelegation, bool>> IsAssetBlockingExpression =
        d => d.Status != DelegationStatusCatalog.Executed
            && (d.TargetDocument == null
                || !(d.TargetDocument.ExecStatus == ExecutionStatusCatalog.StateStruckOff
                    || d.TargetDocument.ExecStatus == ExecutionStatusCatalog.Recovered
                    || d.TargetDocument.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                    || d.TargetDocument.ExecutedStatus == ExecutedStatusCatalog.StruckOff));

    private static readonly Func<DocumentDelegation, bool> IsAssetBlockingCompiled =
        IsAssetBlockingExpression.Compile();

    /// <summary>هل الإنابة «سارية» بمعنى S1 الواسع؟ — أي حال سوى المنفذة (بما فيها
    /// «بانتظار رئيس القسم» عمدًا: اعتماد الإنابة لا يعيد فحص المنيب).</summary>
    public static bool IsLifecycleActive(DocumentDelegation d) =>
        d.Status != DelegationStatusCatalog.Executed;

    /// <summary>هل تحجب الإنابة أموالها عن أي تسطير جديد؟ — السارية ما دام منابها
    /// غير نهائي؛ المعلّقة بلا مناب حاجبة دائمًا (الأموال محجوزة منذ التسطير).</summary>
    public static bool IsAssetBlocking(DocumentDelegation d) => IsAssetBlockingCompiled(d);

    /// <summary>نهائية المناب المُحرِّرة لأمواله (تعود للظهور في التسطير الجديد):
    /// مشطوب بجهتيه (طالبة تنفيذ/طالبة منفذة)، مسترد، منفذ إنابة.
    /// الدرافت («محالة») غير نهائي — يحجب.</summary>
    public static bool IsTargetTerminal(Document target) =>
        target.ExecStatus == ExecutionStatusCatalog.StateStruckOff
        || target.ExecStatus == ExecutionStatusCatalog.Recovered
        || target.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
        || ExecutedStatusCatalog.IsStruckOff(target.ExecutedStatus);
}
