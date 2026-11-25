using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── بناء لقطات الأطراف ────────────────────────────────────────────────

    internal static string ParseDirection(string? value)
    {
        var direction = value?.Trim() ?? string.Empty;
        if (!AppealDirectionCatalog.ValidDirections.Contains(direction))
            throw new ArgumentException("اتجاه الاستئناف غير صالح — اختر: مستأنِفين أو مستأنف علينا");
        return direction;
    }

    /// <summary>خيارات «المستأنف» بحسب الاتجاه وصفة الملف:
    /// مستأنِفين ← الجهات العامة طالبة التنفيذ فقط؛
    /// مستأنف علينا ← المنفذ عليهم (طبيعيون/جهات/ورثة) في وضع «منفذ عليه»،
    /// والمقترض والكفلاء والورثة في وضع «طالبة تنفيذ».</summary>
    private static List<PartyOption> BuildOptions(Document doc, string direction)
    {
        var options = new List<PartyOption>();
        var executedLike = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide);

        if (direction == AppealDirectionCatalog.Appellants)
        {
            if (executedLike)
            {
                foreach (var e in doc.ExecutionApplicants)
                    options.Add(new PartyOption("execution-applicant", e.Id, TripleOr(e.Name, e.Father, e.Family, e.LegalRepresentative)));
            }
            else
            {
                foreach (var e in doc.ApplicantPublicEntities)
                    options.Add(new PartyOption("applicant-entity", e.Id, Single(e.Name)));
            }
            return options;
        }

        if (executedLike)
        {
            foreach (var p in doc.ExecutedNaturalPersons)
                options.Add(new PartyOption("executed-natural", p.Id, TripleOr(p.Name, p.Father, p.Family, null)));
            foreach (var e in doc.ExecutedPublicEntities)
                options.Add(new PartyOption("executed-public", e.Id, Single(e.EntityName)));
            foreach (var h in doc.ExecutedHeirs)
                options.Add(new PartyOption("executed-heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
        }
        else
        {
            options.Add(new PartyOption("borrower", doc.Id, TripleOr(doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily, doc.BorrowerRepresentativeName)));
            foreach (var g in doc.Guarantors)
                options.Add(new PartyOption("guarantor", g.Id, TripleOr(g.GuarantorName, g.GuarantorFather, g.GuarantorFamily, null)));
            foreach (var h in doc.Heirs)
                options.Add(new PartyOption("heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
        }
        return options;
    }

    /// <summary>سجل كامل لأطراف الملف من الجهتين — أساس خانة «المستأنف عليهم»:
    /// كل أطراف الملف ناقص المختارين ضمن المستأنف، لأن الاستئناف يكون بمواجهة الجميع حكمًا.</summary>
    private static List<PartyOption> BuildAllParties(Document doc)
    {
        var executedLike = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide);
        var parties = new List<PartyOption>();

        if (executedLike)
        {
            foreach (var a in doc.ExecutionApplicants)
                parties.Add(new PartyOption("execution-applicant", a.Id, TripleOr(a.Name, a.Father, a.Family, a.LegalRepresentative)));
            foreach (var p in doc.ExecutedNaturalPersons)
                parties.Add(new PartyOption("executed-natural", p.Id, TripleOr(p.Name, p.Father, p.Family, null)));
            foreach (var h in doc.ExecutedHeirs)
                parties.Add(new PartyOption("executed-heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
            foreach (var e in doc.ExecutedPublicEntities)
                parties.Add(new PartyOption("executed-public", e.Id, Single(e.EntityName)));
        }
        else
        {
            foreach (var e in doc.ApplicantPublicEntities)
                parties.Add(new PartyOption("applicant-entity", e.Id, Single(e.Name)));
            parties.Add(new PartyOption("borrower", doc.Id, TripleOr(doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily, doc.BorrowerRepresentativeName)));
            foreach (var g in doc.Guarantors)
                parties.Add(new PartyOption("guarantor", g.Id, TripleOr(g.GuarantorName, g.GuarantorFather, g.GuarantorFamily, null)));
            foreach (var h in doc.Heirs)
                parties.Add(new PartyOption("heir", h.Id, TripleOr(h.HeirName, h.HeirFather, h.HeirFamily, null)));
        }
        return parties;
    }

    internal static (string AppellantsJson, string AppelleesJson) BuildSnapshots(
        Document doc, string direction, List<AppealPartySelectionDto>? selections)
    {
        var options = BuildOptions(doc, direction);
        if (options.Count == 0)
            throw new ArgumentException("لا توجد أطراف مؤهلة للاختيار كمستأنف على هذا الملف");

        var selected = (selections ?? new List<AppealPartySelectionDto>())
            .Select(s => $"{s.Kind.Trim()}:{s.PartyId}")
            .Distinct()
            .ToList();

        if (selected.Count == 0)
            throw new ArgumentException("يجب اختيار المستأنف");

        var byKey = options.ToDictionary(o => $"{o.Kind}:{o.PartyId}");
        var appellants = new List<AppealPartyDto>();
        foreach (var key in selected)
        {
            if (!byKey.TryGetValue(key, out var option))
                throw new ArgumentException("أحد المستأنفين المختارين لا يتبع أطراف الملف");
            appellants.Add(new AppealPartyDto(option.Kind, option.PartyId, option.Name));
        }

        // المستأنف عليهم = سجل أطراف الملف الكامل ناقص المختارين (مواجهة الجميع حكمًا).
        var selectedKeys = selected.ToHashSet();
        var appellees = BuildAllParties(doc)
            .Where(o => !selectedKeys.Contains($"{o.Kind}:{o.PartyId}"))
            .Select(o => new AppealPartyDto(o.Kind, o.PartyId, o.Name))
            .ToList();

        return (
            AppealSnapshotSerializer.SerializeParties(appellants),
            AppealSnapshotSerializer.SerializeParties(appellees));
    }
}
