using System.Linq.Expressions;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// إسقاط صفوف القوائم المشترك (`P3-04b`/`P3-04c`/`P3-04d`): تعبير `Select` واحد
/// وتحمل العناصر الفرعية الدفعية — يستخدمه مستودعا الملفات والبوابة، فلا يتباعد
/// شكل صف قائمة عن آخر، ولا يُنسخ منطق القراءات الدفعية.
/// </summary>
internal static class DocumentListRowProjection
{
    /// <summary>
    /// حقول صف القائمة: السطحية + تواريخ العرض الخاصة بالأرشيف، بلا `Include` للشجرة
    /// الكاملة. مصدر واحد يمنع تباعد أشكال القوائم.
    /// </summary>
    public static readonly Expression<Func<Document, DocumentListRow>> Selector =
        d => new DocumentListRow
        {
            Id = d.Id,
            Version = d.Version,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            DocumentType = d.DocumentType,
            IsDraft = d.IsDraft,
            BorrowerName = d.BorrowerName,
            BorrowerFather = d.BorrowerFather,
            BorrowerFamily = d.BorrowerFamily,
            Applicant = d.Applicant,
            Lawyer = d.Lawyer,
            Court = d.Court,
            ExecutionCircuitId = d.ExecutionCircuitId,
            GeneralEntitySide = d.GeneralEntitySide,
            ExecStatus = d.ExecStatus,
            ExecSubStatus = d.ExecSubStatus,
            ExecutedStatus = d.ExecutedStatus,
            FileNumber = d.FileNumber,
            FileType = d.FileType,
            FileYear = d.FileYear,
            BranchName = d.BranchName,
            SourceDelegationId = d.SourceDelegationId,
            AdministrativeBranchName = d.Branch != null ? d.Branch.Name : null,
            DeletedAt = d.DeletedAt,
            StruckOffDate = d.StruckOffDate,
            ExecutedExecutionDate = d.ExecutedExecutionDate,
            ExecutedDepositDate = d.ExecutedDepositDate,
            ForcedExecutionDate = d.ForcedExecutionDate,
            BaraetDate = d.BaraetDate,
            StartReferralDate = d.StartReferralDate,
        };

    /// <summary>
    /// استكمال صفوف القائمة بعناصرها الفرعية دفعةً واحدة لمعرّفات الصفحة: أرقام الأساس،
    /// تاريخ القيد، الإجراءات، ملخصات الأطراف، وتاريخ إعادة الإنابة — ثم تُغذّي
    /// `DocumentResponse.FromEntity` على كيان مُجمَّع في الخدمة بلا نسخة ثانية من القواعد.
    /// </summary>
    public static async Task LoadChildrenAsync(
        DocGeneratorDbContext db,
        List<DocumentListRow> items,
        CancellationToken ct,
        bool includeDeletedChildren = false)
    {
        if (items.Count == 0)
            return;

        var ids = items.Select(i => i.Id).ToList();

        // قائمة المحذوفات تقرأ مستندات متجاوزةً فلتر `!IsDeleted`؛ ولأن كل جداول الأبناء
        // تحمل فلترًا يستثني أبناء المحذوف، يجب تجاوزه هنا أيضًا وإلا فقدت البطاقة
        // أطرافها وأرقام أساسها وآخر نشاطها بخلاف المسار القديم (`IgnoreQueryFilters`
        // على الاستعلام الكامل). القوائم الأخرى تُبقي الفلاتر كما هي.
        IQueryable<T> Scoped<T>(IQueryable<T> q) where T : class
            => includeDeletedChildren ? q.IgnoreQueryFilters() : q;

        var baseNumbers = await Scoped(db.BaseNumbers.AsNoTracking())
            .Where(b => ids.Contains(b.DocumentId))
            .ToListAsync(ct);
        var registrationDates = await Scoped(db.DocumentRegistrationDates.AsNoTracking())
            .Where(r => ids.Contains(r.DocumentId))
            .ToDictionaryAsync(r => r.DocumentId, r => r.DateParsed, ct);
        var actions = await Scoped(db.ExecutionActions.AsNoTracking())
            .Where(a => ids.Contains(a.DocumentId))
            .Include(a => a.CreatedBy)
            .ToListAsync(ct);
        var executedNaturalPersons = await Scoped(db.ExecutedNaturalPersons.AsNoTracking())
            .Where(p => ids.Contains(p.DocumentId))
            .ToListAsync(ct);
        var executedPublicEntities = await Scoped(db.ExecutedPublicEntities.AsNoTracking())
            .Where(e => ids.Contains(e.DocumentId))
            .ToListAsync(ct);
        var executionApplicants = await Scoped(db.ExecutionApplicants.AsNoTracking())
            .Where(a => ids.Contains(a.DocumentId))
            .ToListAsync(ct);
        var applicantPublicEntities = await Scoped(db.ApplicantPublicEntities.AsNoTracking())
            .Where(a => ids.Contains(a.DocumentId))
            .ToListAsync(ct);

        var delegationIds = items
            .Where(i => i.SourceDelegationId.HasValue)
            .Select(i => i.SourceDelegationId!.Value)
            .Distinct()
            .ToList();
        var delegationReturnDates = delegationIds.Count == 0
            ? new Dictionary<int, DateTime?>()
            : await Scoped(db.DocumentDelegations.AsNoTracking())
                .Where(x => delegationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.ReturnDate, ct);

        var baseLookup = baseNumbers.ToLookup(b => b.DocumentId);
        var actionLookup = actions.ToLookup(a => a.DocumentId);
        var executedNaturalLookup = executedNaturalPersons.ToLookup(p => p.DocumentId);
        var executedPublicLookup = executedPublicEntities.ToLookup(e => e.DocumentId);
        var applicantLookup = executionApplicants.ToLookup(a => a.DocumentId);
        var applicantPublicLookup = applicantPublicEntities.ToLookup(a => a.DocumentId);

        foreach (var item in items)
        {
            item.BaseNumbers = baseLookup[item.Id].OrderBy(b => b.Year).ThenBy(b => b.Id).ToList();
            item.ExecutionActions = actionLookup[item.Id].ToList();
            item.ExecutedNaturalPersons = executedNaturalLookup[item.Id].ToList();
            item.ExecutedPublicEntities = executedPublicLookup[item.Id].ToList();
            item.ExecutionApplicants = applicantLookup[item.Id].ToList();
            item.ApplicantPublicEntities = applicantPublicLookup[item.Id].ToList();
            item.RegistrationDate = registrationDates.GetValueOrDefault(item.Id);
            item.DelegationReturnDate = item.SourceDelegationId.HasValue
                ? delegationReturnDates.GetValueOrDefault(item.SourceDelegationId.Value)
                : null;
        }
    }
}
