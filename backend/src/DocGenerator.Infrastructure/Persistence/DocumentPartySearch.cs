using System.Linq.Expressions;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

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
/// <para>تُبنى الفروع المشروطة في شجرة التعبير نفسها (لا كمعاملات `bool` في SQL)،
/// فلا تصل فروع ميتة إلى قاعدة البيانات في مسار لا يحتاجها.</para>
/// </summary>
internal static class DocumentPartySearch
{
    public static IQueryable<Document> Apply(
        IQueryable<Document> q,
        IQueryable<DocumentAppeal> appeals,
        string term,
        bool includeExecutedFamily,
        bool includeAppeals)
    {
        var normTerm = ArabicNameNormalizer.Normalize(term);
        var branches = new List<Expression<Func<Document, bool>>>
        {
            d => d.SearchText != null && d.SearchText.Contains(normTerm),
            d => d.BorrowerName != null &&
                ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                 (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term)),
        };

        if (includeExecutedFamily)
        {
            branches.Add(d => d.ExecutionApplicants.Any(a =>
                (a.Name != null &&
                    ((a.Name + " " + (a.Family ?? string.Empty)).Contains(term) ||
                     (a.Name + " " + (a.Father ?? string.Empty) + " " + (a.Family ?? string.Empty)).Contains(term))) ||
                ((a.DeceasedName ?? string.Empty) + " " + (a.DeceasedFamily ?? string.Empty)).Contains(term)));
            branches.Add(d => d.ExecutedPublicEntities.Any(e =>
                e.EntityName != null && e.EntityName.Contains(term)));
            branches.Add(d => d.ExecutedNaturalPersons.Any(p =>
                p.Name != null &&
                ((p.Name + " " + (p.Family ?? string.Empty)).Contains(term) ||
                 (p.Name + " " + (p.Father ?? string.Empty) + " " + (p.Family ?? string.Empty)).Contains(term))));
        }

        branches.Add(d => d.ExecutedHeirs.Any(h =>
            h.HeirName != null &&
            ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
             (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))));
        branches.Add(d => d.ApplicantPublicEntities.Any(a =>
            a.Name != null && a.Name.Contains(term)));
        branches.Add(d => d.Guarantors.Any(g =>
            g.GuarantorName != null &&
            ((g.GuarantorName + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term) ||
             (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))));
        branches.Add(d => d.Heirs.Any(h =>
            h.HeirName != null &&
            ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
             (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))));
        branches.Add(d => d.Interventions.Any(i =>
            (i.Name != null &&
                ((i.Name + " " + (i.Family ?? string.Empty)).Contains(term) ||
                 (i.Name + " " + (i.Father ?? string.Empty) + " " + (i.Family ?? string.Empty)).Contains(term))) ||
            (i.EntityName != null && i.EntityName.Contains(term)) ||
            i.Heirs.Any(h =>
                h.HeirName != null &&
                ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                 (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term)))));

        if (includeAppeals)
        {
            branches.Add(d => appeals.Any(a =>
                a.DocumentId == d.Id &&
                ((a.AppellantsJson != null && a.AppellantsJson.Contains(term)) ||
                 (a.AppelleesJson != null && a.AppelleesJson.Contains(term)))));
        }

        var predicate = branches.Aggregate(Or);
        return q.Where(predicate);
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
