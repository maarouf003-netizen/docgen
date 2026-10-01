# خطة تنفيذ مفصلة: الملفات بلا إجراءات (Stale Files) + حذف أرشفة التذكيرات الشخصية (استبدال بـ «منتهٍ»)

> **الحالة:** خطة نهائية وفق الأصول الصارمة. لا يبدأ التنفيذ قبل موافقة صريحة على هذه الوثيقة. يُسمح بالقراءة فقط حتى إصدار الموافقة.

> **القرار المعتمد:** استبدال الأرشفة بحالة `IsCompleted` (الخيار 3) — يعيد فائدة الأرشفة بلا عيوبها، ويُفكّ سقف `200` للتذكيرات النشطة دون رفعه.

## 1) الأهداف والنطاق

### 1.1 الأهداف
- إضافة صفحة مستقلة ` /stale-files` تعرض الملفات التي **لم يدخل عليها المحامي أي `ExecutionAction` منذ ≥ N شهرًا** (افتراضي 4 أشهر)، مع رابط يفتح `/documents/:id`.
- الاستبدال الكلي لأرشفة `PersonalReminder.IsArchived` بحالة **`IsCompleted`**: التذكير المنتهي لا يُحتسب ضمن سقف 200، يختفي من التقويم/الجرس، ويُعرض في قسم «تذكيرات منتهية» قابل للإعادة للفتح.
- حذف الأرشفة نهائيًا من الخلفية والواجهة (بدل إبقائها ميتة).
- تطبيق **التحقق الكامل** (اختبارات + lint + typecheck + build) قبل إعلان الإنجاز.
- الالتزام الصارم بـ `Date Fields Rule` و `Frontend Rules` و `Web Design Guidelines`.

### 1.2 خارج النطاق عمدًا
- تعديل `CalendarSource` أو `groupRemindersByDay` أو `ReminderList` في نطاق التنفيذ الحالي (يُستفاد منها كما هي).
- أي لمس لـ `DocumentService` (يُستخدم `IStaleFileReminderService` مستقل).
- إضافة عمود/حقل لحساب الركود في `Document` (الحساب عند القراءة فقط).
- تغيير سلوك `AddExecutionActionAsync` (لا يحدّث `Document.UpdatedAt` — معلوم).

## 2) المصطلحات والتثبيتات الدلالية (إلزامي)

### 2.1 تعريف «الملفات بلا إجراءات» (Stale Files)
الملف **نشط** ولم يُسجَّل عليه أي إجراء تنفيذي يدوي (`ExecutionAction`) منذ فترة `months`. العمر يُحسب من:

- **إذا وُجدت `ExecutionActions` للملف:** `lastActivityUtc = MAX(ExecutionActions.CreatedAt)` (UTC). المصدر الحصري — **مؤكد:** لا يُعتمد `Document.UpdatedAt`.
- **إذا لم توجد أي `ExecutionActions`:** `lastActivityUtc = Document.CreatedAt` (UTC) — نقطة البداية للملف بلا إجراءات. **يُطلب التأكيد التالي:** هل يُفضَّل استخدام `Document.CreatedAt` أم `FileReceiptDate`/تاريخ التسجيل الرسمي؟ (افتراض الخطة: `Document.CreatedAt`، مع إبقائه قابل للتوثيق).

### 2.2 نطاق الملفات المشمولة (Active Scope)
يُستخدم **نطاق الملفات النشطة الحرفي** الموجود في `DocumentRepository.ApplySearchFilters` (السطور ~141–157) كمصدر وحيد للحقيقة، ويُستخرج إلى `DocumentScope.ActiveScope(IQueryable<Document>, bool includeDrafts)`.

**التضمين/الاستبعاد المعتمد:**
- **تشمل:** الملفات المتداولة (`مستمر`/`متداول`)، المتريثة (`تريث`)، والملفات ذات الحالة الجزئية (`منفذ جزئياً`).
- **تستبعد صراحة:** `مشطوب`، `منفذ` (كامل)، `محال إلى البداية`، `مسترد من التنفيذ`، الملفات بحالة «منفذ عليه» (`ExecutedStatusCatalog`)، و**ملفات تحت الرفع (`Draft`) — `includeDrafts = false`** للتنبيه (الملف غير المسجَّل بعد لا يُوجَّه به المحامي).
- **الإنابة (`انابة`):** افتراض الخطة: **تُضمَّن ملفات الإنابة النشطة** (`FileType = "انابة"` وحالتها التنفيذية نشطة ضمن النطاق أعلاه). **يُستبعد** `منفذ إنابة` (حالة نهائية يُصنَّف غالبًا كـ«منفَّذ» في القوائم القائمة) — انتظار تأكيد صريح إن رغبتَ إدراجه.

> ملاحظة: لا يُستخدم `GeneralEntitySideCatalog.IsExecutedLike()` داخل تعبير `Where` لترجمته إلى SQL (نمط EF القائم) — يُفكَّك إلى شروط صريحة OR.

### 2.3 الحدود الزمنية والشدة
- الافتراضي: `months = 4`
- المدى المسموح: `[1..24]` (validation صارم)
- الشدة (Severity): `critical ≥ 12 أشهر`، `warn ≥ 6 أشهر`، وإلا `info` (4–5 أشهر). قابلة للتوثيق دون هجرة.

## 3) المرحلة A — الملفات بلا إجراءات

