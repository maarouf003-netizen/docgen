using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// المصدر الوحيد لاشتقاق حالة العرض الموحدة للملف (منفذ/تريث/تحت رفع/متداول/
/// متداول / منفذ جزئيا/مسترد/مشطوب/محال الى البداية) من حقول الحالة الخام — تستهلكه
/// الاستجابة للواجهة وخدمة تصدير Excel معًا، فلا تتكرر القواعد في طرفين.
/// </summary>
public static class DocumentStatusResolver
{
    public static string Resolve(IDocumentExecutionState doc)
    {
        // عائلة وضع «الجهة العامة منفذ عليها» (Executed + Deposit): حالتها من ExecutedStatus
        // (متداول/منفذ/مشطوب)، معزولة تمامًا عن نظام «طالبة تنفيذ».
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            if (string.IsNullOrWhiteSpace(doc.ExecutedStatus)) return "متداول";
            return doc.ExecutedStatus == ExecutionStatusCatalog.StateStruckOff ? "مشطوب" : "منفذ";
        }

        if (doc.ExecStatus == ExecutionStatusCatalog.StateStruckOff) return "مشطوب";
        // «محال الى البداية» حالة عرض مستقلة (الخطة RTS — له نقطة دخول/عودة مخصصتان ولا يُطوى
        // في «متداول» كي تظهر شارتُه وفلاتره وعدّاداته، ولا في «منفذ» وإن حمل جزئية:
        // حالة «إحالة» معلقة لا تنفيذَ فيها فعليًا (بلا أموال للتنفيذ عليها).
        if (doc.ExecStatus == ExecutionStatusCatalog.ReferredToStart) return "محال الى البداية";
        // «مسترد» (المناب الذي استُرد إلى الدائرة المنيبة) حالة عرض مستقلة (الخطة R1/L3) —
        // لا تُطوى في «منفذ» كي تظهر شارتُها في البطاقة والمنفذة والتصدير.
        if (doc.ExecStatus == ExecutionStatusCatalog.Recovered) return "مسترد";
        if (doc.ExecStatus == ExecutionStatusCatalog.Deferred) return "تريث";
        if (doc.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
            && doc.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted) return "متداول / منفذ جزئيا";
        if (doc.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
            || doc.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
            || doc.ExecStatus == ExecutionStatusCatalog.DelegationExecuted) return "منفذ";
        return doc.IsDraft ? "تحت رفع" : "متداول";
    }
}
