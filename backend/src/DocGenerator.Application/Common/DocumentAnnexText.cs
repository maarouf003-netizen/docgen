using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع ملاحق السند التنفيذي: الجدول الفرعي هو المصدر، والطلب القديم بلا قائمة
/// يُعامَل كملحق واحد للتوافق الخلفي. الصف الفارغ تمامًا يُتجاهل، والترتيب هو
/// ترتيب الإدخال (Sequence يبدأ من 1).
/// </summary>
public static class DocumentAnnexText
{
    public static List<DocumentAnnex> Build(DocumentUpsertRequest request)
    {
        var source = request.Annexes;
        if (source is not null && source.Count > 0)
        {
            var rows = new List<DocumentAnnex>();
            foreach (var a in source)
            {
                if (a is null) continue;
                var type = (a.AnnexType ?? string.Empty).Trim();
                var number = (a.AnnexNumber ?? string.Empty).Trim();
                var date = (a.AnnexDate ?? string.Empty).Trim();
                if (type.Length == 0 && number.Length == 0 && date.Length == 0) continue;
                rows.Add(new DocumentAnnex
                {
                    Sequence = rows.Count + 1,
                    AnnexType = type,
                    AnnexNumber = number,
                    AnnexDate = date,
                });
            }
            return rows;
        }

        // توافق خلفي: الطلب القديم بلا قائمة يُعامَل كملحق واحد.
        var legacyType = (request.AnnexType ?? string.Empty).Trim();
        var legacyNumber = (request.AnnexNumber ?? string.Empty).Trim();
        var legacyDate = (request.AnnexDate ?? string.Empty).Trim();
        if (legacyType.Length == 0 && legacyNumber.Length == 0 && legacyDate.Length == 0)
            return new List<DocumentAnnex>();

        return new List<DocumentAnnex>
        {
            new()
            {
                Sequence = 1,
                AnnexType = legacyType,
                AnnexNumber = legacyNumber,
                AnnexDate = legacyDate,
            },
        };
    }

    public static bool ListsEqual(IReadOnlyList<DocumentAnnex> requested, IReadOnlyList<DocumentAnnex> stored)
    {
        if (requested.Count != stored.Count) return false;
        for (var i = 0; i < requested.Count; i++)
        {
            var a = requested[i];
            var b = stored[i];
            if (a.Sequence != b.Sequence) return false;
            if (!string.Equals(a.AnnexType, b.AnnexType, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.AnnexNumber, b.AnnexNumber, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.AnnexDate, b.AnnexDate, StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