### 3.1 Enum و Catalog
**جديد:** `backend/src/DocGenerator.Domain/Enums/StaleFileCatalog.cs`
```csharp
namespace DocGenerator.Domain.Enums;

public static class StaleFileCatalog
{
    public const int DefaultMonths = 4;
    public const int MinMonths = 1;
    public const int MaxMonths = 24;

    public const int DefaultPerPage = 20;
    public const int MaxPerPage = 100;

    public const string SeverityInfo = "info";
    public const string SeverityWarn = "warn";
    public const string SeverityCritical = "critical";
}
```

**جديد:** `backend/src/DocGenerator.Domain/Enums/StaleFileSeverity.cs` (اختياري للتوثيق) أو استخدام الثوابت مباشرة — الثوابت كافية.

### 3.2 Scope موحّد (مصدر الحقيقة)
**جديد:** `backend/src/DocGenerator.Infrastructure/Persistence/DocumentScope.cs`
```csharp
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Infrastructure.Persistence;

public static class DocumentScope
{
    // النطاق النشط حرفيًا مطابق لـ ApplySearchFilters (includeDrafts يتحكّم بـ Draft)
    public static IQueryable<Document> ActiveScope(IQueryable<Document> query, bool includeDrafts)
    {
        // يُفكَّك النطاق النشط إلى شروط صريحة (عدم استخدام IsExecutedLike داخل Where قابل للترجمة المباشرة)
        query = query.Where(d =>
            d.ExecStatus != ExecutionStatusCatalog.StruckOff &&
            d.ExecStatus != ExecutionStatusCatalog.Executed &&
            d.ExecStatus != ExecutionStatusCatalog.ReturnedToStart &&
            d.ExecStatus != ExecutionStatusCatalog.RecoveredFromExecution &&
            d.ExecStatus != ExecutedStatusCatalog.ExecutedOnIt
        );

        if (!includeDrafts)
        {
            query = query.Where(d => !d.IsDraft);
        }
        return query;
    }
}
```

**تعديل:** `backend/src/DocGenerator.Infrastructure/Persistence/DocumentRepository.cs:139–158`
استبدال كتلة الفلاتر بـ:
```csharp
query = DocumentScope.ActiveScope(query, includeDrafts: true);
```
(سلوك مطابق حرفيًا — حماية ضد الانحراف مستقبلاً).

### 3.3 الحاسبة النقية والقابلة للاختبار
**جديد:** `backend/src/DocGenerator.Application/Common/StaleFileReminderCalculator.cs`
```csharp
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

public static class StaleFileReminderCalculator
{
    public static int NormalizeMonths(int? months)
    {
        var m = months ?? StaleFileCatalog.DefaultMonths;
        if (m < StaleFileCatalog.MinMonths) m = StaleFileCatalog.MinMonths;
        if (m > StaleFileCatalog.MaxMonths) m = StaleFileCatalog.MaxMonths;
        return m;
    }

    public static DateTime CutoffUtc(DateTime todayUtc, int months)
        => todayUtc.AddMonths(-months);

    // فرق الأشهر الكاملة (≥ 1). اليوم نفسه لا يُحسب شهرًا كاملًا إضافيًا وفق المنطق المقصود.
    public static int IdleMonths(DateTime todayUtc, DateTime lastActivityUtc)
    {
        if (lastActivityUtc > todayUtc) lastActivityUtc = todayUtc;
        var months = (todayUtc.Year - lastActivityUtc.Year) * 12 + (todayUtc.Month - lastActivityUtc.Month);
        if (todayUtc.Day < lastActivityUtc.Day) months -= 1;
        if (months < 0) months = 0;
        return months;
    }

    public static string Severity(int idleMonths)
    {
        if (idleMonths >= 12) return StaleFileCatalog.SeverityCritical;
        if (idleMonths >= 6) return StaleFileCatalog.SeverityWarn;
        return StaleFileCatalog.SeverityInfo;
    }

    // yyyy-MM-dd بتوقيت المستخدم (TimeZoneInfo) — التزام Date Fields Rule (نص تاريخ)
    public static string? LastActivityOn(DateTime? lastActivityUtc, DateTime createdUtc, TimeZoneInfo tz)
    {
        var baseUtc = lastActivityUtc ?? createdUtc;
        var local = TimeZoneInfo.ConvertTimeFromUtc(baseUtc, tz);
        return local.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}
```

> ملاحظة: الحساب بالأشهر الكاملة كما هو مكتوب — يُفصّل في الاختبارات (0،1،4،5).

### 3.4 DTOs
**جديد:** `backend/src/DocGenerator.Application/DTOs/StaleFileDtos.cs`
```csharp
namespace DocGenerator.Application.DTOs;

public record StaleFileReminderDto(
    int DocumentId,
    string? FileNumber,
    string? FileYear,
    string? DocumentType,
    string? Court,
    string? BranchName,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    int IdleMonths,
    string Severity,        // info|warn|critical
    string? LastActionOn,    // yyyy-MM-dd (حسب Date Fields Rule — نص)
    bool HasAnyAction
);

public record StaleFileCountDto(int Count);
```

### 3.5 Repository (استعلام خطوتين — إلزامي)
**جديد:** `backend/src/DocGenerator.Application/Interfaces/IStaleFileRepository.cs`
```csharp
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Interfaces;

public interface IStaleFileRepository
{
    Task<(List<StaleFileReminderDto> Items, int TotalCount)> GetStaleAsync(
        int lawyerId,
        int months,
        DateTime todayUtc,
        TimeZoneInfo tz,
        string? court,
        int page,
        int perPage,
        CancellationToken ct);
}
```

