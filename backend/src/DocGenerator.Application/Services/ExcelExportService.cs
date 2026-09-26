using DocGenerator.Application.Common;
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
///
/// مساران بعقدين منفصلين عمدًا (لا رايات متضاربة تُنسى):
/// ‎<see cref="BuildDocumentsWorkbook"/>‎ للمدير/المحامي (يُعمَّم على رايات الدور)،
/// و‎<see cref="BuildPortalWorkbook"/>‎ لبوابة المندوب (قائمة أعمدة ثابتة مضبوطة سلفًا).
/// </summary>
public interface IExcelExportService
{
    byte[] BuildDocumentsWorkbook(
        IReadOnlyList<DocumentResponse> documents,
        bool includeAdministrativeBranch,
        bool includeAssignedLawyer,
        bool includeViewCount);

    /// <summary>
    /// مصنّف بوابة المندوب: أعمدة ثابتة لا رايات. «ملحق العقد» غير موجود فيه
    /// بحكم التصميم، وعمود «الإجراءات والملاحظات» يحمل **أحدث إجراء علني واحدًا**
    /// فقط (أول عنصر بعد تنقية `ScrubForPortal`: النوع `action` حصرًا، الأحدث
    /// `CreatedAt` ثم `Id` — الملاحظات الداخلية لا تصل إلى العمود أصلًا)،
    /// وعمود الفرع يحمل <b>فروع نطاق المندوب نفسه</b> (المصدر الآمن
    /// `MatchedEntries`) لا `DocumentResponse.BranchName` الداخلي المحجوب.
    /// </summary>
    byte[] BuildPortalWorkbook(IReadOnlyList<PortalWorkbookRow> rows);

    byte[] BuildChangeEventsWorkbook(IReadOnlyList<EntityChangeEventDto> events);
}

public sealed class ExcelExportService : IExcelExportService
{
    private static readonly string[] BaseColumns =
    {
        "الحالة", "طالب التنفيذ", "الفرع", "المنفذ عليه", "دائرة التنفيذ",
        "رقم الملف", "لعام", "ملحق العقد",
    };

    /// <summary>
    /// أعمدة بوابة المندوب — تعريف مغلق ومحكوم بالقائمة الحرفية أدناه
    /// (الاختبار `Export_PortalHeadersMatchDeclaredAllowlist` يقارن التسلسل كاملًا).
    /// العنوان «فرع الجهة» لا «الفرع» عمدًا: في تصدير المدير «الفرع» هو فرع الإدارة
    /// الداخلي، وفي هذا المصنّف هو فروع نطاق المندوب — فتسمية واحدة بمعنيين يقرأه
    /// المدقق تناقضًا.
    /// </summary>
    private static readonly string[] PortalColumns =
    {
        "الحالة", "طالب التنفيذ", "فرع الجهة", "المنفذ عليه", "دائرة التنفيذ",
        "رقم الملف", "لعام", "الإجراءات والملاحظات",
    };

    public byte[] BuildDocumentsWorkbook(
        IReadOnlyList<DocumentResponse> documents,
        bool includeAdministrativeBranch,
        bool includeAssignedLawyer,
        bool includeViewCount)
    {
        var headers = BuildHeaders(includeAdministrativeBranch, includeAssignedLawyer, includeViewCount);
        return WriteWorkbook(
            "الملفات التنفيذية",
            headers,
            documents.Select(doc => BuildValues(
                doc,
                includeAdministrativeBranch, includeAssignedLawyer, includeViewCount)));
    }

    public byte[] BuildPortalWorkbook(IReadOnlyList<PortalWorkbookRow> rows)
        => WriteWorkbook(
            "الملفات التنفيذية",
            PortalColumns,
            rows.Select(r => BuildPortalValues(r)));

