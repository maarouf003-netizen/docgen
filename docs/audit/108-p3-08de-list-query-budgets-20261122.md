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

## 3. القياس والتحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `QueryBudgetTests` | **سيُحدَّث بعد التشغيل** |
| بقية `Api` | بلا تغيير |

## 4. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