**جديد:** `backend/src/DocGenerator.Infrastructure/Persistence/StaleFileRepository.cs`
```csharp
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public class StaleFileRepository : IStaleFileRepository
{
    private readonly DocGeneratorDbContext _db;

    public StaleFileRepository(DocGeneratorDbContext db)
    {
        _db = db;
    }

    public async Task<(List<StaleFileReminderDto> Items, int TotalCount)> GetStaleAsync(
        int lawyerId,
        int months,
        DateTime todayUtc,
        TimeZoneInfo tz,
        string? court,
        int page,
        int perPage,
        CancellationToken ct)
    {
        var cutoffUtc = StaleFileReminderCalculator.CutoffUtc(todayUtc, months);

        // ① Documents: نطاق نشط + CreatedById + CreatedAt <= cutoff (تصفية مبكرة)
        var docQuery = _db.Documents
            .AsNoTracking()
            .Where(d => d.CreatedById == lawyerId)
            .Where(d => !d.IsDraft);

        docQuery = DocumentScope.ActiveScope(docQuery, includeDrafts: false);

        if (court != null && court.Trim() != string.Empty)
        {
            var c = court.Trim();
            docQuery = docQuery.Where(d => d.Court != null && d.Court.Contains(c));
        }

        // تصفية مبكرة لتقليل السجلات: الملف المنشأ بعد cutoff لا يُرشَّح
        docQuery = docQuery.Where(d => d.CreatedAt <= cutoffUtc);

        // حقول العرض فقط
        var docs = await docQuery
            .Select(d => new
            {
                d.Id,
                d.FileNumber,
                d.FileYear,
                d.DocumentType,
                d.Court,
                d.BranchName,
                d.BorrowerName,
                d.BorrowerFather,
                d.BorrowerFamily,
                d.CreatedAt
            })
            .ToListAsync(ct);

        if (docs.Count == 0)
        {
            return ([], 0);
        }

        var ids = docs.Select(x => x.Id).ToList();

        // ② ExecutionActions: MAX(CreatedAt) لكل DocumentId (بلا تحميل Text)
        var lastActs = await _db.ExecutionActions
            .AsNoTracking()
            .Where(a => ids.Contains(a.DocumentId))
            .GroupBy(a => a.DocumentId)
            .Select(g => new { DocumentId = g.Key, LastAt = g.Max(a => a.CreatedAt) })
            .ToListAsync(ct);

        var lastMap = lastActs.ToDictionary(x => x.DocumentId, x => x.LastAt);

        // ③ الدمج + الترشيح النهائي + الفرز + الترقيم
        var list = new List<StaleFileReminderDto>(docs.Count);
        foreach (var d in docs)
        {
            lastMap.TryGetValue(d.Id, out var lastAt);
            var hasAny = lastAt.HasValue;
            var effective = hasAny ? lastAt!.Value : d.CreatedAt;

            // ترشيح نهائي: فعال ≤ cutoff (حماية)
            if (effective > cutoffUtc) continue;

            var idle = StaleFileReminderCalculator.IdleMonths(todayUtc, effective);
            var sev = StaleFileReminderCalculator.Severity(idle);
            var lastOn = StaleFileReminderCalculator.LastActivityOn(hasAny ? lastAt : null, d.CreatedAt, tz);

            list.Add(new StaleFileReminderDto(
                DocumentId: d.Id,
                FileNumber: d.FileNumber,
                FileYear: d.FileYear,
                DocumentType: d.DocumentType,
                Court: d.Court,
                BranchName: d.BranchName,
                BorrowerName: d.BorrowerName,
                BorrowerFather: d.BorrowerFather,
                BorrowerFamily: d.BorrowerFamily,
                IdleMonths: idle,
                Severity: sev,
                LastActionOn: lastOn,
                HasAnyAction: hasAny
            ));
        }

        // الترتيب: الأقدم في الركود أولاً (IdleMonths تنازليًا)، ثم DocumentId تصاعديًا
        list = list
            .OrderByDescending(x => x.IdleMonths)
            .ThenBy(x => x.DocumentId)
            .ToList();

        var total = list.Count;
        var pageIdx = page < 1 ? 1 : page;
        var perP = perPage < 1 ? StaleFileCatalog.DefaultPerPage : (perPage > StaleFileCatalog.MaxPerPage ? StaleFileCatalog.MaxPerPage : perPage);
        var skip = (pageIdx - 1) * perP;
        var itemsPaged = list.Skip(skip).Take(perP).ToList();

        return (itemsPaged, total);
    }
}
```

**تسجيل DI:** `backend/src/DocGenerator.Infrastructure/DependencyInjection.cs` ← `services.AddScoped<IStaleFileRepository, StaleFileRepository>();` (بعد التسجيلات القائمة، مثلاً بعد `PersonalReminderRepository`).

### 3.6 الخدمة
**جديد:** `backend/src/DocGenerator.Application/Interfaces/IStaleFileReminderService.cs`
```csharp
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Interfaces;

public interface IStaleFileReminderService
{
    Task<(List<StaleFileReminderDto> Items, int TotalCount)> GetStaleAsync(int lawyerId, int? months, string? court, int page, int perPage, CancellationToken ct);
    Task<StaleFileCountDto> GetCountAsync(int lawyerId, int? months, CancellationToken ct);
    Task<List<string>> GetCourtsAsync(int lawyerId, int? months, CancellationToken ct);
}
```

