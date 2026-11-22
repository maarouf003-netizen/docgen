# `P3-08d/e` — ميزانيات استعلامات القراءة المُسقَطة والمؤشر — 2026-11-22

> **النطاق:** تمديد حارس N+1 (`QueryBudgetTests`) إلى المسارات التي أُسقِطت في
> `P3-04b/c/d` وأُضيف لها مؤشر `P3-05`: القائمة الرئيسية المُسقَطة، صفحة المؤشر،
> القوائم الأرشيفية الأربع، تذكيرات الشطب، وتصدير البوابة المُسقَط.
> **لا تغيير سلوكي ولا هجرة.**

## 1. الميزانيات المثبتة

| المسار | الصفوف | السقف | الغرض |
|---|---:|---:|---|
| `GET /api/documents/list` | 15 | **20** | إسقاط القائمة + الأطراف الدفعية + إثراء الاستئناف |
| `GET /api/documents/list?cursor=…` | 10+10 | **20** | نفس المسار بلا `Skip` |
| `GET /api/documents/struck-off/list` | 15 | **18** | أرشيف مشطوب مُسقَط |
| `GET /api/documents/executed/list` | 15 | **18** | أرشيف منفذ مُسقَط |
| `GET /api/documents/referred-to-start/list` | 15 | **18** | أرشيف محال مُسقَط |
| `GET /api/documents/deleted/list` | 15 | **18** | محذوفات مع تجاوز فلاتر الأبناء |
| `GET /api/documents/strike-off-reminders/list` | 15 | **18** | اختيار بالمعرّفات + تحميل دفعي |
| `GET /api/portal/export` | 15 | **20** | تصدير البوابة المُسقَط (`P3-04d`) |

السقوف تُقاس بـ`QueryCounter` (Interceptor يعدّ كل أمر لكل طلب) وبهامش صغير؛
أي N+1 يضيف ≥15 استعلامًا فيتجاوز السقف فورًا. الأرقام الفعلية تُسجَّل بعد
تشغيل الاختبارات على `PostgreSQL` (انظر §3).

## 2. الاختبارات المضافة

- `DocumentsProjectedList_PageOfFifteen_StaysWithinQueryBudget`
- `DocumentsProjectedList_CursorPage_StaysWithinQueryBudget`
- `StruckOffList_PageOfFifteen_StaysWithinQueryBudget`
- `ExecutedList_PageOfFifteen_StaysWithinQueryBudget`
- `ReferredToStartList_PageOfFifteen_StaysWithinQueryBudget`
- `DeletedList_PageOfFifteen_StaysWithinQueryBudget`
- `StrikeOffRemindersList_PageOfFifteen_StaysWithinQueryBudget`
- `PortalExport_WithFifteenRows_StaysWithinQueryBudget`
- استُخرج `SeedPortalScopeAsync` لإعادة استخدام تهيئة نطاق المندوب بين قائمة
  البوابة وتصديرها.

## 3. المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | السقوف الأولية تقديرية؛ قد تكون مرتفعة فتفشل الاختبار | تشغيل على PostgreSQL | تُقاس الأرقام الفعلية وتُثبَّت السقوف بعدها (TBD) |
| 2 | الملفات غير المرقمة (مسودات) لا تدخل بعض التحولات/التذكيرات | `CreateDocumentAsync` بلا `registered` | كل اختبارات الحالة تنشئ ملفًا مرقّمًا (`registered: true`) |
| 3 | تكرار تهيئة نطاق المندوب بين القائمة والتصدير | كود الاختبار | `SeedPortalScopeAsync` مساعد واحد |
| 4 | عدّاد الاستعلامات قد يحسب تهيئة البذور | `counter.Reset()` بعد البذر | القياس يبدأ بعد التهيئة فقط |

## 4. القياس والتحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `QueryBudgetTests` (15) | **15/15** · 2m27s — كل المسارات ضمن السقوف |
| `Api` غير تسلسلي | **1562/1563**؛ الفشل الوحيد `PostgresTestDbDisposeTests.Dispose_CompletesWithoutPumpingSynchronizationContext` (50s) وأُعيد منفردًا **1/1** — تقلّب تحت الحمل لا علاقة له بالميزانيات |
| `Api` تسلسلي | **5/5** |

السقوف المثبتة (20/18/20) تُركت بهامش فوق القياس الفعلي لأن الهدف كشف N+1
(يزيد ≥15 استعلامًا)، لا تجميد العدد بدقة بايت.

## 5. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
