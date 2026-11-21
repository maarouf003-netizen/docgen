using System.Linq.Expressions;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// باني تعبير بحث الأطراف الاسمي — المصدر الوحيد لكتل البحث النصي في:
/// <list type="bullet">
/// <item>قوائم المحامين (الكتلة الموحدة وكتلة «محال الى البداية»)،</item>
/// <item>بوابة مندوب الجهة.</item>
/// </list>
/// الفروق بين المسارات معلنة بأعلام صريحة، لا بنسخ يدوية تتباعد مع الوقت.
///
/// <para><b>ملاحظات دلالية ثابتة:</b>
/// <list type="bullet">
/// <item>`SearchText` مطبَّع، أما الحقول المباشرة فتُطابق على النص الخام (PB-001).</item>
/// <item>فروع عائلة «منفذ عليه» (طالب التنفيذ/الطبيعي/الجهة العامة) تُستبعد في صفحة
/// «محال الى البداية» فقط (`includeExecutedFamily: false`) — تضييق معتمد 2026-09-24.</item>
/// <item>ورثة المنفذ عليه (`ExecutedHeirs`) يبقون في كل المسارات عمدًا: سلوك قائم
/// قبل التوحيد (ج) وإزالته انحدار خارج نطاق التضييق.</item>
/// <item>لقطات الاستئناف تُبحث مباشرة في قوائم المحامين (`includeAppeals: true`)،
/// بينما تعتمد البوابة على `SearchText` وحده لهذا البند (`includeAppeals: false`).</item>
/// </list></para>
///
/// <para><b>P3-02:</b> بقي مسند `SearchText`/المقترض على `Documents` كما هو (مسار واحد)،
/// أما فروع جداول الأطراف (التي كانت `OR` متكررًا مع `EXISTS`) فصارت **مجموعات مرشّحين
/// مستقلة** تُجمع بـ`UNION ALL` (`Concat`) على `DocumentId` فقط، ثم تُضاف للمسند
/// بمساواة واحدة. هذا يقلّل مسح `Documents` المتكرر ويُبقي النطاق (المالك/الفرع/الحالة)
/// مطبَّقًا مرة واحدة عبر `q`، فلا يتوسّع البحث خارج نطاق المستدعي.</para>
/// </summary>
internal static class DocumentPartySearch
{
    public static IQueryable<Document> Apply(
        IQueryable<Document> q,
        DocGeneratorDbContext db,
        string term,
        bool includeExecutedFamily,
        bool includeAppeals)
    {
        var normTerm = ArabicNameNormalizer.Normalize(term);

        // المسار الأساسي على `Documents` يبقى مسندًا واحدًا: `SearchText` (مطبَّع، مدعوم
        // بفهرس GIN ثلاثي) أو اسم المقترض الخام. تكراره لكل فرع يمسح الجدول عشر مرات.
        Expression<Func<Document, bool>> basePredicate = d =>
            (d.SearchText != null && d.SearchText.Contains(normTerm)) ||
            (d.BorrowerName != null &&
                ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                 (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term)));

        var childIds = new List<IQueryable<int>>();

        if (includeExecutedFamily)
        {
            childIds.Add(db.ExecutionApplicants.AsNoTracking()
                .Where(a =>
                    (a.Name != null &&
                        ((a.Name + " " + (a.Family ?? string.Empty)).Contains(term) ||
                         (a.Name + " " + (a.Father ?? string.Empty) + " " + (a.Family ?? string.Empty)).Contains(term))) ||
                    ((a.DeceasedName ?? string.Empty) + " " + (a.DeceasedFamily ?? string.Empty)).Contains(term))
                .Select(a => a.DocumentId));
            childIds.Add(db.ExecutedPublicEntities.AsNoTracking()
                .Where(e => e.EntityName != null && e.EntityName.Contains(term))
                .Select(e => e.DocumentId));
            childIds.Add(db.ExecutedNaturalPersons.AsNoTracking()
                .Where(p => p.Name != null &&
                    ((p.Name + " " + (p.Family ?? string.Empty)).Contains(term) ||
                     (p.Name + " " + (p.Father ?? string.Empty) + " " + (p.Family ?? string.Empty)).Contains(term)))
                .Select(p => p.DocumentId));
        }

        childIds.Add(db.ExecutedHeirs.AsNoTracking()
            .Where(h => h.HeirName != null &&
                ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                 (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term)))
            .Select(h => h.DocumentId));
        childIds.Add(db.ApplicantPublicEntities.AsNoTracking()
            .Where(a => a.Name != null && a.Name.Contains(term))
            .Select(a => a.DocumentId));
        childIds.Add(db.Guarantors.AsNoTracking()
            .Where(g => g.GuarantorName != null &&
                ((g.GuarantorName + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term) ||
                 (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term)))
            .Select(g => g.DocumentId));
        childIds.Add(db.Heirs.AsNoTracking()
            .Where(h => h.HeirName != null &&
                ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                 (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term)))
            .Select(h => h.DocumentId));
        childIds.Add(db.DocumentInterventions.AsNoTracking()
            .Where(i =>
                (i.Name != null &&
                    ((i.Name + " " + (i.Family ?? string.Empty)).Contains(term) ||
                     (i.Name + " " + (i.Father ?? string.Empty) + " " + (i.Family ?? string.Empty)).Contains(term))) ||
                (i.EntityName != null && i.EntityName.Contains(term)) ||
                i.Heirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))))
            .Select(i => i.DocumentId));

        if (includeAppeals)
        {
            childIds.Add(db.DocumentAppeals.AsNoTracking()
                .Where(a =>
                    (a.AppellantsJson != null && a.AppellantsJson.Contains(term)) ||
                    (a.AppelleesJson != null && a.AppelleesJson.Contains(term)))
                .Select(a => a.DocumentId));
        }

        // P3-02: `UNION ALL` صريح (`Concat`) على معرّفات جداول الأطراف فقط.
        var union = childIds[0];
        for (var i = 1; i < childIds.Count; i++)
            union = union.Concat(childIds[i]);

        // يُضاف المسند الأساسي مرة واحدة؛ النطاق يُطبَّق عبر `q` نفسه.
        var childPredicate = (Expression<Func<Document, bool>>)(d => union.Contains(d.Id));
        return q.Where(Or(basePredicate, childPredicate));
    }

    /// <summary>يجمع شرطين بـ`OR` بعد توحيد معامل المستند — بلا مكتبات خارجية.</summary>
    private static Expression<Func<Document, bool>> Or(
        Expression<Func<Document, bool>> left,
        Expression<Func<Document, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Document), "d");
        var body = Expression.OrElse(
            new ParameterReplacer(left.Parameters[0], parameter).Visit(left.Body)!,
            new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body)!);
        return Expression.Lambda<Func<Document, bool>>(body, parameter);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