**جديد:** `backend/src/DocGenerator.Application/Services/StaleFileReminderService.cs`
```csharp
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Interfaces;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public class StaleFileReminderService : IStaleFileReminderService
{
    private readonly IStaleFileRepository _repo;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _tz;

    public StaleFileReminderService(IStaleFileRepository repo, TimeProvider clock, TimeZoneInfo tz)
    {
        _repo = repo;
        _clock = clock;
        _tz = tz;
    }

    public async Task<(List<StaleFileReminderDto> Items, int TotalCount)> GetStaleAsync(int lawyerId, int? months, string? court, int page, int perPage, CancellationToken ct)
    {
        var m = StaleFileReminderCalculator.NormalizeMonths(months);
        var now = ServerClock.Now(_clock, _tz);
        var todayUtc = now.Date.ToUniversalTime();
        return await _repo.GetStaleAsync(lawyerId, m, todayUtc, _tz, court, page, perPage, ct);
    }

    public async Task<StaleFileCountDto> GetCountAsync(int lawyerId, int? months, CancellationToken ct)
    {
        var m = StaleFileReminderCalculator.NormalizeMonths(months);
        var now = ServerClock.Now(_clock, _tz);
        var todayUtc = now.Date.ToUniversalTime();
        var (items, total) = await _repo.GetStaleAsync(lawyerId, m, todayUtc, _tz, null, 1, 1, ct);
        return new StaleFileCountDto(total);
    }

    public async Task<List<string>> GetCourtsAsync(int lawyerId, int? months, CancellationToken ct)
    {
        var m = StaleFileReminderCalculator.NormalizeMonths(months);
        var now = ServerClock.Now(_clock, _tz);
        var todayUtc = now.Date.ToUniversalTime();
        var cutoffUtc = StaleFileReminderCalculator.CutoffUtc(todayUtc, m);

        var q = _repo.GetType(); // placeholder not needed; rebuild light query via context? but repo returns paged
        // Light query: courts distinct for active stale scope
        // تنفيذ مباشر عبر DbContext داخل الخدمة خفيفة أو إضافة طريقة — لكن لتفادي DI إضافي نعيد الاستعلام المباشر خفيف
        // لكن _repo يعتمد DbContext — يمكن حقن DbContext هنا أو إضافة طريقة في IRepo. الأفضل إضافة في Repo.

        // (لحفظ التعديل الدقيق: نضيف الطرق الثلاث عبر Repo أو نعيد light query)
        await Task.CompletedTask;
        return [];
    }
}
```

> تصحيح صغير للـ `GetCourtsAsync`: الأفضل إضافته لـ `IStaleFileRepository` (استعلام `Distinct(d.Court)` مع نفس الفلاتر، بدون Select كامل). أُدرجه أدناه في Repo.

**إضافة `GetCourtsAsync` إلى Repo:**
```csharp
Task<List<string>> GetCourtsAsync(int lawyerId, int months, DateTime todayUtc, CancellationToken ct);
```
تنفيذ:
```csharp
public async Task<List<string>> GetCourtsAsync(int lawyerId, int months, DateTime todayUtc, CancellationToken ct)
{
    var cutoffUtc = StaleFileReminderCalculator.CutoffUtc(todayUtc, months);
    var courts = await _db.Documents
        .AsNoTracking()
        .Where(d => d.CreatedById == lawyerId && !d.IsDraft)
        .Where(d => d.Court != null && d.Court != string.Empty)
        .Where(DocumentScope.ActiveScope(_db.Documents.AsQueryable(), includeDrafts:false).Where(x=>x.Id==d.Id).Any()) // not needed; inline
        // inline ActiveScope logic by filter copy is cheap: reuse same conditions
        .Where(d => d.ExecStatus != ExecutionStatusCatalog.StruckOff && ...)
        // أو ببساطة نطبق نفس ActiveScope على IQueryable
        .ApplyActiveScope(includeDrafts:false) // extension اختياري
        .Where(d => d.CreatedAt <= cutoffUtc)
        .Select(d => d.Court!)
        .Distinct()
        .OrderBy(c => c)
        .ToListAsync(ct);
    return courts;
}
```
(لتبسيط: كرّر شروط `ActiveScope` مباشرة أو أنشئ `IQueryable<Document>` واحدًا).

**تسجيل الخدمة:** `backend/src/DocGenerator.Application/DependencyInjection.cs` ← `services.AddScoped<IStaleFileReminderService, StaleFileReminderService>();`

