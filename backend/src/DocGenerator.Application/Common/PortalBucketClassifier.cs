using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// مصنّف سلّات البوابة الوحيد (عدّادات الإحصاءات + فلتر القائمة عبر الثوابت نفسها):
/// حالة العرض أولًا، ثم المسودة بلا حالة، ثم «محال الى البداية».
/// <para>
/// طرف «منفذ عليه/إيداع» تُشتق حالته من <c>ExecutedStatus</c> حصرًا (نفس أسبقية
/// <see cref="DocumentStatusResolver"/>)، وإلا ظهر شاره «منفذ» تحت سلّة «متداول».
/// </para>
/// </summary>
public static class PortalBucketClassifier
{
    public static string Classify(
        bool isDraft,
        string? execStatus,
        string? execSubStatus,
        string? generalEntitySide = null,
        string? executedStatus = null)
    {
        if (GeneralEntitySideCatalog.IsExecutedLike(generalEntitySide))
        {
            if (string.IsNullOrWhiteSpace(executedStatus)) return ExecutionStatusCatalog.StateCirculating;
            return executedStatus == ExecutedStatusCatalog.StruckOff
                ? ExecutionStatusCatalog.StateStruckOff
                : ExecutionStatusCatalog.ExecutedFilter;
        }

        if (!string.IsNullOrEmpty(execStatus))
        {
            if (ExecutionStatusCatalog.IsExecuted(execStatus, execSubStatus))
                return ExecutionStatusCatalog.ExecutedFilter;
            if (execStatus == ExecutionStatusCatalog.Deferred)
                return ExecutionStatusCatalog.Deferred;
            if (execStatus == ExecutionStatusCatalog.ReferredToStart)
                return ExecutionStatusCatalog.ReferredToStart;
            return ExecutionStatusCatalog.StateCirculating;
        }

        return isDraft ? ExecutionStatusCatalog.DraftFilter : ExecutionStatusCatalog.StateCirculating;
    }
}
