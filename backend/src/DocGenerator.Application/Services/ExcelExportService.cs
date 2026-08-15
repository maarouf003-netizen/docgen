using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// توليد ملف xlsx حقيقي للملفات التنفيذية عبر DocumentFormat.OpenXml
/// (موجودة أصلًا في المشروع) دون أي اعتماد خارجي جديد.
/// الأعمدة تُبنى حسب أذونات الدور المُمرّرة من المتحكم.
/// </summary>
public interface IExcelExportService
{
    byte[] BuildDocumentsWorkbook(
        IReadOnlyList<DocumentResponse> documents,
        bool includeAdministrativeBranch,
        bool includeAssignedLawyer,
        bool includeViewCount);
}

public sealed class ExcelExportService : IExcelExportService
{
    private static readonly string[] BaseColumns =
    {
        "الحالة", "طالب التنفيذ", "الفرع", "المنفذ عليه", "دائرة التنفيذ",
        "رقم الملف", "ملحق العقد",
    };

    public byte[] BuildDocumentsWorkbook(
        IReadOnlyList<DocumentResponse> documents,
        bool includeAdministrativeBranch,
        bool includeAssignedLawyer,
        bool includeViewCount)
    {
        using var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook(new Sheets());

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());
            worksheetPart.Worksheet.Append(new AutoFilter());

            var sheets = workbookPart.Workbook.GetFirstChild<Sheets>()!;
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "الملفات التنفيذية",
            });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            var headers = BuildHeaders(includeAdministrativeBranch, includeAssignedLawyer, includeViewCount);
            sheetData.AppendChild(BuildRow(headers));

            foreach (var doc in documents)
                sheetData.AppendChild(BuildRow(BuildValues(doc,
                    includeAdministrativeBranch, includeAssignedLawyer, includeViewCount)));

            // نطاق AutoFilter يبدأ من صف العنوان إلى آخر صف بيانات ليكون صالحًا في إكسل
            // (AutoFilter بلا Reference منتج ملفًا غير مطابق للمخطط ويُطلب إصلاحه).
            worksheetPart.Worksheet.GetFirstChild<AutoFilter>()!.Reference =
                $"A1:{ColumnLetter(headers.Count)}{1 + documents.Count}";

            worksheetPart.Worksheet.Save();
        }

        return stream.ToArray();
    }

    private static List<string> BuildHeaders(bool includeAdministrativeBranch, bool includeAssignedLawyer, bool includeViewCount)
    {
        var headers = new List<string>();
        if (includeAdministrativeBranch)
            headers.Add("فرع الإدارة");
        headers.AddRange(BaseColumns);
        if (includeAssignedLawyer)
            headers.Add("المحامي المختص");
        headers.Add("الإجراءات والملاحظات");
        if (includeViewCount)
            headers.Add("عدد المشاهدات");
        return headers;
    }

    private static List<string> BuildValues(
        DocumentResponse doc,
        bool includeAdministrativeBranch,
        bool includeAssignedLawyer,
        bool includeViewCount)
    {
        var values = new List<string>();
        if (includeAdministrativeBranch)
            values.Add(doc.AdministrativeBranchName ?? string.Empty);
        values.Add(StatusText(doc));
        values.Add(ApplicantText(doc));
        values.Add(doc.BranchName ?? string.Empty);
        values.Add(FullName(doc));
        values.Add(doc.Court ?? string.Empty);
        values.Add(FileNumberText(doc));
        values.Add(doc.AnnexNumber ?? string.Empty);
        if (includeAssignedLawyer)
            values.Add(doc.Lawyer ?? string.Empty);
        values.Add(HtmlInputSanitizer.ToPlainText(doc.ExecutionActions.FirstOrDefault()?.Text));
        if (includeViewCount)
            values.Add(doc.ViewCount.ToString());
        return values;
    }

    /// <summary>اسم طالب التنفيذ/العرض: في عائلة وضع «منفذ عليه» يُؤخذ من أول «طالب تنفيذ/عرض» (اسم ثلاثي)، وإلا الحقل المباشر.</summary>
    private static string ApplicantText(DocumentResponse doc)
    {
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var applicant = doc.ExecutionApplicants
                .Select(a => string.Join(' ', a.Name, a.Father, a.Family))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(applicant))
                return applicant;
        }
        return doc.Applicant ?? string.Empty;
    }

    private static string StatusText(DocumentResponse doc)
    {
        // ملف عائلة وضع «منفذ عليه» (Executed + Deposit): الحالة من ExecutedStatus (متداول/منفذ/مشطوب)
        // معزولة تمامًا عن حالة نظام «طالبة تنفيذ».
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            if (string.IsNullOrWhiteSpace(doc.ExecutedStatus)) return "متداول";
            return doc.ExecutedStatus == "مشطوب" ? "مشطوب" : "منفذ";
        }
        if (doc.ExecStatus == "تريث") return "تريث";
        if (doc.ExecStatus == "منفذ جبريا" && doc.ExecSubStatus == "منفذ جزئيا") return "متداول / منفذ جزئيا";
        if (doc.ExecStatus == "منفذ جبريا" || doc.ExecStatus == "منفذ بالتسوية") return "منفذ";
        return doc.IsDraft ? "تحت رفع" : "متداول";
    }

    private static string FullName(DocumentResponse doc)
    {
        // ملف عائلة وضع «منفذ عليه» (Executed + Deposit): الاسم المعروض هو أول طرف
        // (طالب التنفيذ/العرض أولًا، ثم الجهة/الشخص المنفذ عليه) — بلا مقترض.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var applicant = doc.ExecutionApplicants
                .Select(a => string.Join(' ', a.Name, a.Father, a.Family))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(applicant))
                return applicant;
            var entity = doc.ExecutedPublicEntities
                .Select(e => e.EntityName)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(entity))
                return entity;
            return doc.ExecutedNaturalPersons
                .Select(p => string.Join(' ', p.Name, p.Father, p.Family))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
        return string.Join(' ',
            new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }.Where(v => !string.IsNullOrWhiteSpace(v)));
    }

    private static string FileNumberText(DocumentResponse doc)
    {
        if (doc.IsDraft) return string.Empty;
        var number = doc.DisplayFileNumber ?? doc.FileNumber ?? string.Empty;
        var type = doc.FileType ?? string.Empty;
        return type.Length > 0 ? $"{number} {type}".Trim() : number;
    }

    private static Row BuildRow(IReadOnlyList<string> values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.AppendChild(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value ?? string.Empty)),
            });
        }
        return row;
    }

    /// <summary>حرف العمود المقابل لفهرس عمود (1 = A، 2 = B، ...) مع دعم أعمدة AA+.</summary>
    private static string ColumnLetter(int index)
    {
        var letters = string.Empty;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            letters = (char)('A' + rem) + letters;
            index = (index - 1) / 26;
        }
        return letters;
    }
}