### 3.7 Controller
**جديد:** `backend/src/DocGenerator.Api/Controllers/StaleFileRemindersController.cs`
```csharp
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Interfaces;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/stale-file-reminders")]
[Authorize(Roles = "lawyer")]
[EnableRateLimiting("ExpensivePolicy")]
public class StaleFileRemindersController : ControllerBase
{
    private readonly IStaleFileReminderService _svc;
    private readonly IUserContext _user;

    public StaleFileRemindersController(IStaleFileReminderService svc, IUserContext user)
    {
        _svc = svc;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] int? months, [FromQuery] int page = 1, [FromQuery] int perPage = StaleFileCatalog.DefaultPerPage, [FromQuery] string? court = null, CancellationToken ct = default)
    {
        var uid = _user.UserId;
        if (uid <= 0) return Forbid();

        var m = months;
        if (m.HasValue && (m.Value < StaleFileCatalog.MinMonths || m.Value > StaleFileCatalog.MaxMonths))
            throw new ArgumentException($"months يجب أن يكون بين {StaleFileCatalog.MinMonths} و{StaleFileCatalog.MaxMonths}");

        var pp = perPage;
        if (pp < 1 || pp > StaleFileCatalog.MaxPerPage)
            throw new ArgumentException($"perPage يجب أن يكون بين 1 و{StaleFileCatalog.MaxPerPage}");

        var (items, total) = await _svc.GetStaleAsync(uid, m, court, page, pp, ct);
        return Ok(new { items, totalCount = total, page, perPage = pp });
    }

    [HttpGet("count")]
    public async Task<ActionResult> Count([FromQuery] int? months, CancellationToken ct = default)
    {
        var uid = _user.UserId;
        if (uid <= 0) return Forbid();
        if (months.HasValue && (months.Value < StaleFileCatalog.MinMonths || months.Value > StaleFileCatalog.MaxMonths))
            throw new ArgumentException($"months يجب أن يكون بين {StaleFileCatalog.MinMonths} و{StaleFileCatalog.MaxMonths}");
        var c = await _svc.GetCountAsync(uid, months, ct);
        return Ok(new { count = c.Count });
    }

    [HttpGet("courts")]
    public async Task<ActionResult> Courts([FromQuery] int? months, CancellationToken ct = default)
    {
        var uid = _user.UserId;
        if (uid <= 0) return Forbid();
        if (months.HasValue && (months.Value < StaleFileCatalog.MinMonths || months.Value > StaleFileCatalog.MaxMonths))
            throw new ArgumentException($"months يجب أن يكون بين {StaleFileCatalog.MinMonths} و{StaleFileCatalog.MaxMonths}");
        var list = await _svc.GetCourtsAsync(uid, months, ct);
        return Ok(list);
    }
}
```

> Validation: القيم خارج النطاق → `ArgumentException` → 400 عبر `GlobalExceptionHandler` القائم.

## 4) المرحلة B — استبدال الأرشفة بـ «IsCompleted»

### 4.1 الكيان
**تعديل:** `backend/src/DocGenerator.Domain/Entities/PersonalReminder.cs`
- احذف `public bool IsArchived { get; set; }`
- أضف `public bool IsCompleted { get; set; }`
- تحديث التعليق: يشير إلى عدم احتسابه في السقف، واختفائه من التقويم/الجرس، وبقائه للاستدعاء.

### 4.2 Enum
**تعديل:** `backend/src/DocGenerator.Domain/Enums/PersonalReminderCatalog.cs:39–40`
التعليق: «سقف التذكيرات النشطة (غير المنتهية) للمستخدم الواحد — منع الإساءة.» (توضيح دلالي).

### 4.3 Configuration (EF Fluent)
**تعديل:** `backend/src/DocGenerator.Infrastructure/Persistence/Configurations/Configurations.cs:1327–1355`
- احذف `builder.HasIndex(r => r.IsArchived);`
- أضف فهرس مركّب: `builder.HasIndex(r => new { r.LawyerId, r.IsCompleted });` (مفيد للفلترة النشطة/المنتهية حسب المستخدم)
- احذف Mapping لـ `IsArchived` وأضف `IsCompleted` (القيمة الافتراضية `false`).

### 4.4 Repository
**تعديل:** `backend/src/DocGenerator.Application/Interfaces/IPersonalReminderRepository.cs`
```csharp
Task<IReadOnlyList<PersonalReminder>> ListAsync(int userId, bool includeCompleted = false, CancellationToken ct = default);
Task<int> CountActiveForUserAsync(int userId, CancellationToken ct = default);
```
(استبدال `CountActiveAsync` باسم واضح + إزالة `includeArchived`).

**تعديل:** `backend/src/DocGenerator.Infrastructure/Persistence/PersonalReminderRepository.cs`
- `ListAsync`: `if (!includeCompleted) q = q.Where(r => !r.IsCompleted);`
- `CountActiveForUserAsync`: `return await _db.PersonalReminders.CountAsync(r => r.LawyerId == userId && !r.IsCompleted, ct);`
- احذف أي إشارة لـ `IsArchived`.

### 4.5 Service
**تعديل:** `backend/src/DocGenerator.Application/Services/PersonalReminderService.cs`

- `ListAsync(int userId, bool includeCompleted = false, CancellationToken ct)` — بدون معامل الأرشفة.
- `CreateAsync`: يبقى السقف على النشطة فقط: `var activeCount = await _repo.CountActiveForUserAsync(userId, ct);` والتحقق `MaxActivePerUser` (200).
- `UpdateAsync`: التعامل مع `IsCompleted` (قبول التحويل إلى منتهٍ أو إعادته). إزالة فرع الأرشفة.
- `SetOccurrenceAsync`: إضافة حارس:
  ```csharp
  if (reminder.IsCompleted)
      throw new ArgumentException("التذكير منتهٍ — أعِده للفتح أولًا");
  ```
- `ToDto`: إزالة `IsArchived`، إضافة `IsCompleted` (تحديث DTO).
- رسالتا السقف (كما نوقشت):
  - السطر ~75: «… (200) — احذف تذكيرًا قديمًا منها»
  - السطر ~185: «… — حدّد تاريخ انتهاء للتكرار أو احذف التذكير»