    private static List<string> BuildPortalValues(PortalWorkbookRow row)
    {
        var doc = row.Document;
        return new List<string>
        {
            StatusText(doc),
            ApplicantText(doc),
            PortalBranchText(row.ScopedEntries),
            FullName(doc),
            doc.Court ?? string.Empty,
            FileNumberText(doc),
            doc.DisplayFileYear ?? doc.FileYear ?? string.Empty,
            // أحدث إجراء علني وحده: `ScrubForPortal` رشّح `ExecutionActions` إلى
            // النوع `action` مرتّبًا (الأحدث أولًا) قبل بناء الصف، فالأول هنا هو
            // الأحدث حتمًا، والملاحظات الداخلية لا تصل إلى هذا العمود أصلًا.
            HtmlInputSanitizer.ToPlainText(doc.ExecutionActions.FirstOrDefault()?.Text),
        };
    }

    /// <summary>
    /// عمود «فرع الجهة»: كل الفروع المطابقة من نطاق المندوب، كاملة بلا اقتطاع
    /// (خلافًا لبطاقة الواجهة التي تختصر أول فرعين وتلحق «+N» لحجر البصر — الاقتطاع
    /// في ملف إكسل يفقد بيانات بصمت ويجعل الخلية غير قابلة للفرز على كل قيمها).
    /// الترتيب ليس ترتيب الإدراج ولا ترتيب قاعدة البيانات: هو ترتيب
    /// `BuildMatchedEntries` أي ترتيب `PortalScopeResolution.Entries` — لغوي
    /// عربي (المحافظة ثم الفرع بمقارن ثقافة `ar` في `PortalRepository`)،
    /// وهو نفسه ترتيب قائمة الفرع في واجهة المندوب، فالخلية تطابق قائمته.
    /// </summary>
    private static string PortalBranchText(IReadOnlyList<PortalScopeEntryDto> entries)
        => entries.Count == 0
            ? string.Empty
            : string.Join(" · ", entries.Select(e => PublicEntityBranchCatalog.Label(e.Governorate, e.BranchName)));

    /// <summary>
    /// المحرك المشترك لكتابة المصنَّف (بنية الملف + AutoFilter + الصفوف) — مصدر
    /// واحد يبقي مسارَي التصدير متطابقين بنيويًا بلا ازدواج، ويبقى حساب نطاق
    /// `AutoFilter` مشتقًا من عدد العناوين الفعلي فلا يبقى يدويًا يخطئ عند أي
    /// إضافة أو حذف عمود.
    /// </summary>
    private static byte[] WriteWorkbook(
        string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
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
                Name = sheetName,
            });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;
            sheetData.AppendChild(BuildRow(headers));

            var rowCount = 0;
            foreach (var values in rows)
            {
                sheetData.AppendChild(BuildRow(values));
                rowCount++;
            }

            // نطاق AutoFilter يبدأ من صف العنوان إلى آخر صف بيانات ليكون صالحًا في إكسل
            // (AutoFilter بلا Reference منتج ملفًا غير مطابق للمخطط ويُطلب إصلاحه).
            worksheetPart.Worksheet.GetFirstChild<AutoFilter>()!.Reference =
                $"A1:{ColumnLetter(headers.Count)}{1 + rowCount}";

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
        values.Add(doc.DisplayFileYear ?? doc.FileYear ?? string.Empty);
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
        => DocumentStatusResolver.Resolve(doc);

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

    public byte[] BuildChangeEventsWorkbook(IReadOnlyList<EntityChangeEventDto> events)
    {
        var headers = new[] { "التاريخ", "الفاعل", "النوع", "الجهة", "المحافظة", "المرسوم", "التفاصيل" };
        return WriteWorkbook(
            "سجل التغييرات",
            headers,
            events.Select(e => (IReadOnlyList<string>)new[]
            {
                e.CreatedAtUtc ?? string.Empty,
                e.ActorName ?? string.Empty,
                e.ActionKind ?? string.Empty,
                e.CanonicalName ?? string.Empty,
                e.Governorate ?? string.Empty,
                string.Join(" ", new[] { e.DecreeKind, e.DecreeNumber, e.DecreeDate }
                    .Where(v => !string.IsNullOrWhiteSpace(v))),
                e.PayloadJson ?? string.Empty,
            }));
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
