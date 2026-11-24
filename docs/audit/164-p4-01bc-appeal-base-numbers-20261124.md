# `P4-01bc` — كتابات أرقام الأساس الاستئنافية إلى Commands — 2026-11-24

> **قرار النطاق:** مسارا الكتابة في `DocumentAppealService.BaseNumbers.cs`
> (`SaveBaseNumbersAsync` و`CorrectBaseNumberAsync`) ككتلة واحدة لأنهما يكتبان
> `AppealBaseNumber` مباشرة تحت سياسة `AppealBaseNumberPolicy` نفسها؛ وبقي
> `GetBaseNumberHistoryAsync` قراءة في الخدمة.

## قبل/بعد

| قبل | بعد |
|---|---|
| `SaveBaseNumbersAsync`/`CorrectBaseNumberAsync` (في `BaseNumbers.cs`) | أمران ومعالجان في `UseCases/Appeals/AppealBaseNumberCommands.cs` |
| ضمن عقد `IDocumentAppealService` | أُزيلا؛ بقي `GetBaseNumberHistoryAsync` وقراءتا المتابعة/الإحالة |
| `AppealsController` ينادي الخدمة | ينادي المعالجين المسجَّلين صراحةً |
| `BaseNumbers.cs` = قراءة + كتابتان | قراءة فقط (106 سطرًا محذوفًا) |

**حدود المعاملة:** تحميل الاستئناف والحرّاس والتحقق خارج المعاملة كما في الأصل، ثم
معاملة واحدة للحفظ/التدقيق. لا وسم `ITransactionalRequest` لأن الأصل يملك معاملته
(بلا تداخل مع `TransactionBehavior`).

## السلوك المحفوظ حرفيًا

1. **التدوير:** حارس المتابع (`AssignedLawyerId != UserId`) ثم حارس `Pending`، ثم
   شرط الإدخال الواحد للسنة الحالية، ثم منع إعادة تدوير سنة مسجّلة («استخدم التصحيح»)،
   ثم `Bounded(..., MaxNumberLength, ...)`، وإلحاق صف `AppealBaseNumber` جديد مع
   `CreatedById`/`CreatedAt = DateTime.UtcNow` وبقاء السجلات السابقة.
2. **التصحيح:** `EnsureAssignedFollower` (`UnauthorizedAccessException`)، ثم
   `EnsureAllowedYear`، ثم تعديل أحدث صف للسنة في مكانه، وإلا حقول القيد الأصلية
   للعام نفسه، وإلا `"لا يوجد رقم أساس مسجّل لسنة {year}"`.
3. رسالتا التدقيق `rotate_appeal_base_number` و`correct_appeal_base_number` بنصهما
   الحرفي وترتيبهما بعد `SaveChangesAsync`.

## المراجعة النقدية

| # | الوجد | الدليل | الإصلاح | الحالة |
|---|---|---|---|---|
| 1 | إزالة عضوي كتابة من العقد | `IDocumentAppealService.cs`؛ `rg` عبر `backend/` | أُزيلا، ولا مستدعي خدمة متبقٍّ | ✅ |
| 2 | حرفية النصوص (اتجاهان) | مقارنة آلية مع الوالد `fc149245` | 15/15 حرفيًا و0 انحراف/0 إضافة | ✅ |
| 3 | حدود المعاملة | الأصل `BaseNumbers.cs:26-123` | مطابقة: حرّاس خارجًا ثم معاملة واحدة بلا وسم | ✅ |
| 4 | تأكيدات نصية | `DocumentAppealServiceTests` | `Equal` على تدقيق التدوير والتصحيح | ✅ |
| 5 | مواضع الاختبار | 18 استدعاءً (11 حفظ + 7 تصحيح) | امتدادات بالتوقيع القديم + مصنع معالجين | ✅ |
| 6 | تسجيل `DI` | `DependencyInjection.cs` | 69 معالجًا / 0 غير مسجَّل | ✅ |
| 7 | الكود الميت | `BaseNumbers.cs` بعد النقل | الملف قراءة فقط، والوصول للمستودع عبر الحقل القائم | ✅ |
| 8 | الهجرات | لا تغيير مخطط | لا هجرة جديدة؛ المعلّق يبقى 6 | ✅ |

## التحقق

| البوابة | النتيجة |
|---|---|
| `build -warnaserror` / `format` | 0/0 ونظيف |
| مراجعة الحروف العربية (اتجاهان) | **0 انحراف / 0 إضافة** |
| `DI` | 69 / 0 |
| `Architecture` | 29/29 |
| `Application` | 758 / 1659 / 0 |
| `Api` غير التسلسلي / التسلسلي | 901 / 615 / 0 · 0 / 5 / 0 |
| الحرّاس الستة | نظيفة |

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.

> لم تُحرَّر اللقطات `128`–`163`.