### 4.6 DTOs
**تعديل:** `backend/src/DocGenerator.Application/DTOs/PersonalReminderDtos.cs`
- `PersonalReminderDto`: أضف `bool IsCompleted`، احذف `bool IsArchived`
- `UpdatePersonalReminderRequest`: أضف `bool? IsCompleted`, احذف `bool? IsArchived`

### 4.7 Controller
**تعديل:** `backend/src/DocGenerator.Api/Controllers/PersonalRemindersController.cs`
- `GET`: `[FromQuery] bool includeCompleted = false` (بدل `includeArchived`)
- `PUT {id}`: تحديث Bind/Mapping لـ `IsCompleted`
- التعليقات تصحيح دلالي.

## 5) الهجرات (4 ملفات — SQLite + Postgres)

### 5.1 SQLite — إسقاط الأرشفة + إضافة IsCompleted
**جديد:** `backend/src/DocGenerator.Infrastructure/Persistence/Migrations/20260930120000_DropPersonalReminderArchiveAddCompleted.cs`
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    public partial class DropPersonalReminderArchiveAddCompleted : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. حذف المؤرشفين (غير قابل للاسترجاع)
            migrationBuilder.Sql("DELETE FROM \"PersonalReminders\" WHERE \"IsArchived\" = 1;");

            // 2. إسقاط الفهرس
            migrationBuilder.DropIndex(
                name: "IX_PersonalReminders_IsArchived",
                table: "PersonalReminders");

            // 3. إسقاط العمود IsArchived
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "PersonalReminders");

            // 4. إضافة IsCompleted + فهرس مركب
            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "PersonalReminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_LawyerId_IsCompleted",
                table: "PersonalReminders",
                columns: new[] { "LawyerId", "IsCompleted" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalReminders_LawyerId_IsCompleted",
                table: "PersonalReminders");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "PersonalReminders");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PersonalReminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_IsArchived",
                table: "PersonalReminders",
                column: "IsArchived");
            // Down لا يستعيد الصفوف المحذوفة — موثق صراحة
        }
    }
}
```

### 5.2 Postgres — النسخة المقابلة
**جديد:** `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres/20260930120010_DropPersonalReminderArchiveAddCompletedPg.cs`
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    public partial class DropPersonalReminderArchiveAddCompletedPg : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"PersonalReminders\" WHERE \"IsArchived\" = true;");

            migrationBuilder.DropIndex(
                name: "IX_PersonalReminders_IsArchived",
                table: "PersonalReminders");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "PersonalReminders");

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "PersonalReminders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_LawyerId_IsCompleted",
                table: "PersonalReminders",
                columns: new[] { "LawyerId", "IsCompleted" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalReminders_LawyerId_IsCompleted",
                table: "PersonalReminders");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "PersonalReminders");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PersonalReminders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_IsArchived",
                table: "PersonalReminders",
                column: "IsArchived");
        }
    }
}
```

### 5.3 فهرس ExecutionActions (للـ Stale Query)
**جديد:** `backend/src/DocGenerator.Infrastructure/Persistence/Migrations/20260930121000_AddExecutionActionStaleIndex.cs`
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    public partial class AddExecutionActionStaleIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExecutionActions_DocumentId_CreatedAt",
                table: "ExecutionActions",
                columns: new[] { "DocumentId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExecutionActions_DocumentId_CreatedAt",
                table: "ExecutionActions");
        }
    }
}
```

**جديد:** `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres/20260930121010_AddExecutionActionStaleIndexPg.cs`
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    public partial class AddExecutionActionStaleIndexPg : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExecutionActions_DocumentId_CreatedAt",
                table: "ExecutionActions",
                columns: new[] { "DocumentId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExecutionActions_DocumentId_CreatedAt",
                table: "ExecutionActions");
        }
    }
}
```

> **تنبيه هام:** `Up` يحذف الصفوف ذات `IsArchived = true/1` — **غير قابلة للاسترجاع**. يُوصى بنسخة احتياطية من `PersonalReminders` قبل النشر. Down يعيد العمود فقط ولا يسترجع البيانات.

## 6) الواجهة الأمامية (Frontend)

### 6.1 Types
**تعديل:** `frontend/src/types/index.ts`
- `PersonalReminderDto`: أضف `isCompleted: boolean`, احذف `isArchived: boolean`
- `UpdatePersonalReminderRequest`: أضف `isCompleted?: boolean | null`, احذف `isArchived?: boolean | null`
- إضافة:
  ```ts
  export interface StaleFileReminderDto {
    documentId: number;
    fileNumber?: string | null;
    fileYear?: string | null;
    documentType?: string | null;
    court?: string | null;
    branchName?: string | null;
    borrowerName?: string | null;
    borrowerFather?: string | null;
    borrowerFamily?: string | null;
    idleMonths: number;
    severity: 'info'|'warn'|'critical';
    lastActionOn?: string | null; // yyyy-MM-dd
    hasAnyAction: boolean;
  }
  export interface StaleFileCountDto { count: number }
  export interface PagedStaleFiles { items: StaleFileReminderDto[]; totalCount: number; page: number; perPage: number }
  ```

### 6.2 Utils
**جديد:** `frontend/src/utils/staleFiles.ts`
```ts
import { StaleFileReminderDto } from '@/types';

export function pluralMonths(n: number): string {
  if (n <= 0) return '0 شهر';
  if (n === 1) return 'شهر واحد';
  if (n === 2) return 'شهران';
  if (n >= 3 && n <= 10) return `${n} أشهر`;
  return `${n} شهرًا`;
}

export function idleLabel(m: number): string {
  return `مضى ${pluralMonths(m)} بلا إجراء`;
}

export function staleTone(severity: StaleFileReminderDto['severity']) {
  if (severity === 'critical') return { bg: 'bg-red-50', border: 'border-red-200', text: 'text-red-700', badge: 'bg-red-100 text-red-800' };
  if (severity === 'warn') return { bg: 'bg-orange-50', border: 'border-orange-200', text: 'text-orange-800', badge: 'bg-orange-100 text-orange-800' };
  return { bg: 'bg-amber-50', border: 'border-amber-200', text: 'text-amber-800', badge: 'bg-amber-100 text-amber-800' };
}

export function staleSummaryText(r: StaleFileReminderDto): string {
  const parts = [idleLabel(r.idleMonths)];
  const name = [r.borrowerName, r.borrowerFather, r.borrowerFamily].filter(Boolean).join(' ');
  if (name) parts.push(name);
  const file = [r.fileNumber, r.fileYear].filter(Boolean).join('/');
  if (file) parts.push(`رقم الملف ${file}`);
  if (r.documentType) parts.push(r.documentType);
  if (r.court) parts.push(r.court);
  if (r.branchName) parts.push(r.branchName);
  return parts.join(' · ');
}
```

**تعديل:** `frontend/src/utils/dashboardFormat.ts` — إبقاء الأنماط القائمة (لا كسر). `pluralMonths` هنا خاص بـ Stale Files (منفصل عن `pluralDays`).

### 6.3 Icons
**تعديل:** `frontend/src/components/dashboard/dashboardIcons.tsx` ← إضافة `ICONS.stale` (أيقونة مناسبة — Clock/AlertTriangle حسب النمط القائم).

### 6.4 Personal Reminders (تحديث IsCompleted)
**تعديل:** `frontend/src/components/dashboard/personalReminders.ts`
- إزالة كل فروع `if (reminder.isArchived)` (لا توجد بعد الآن)
- تحديث التعليقات
- `expandPersonalReminder`/الفلترة تبقى على النشطة فقط (`!p.isCompleted`)

**تعديل:** `frontend/src/components/dashboard/DayRemindersModal.tsx`
- إزالة `archived` state
- إزالة `isArchived` من payload
- إزالة خانة «أرشفة» بالكامل
- التعامل مع `isCompleted` عند التحديث/الإنشاء (إن وُجد UI منفصل للقسم المنتهي يُضاف لاحقًا — التقويم يبقى للنشطة فقط)

**تعديل:** اختبارات Frontend ذات الصلة (`personalReminders.test.ts`, `DayRemindersModal.test.tsx`) — تحديث التوقعات.

### 6.5 الصفحة المستقلة
**جديد:** `frontend/src/pages/StaleFilesPage.tsx`
- Mobile-first: جدول على `md+` داخل `overflow-x-auto`، بطاقات على الجوال (`useIsMobile()`)
- الفلاتر: `months` (1–24، افتراضي 4)، `court` (Distinct من API)
- الترقيم: `page/perPage` (20 افتراضي، 100 max) + `totalCount`
- ربط الصف/البطاقة بـ `Link to={`/documents/${r.documentId}`}`
- حالات: تحميل، خطأ (`role="alert"`)، فارغة
- استخدام `useCancellableRequest<PagedStaleFiles>` أو fetch مع AbortController (نمط `ArchivedDocumentsList.tsx`)
- عرض `lastActionOn` بصيغة `yyyy-MM-dd` (نص)، شارة Severity بلون مطابق

### 6.6 التنقل والتوجيه
**تعديل:** `frontend/src/App.tsx` ← إضافة Route:
```tsx
<Route path="/stale-files" element={<RequireRole role="lawyer"><StaleFilesPage /></RequireRole>} />
```
(بعد `CalendarPage` أو حسب الترتيب القائم، لا يتعارض مع `/documents/:id`).

**تعديل:** `frontend/src/components/Layout.tsx`
- إضافة عنصر تنقل للمحامي فقط (Sidebar + BottomNav) بعنوان «ملفات بلا إجراءات» مع أيقونة `ICONS.stale`
- `aria-label` واضح، حالة Active.

**تعديل:** `frontend/src/components/dashboard/Dashboard.tsx`
- إضافة بطاقة «ملفات بلا إجراءات» (بعد بطاقات التذكيرات) للمحامي فقط
- Badge عدّاد: `useBadgeCount('/stale-file-reminders/count', { enabled: isLawyer, shape: 'count' })`
- عرض أول 3 عناصر + زر «عرض الكل» إلى `/stale-files`

## 7) الاختبارات (إلزامي)

### 7.1 خلفية — Unit
**جديد:** `backend/tests/DocGenerator.Application.Tests/Common/StaleFileReminderCalculatorTests.cs`
- `IdleMonths`: last = today → 0; +1 يوم → 0; +30 يوم (نفس الشهر يوم أصغر) → 0; فرق شهر كامل (day>=lastDay logic) → 1، 4، 5، 6، 12
- `CutoffUtc`: AddMonths(-4)
- `Severity`: 0–5 info, 6–11 warn, >=12 critical
- `LastActivityOn`: تحويل UTC→tz، صيغة yyyy-MM-dd

**جديد:** `backend/tests/DocGenerator.Application.Tests/Services/StaleFileReminderServiceTests.cs`
- حصر الملكية (CreatedById)
- استبعاد: StruckOff, Executed, ReturnedToStart, RecoveredFromExecution, ExecutedOnIt, IsDraft
- تضمين: `منفذ جزئياً`، `تريث`
- الملف بلا إجراءات → effective = CreatedAt
- Skip/Take + TotalCount
- عزل بين محاميين
- فلتر Court

**جديد:** `backend/tests/DocGenerator.Integration.Tests/Controllers/StaleFileRemindersIntegrationTests.cs`
- lawyer → 200 (list/count/courts)
- head/manager/admin → 403
- ترقيم الصفحات (page/perPage)
- months خارج النطاق → 400
- perPage خارج النطاق → 400

### 7.2 خلفية — PersonalReminder (تحديث)
**تعديل:** `backend/tests/DocGenerator.Application.Tests/Services/PersonalReminderServiceTests.cs`
- حذف اختبارات الأرشفة
- تحديث رسالتَي السقف إلى النصوص الجديدة
- اختبار `IsCompleted`: SetOccurrenceAsync يرفض التذكير المنتهٍ، UpdateAsync يحوّله ويعيد الفتح
- CountActiveForUserAsync يُستثني المنتهية

**تعديل:** `backend/tests/DocGenerator.Integration.Tests/Controllers/PersonalRemindersIntegrationTests.cs`
- حذف `includeArchived=true`
- اختبار `includeCompleted=true/false`
- إنشاء/تحديث بـ `isCompleted`

### 7.3 واجهة — Frontend
**جديد:** `frontend/src/pages/__tests__/StaleFilesPage.test.tsx`
- عرض القائمة، الفرز (الأقدم أولاً)، الترقيم
- الحالة الفارغة، الخطأ
- فلتر Court، months
- رابط `/documents/:id` لكل صف/بطاقة
- Mobile: بطاقات (لا جدول)

**جديد:** `frontend/src/utils/__tests__/staleFiles.test.ts`
- `pluralMonths`: 0,1,2,3–10,11+

**تحديثات:** `Dashboard.test.tsx`, `Layout.test.tsx`, `personalReminders.test.ts`, `DayRemindersModal.test.tsx` — وفق التعديلات.

## 8) قائمة التحقق النهائية (إلزامية قبل إعلان الإنجاز)

1. [ ] **تدقيق العقود حقلًا بحقل** (واجهة→DTO→Service→Repository→عرض): أسماء/أنواع `string?` للتواريخ في DTO الجديد، بلا `DateTime?` في API Response للتواريخ العرضية.
2. [ ] **ActiveScope مطابق حرفيًا** لـ `DocumentRepository.ApplySearchFilters` (includeDrafts صحيح).
3. [ ] **EF لا يستخدم دوال داخل Where** (فكك `IsExecutedLike`).
4. [ ] **استعلام خطوتين** لـ MAX(ExecutionActions.CreatedAt) — لا GROUP BY على Text.
5. [ ] **فهرس** `IX_ExecutionActions_DocumentId_CreatedAt` موجود في SQLite+Postgres.
6. [ ] **الهجرات 4** مكتوبة للمحرّكين الصحيحين (boolean vs INTEGER).
7. [ ] **Down migrations** صحيحة، والتنبيه «غير قابل للاسترجاع» موثق.
8. [ ] **تنبيه النشر** مدرج في التقرير النهائي:
   ```bash
   dotnet ef database update --context DocGeneratorDbContext
   dotnet ef database update --context DocGeneratorPostgresDbContext
   ```
9. [ ] **Backend:** `dotnet test` — أخضر
10. [ ] **Frontend:** `npx oxlint src` — أخضر
11. [ ] **Frontend:** `npx tsc -b` — بدون أخطاء
12. [ ] **Frontend:** `npx vitest run` — أخضر
13. [ ] **Frontend:** `npm run build` — ناجح
14. [ ] **مراجعة المسار الكامل** عبر grep: لا بقايا `IsArchived` (عدا Migrations التاريخية/Designer حيث يجب بقاؤها)
15. [ ] **Mobile-first + useIsMobile()** مُطبّق، لا أبعاد ثابتة تسبب تجاوزًا أفقيًا
16. [ ] **A11y:** أزرار أيقونات بلا نص → `aria-label`، روابط `<Link>`، حقول مع `<label>` أو `aria-label`
17. [ ] **التواريخ**: عرض `yyyy-MM-dd` فقط (نص)، الإدخال يبقى حسب `Date Fields Rule` (لا يمسّ هذه الصفحة)

## 9) ملاحظات التنفيذ (أمان الدمج)

- **لا لمس DocumentService** — فصل كامل.
- **Phase A و B مستقلتان** — يمكن التسليم على مرحلتين إن رغب المستخدم (A أولاً ثم B) دون كسر.
- **التصحيح الدقيق لـ GetCourtsAsync** في التنفيذ: يُنفَّذ كاستعلام خفيف داخل Repo بنفس فلاتر ActiveScope + cutoff (بدون Any مع Subquery غير ضروري) — يُكتب مباشرة.
- **رسالة التنبيه في الواجهة** تُركَّب فقط من `staleSummaryText` (لا تُخزَّن).

**الخلاصة:** الخطة تلتزم تمامًا باختيارك (3)، وتحافظ على السقف 200 آمنًا من الأداء، وتعيد فائدة الأرشفة دون الطريق الموصد. **بانتظار الموافقة الصريحة قبل التنفيذ.**