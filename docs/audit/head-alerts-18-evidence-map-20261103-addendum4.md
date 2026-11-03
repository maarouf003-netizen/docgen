# ملحق 4 — خريطة أدلة §18: مرساة دمج «حزمة إصلاح مراجعة التنبيهات» (2026‑11‑03)

> ملحق رابع مؤرَّخ للوثيقة `head-alerts-18-evidence-map-20261103.md`. **الأصل وملاحقه 1–3
> لا تُحرَّر** (سياسة `docs/audit`)؛ كل ما يُسجَّل هنا جديد ومؤرَّخ.
>
> **الغرض:** تثبيت مرساة الدمج للبنود الستة في «حزمة إصلاح مراجعة التنبيهات» مع أدلتها
> وملفاتها وأرقام بواباتها، ليصبح كل بند قابلًا للتتبّع من الخطة (§18) إلى الكود والاختبار.

---

## 1) المرساة

| العنصر | القيمة |
|---|---|
| الفرع | `fix/head-alerts-review-closure` (من `master` = `c570a36`) |
| `PR` | [#79](https://github.com/maarouf003-netizen/docgen/pull/79) |
| التزامات الفرع | `c391544` (بند 1) → `c8a3fa5` (بندا 3+4) → `bb8dad8` (بند 6) → `cc2631b` (بند 5 + القراران) |
| هاش الدمج على `master` | **`b7997eb`** |
| `CI` على رأس الفرع | **6/6** `success` (`backend` · `backend-postgres` · `frontend` · `guards` · `audit` · `migration-guard`) |
| هجرات | **لا شيء** (لا `SQLite` ولا `PostgreSQL`) |

## 2) البنود وأدلتها

| البند | الادعاء المُثبَت | ملف الدليل |
|---|---|---|
| `§18:3081` | فشل معاملة العمل بعد إدراج التنبيه لا يُنتج تنبيهًا جزئيًا؛ الصف يعود `pending` بمحاولة واحدة و`conflict_retry`، ثم تنجح المحاولة الثانية أو يُسجَّل `failed` | `backend/tests/DocGenerator.Application.Tests/HeadAlertOutboxDispatcherTests.cs` (مع `ConflictInjectingAlertService` / `ConflictGate` / `ThrowingAuditLogger`) |
| `§18:3083` | خطة `Supersede` مع حدث `Unchanged` تؤرشف التنبيه القديم، تُبقي الجديد، ولا تُنشئ صفًا توأمًا | `HeadAlertSystemWriteTests.CreateSystemAsync_SupersedePlanOnUnchanged_ArchivesOldAndKeepsCurrent` |
| `§18:3118` | نص 12,000 حرف حقيقي (`'ا'.repeat`) يُجلب عند الفتح ويُعرض كاملًا بلا قص أو تمرير أفقي | `frontend/src/components/dashboard/AlertDetails.test.tsx` |
| `§18:3119` | `AlertDetailsSheet`: `Escape` والإغلاق يستدعيان `onClose`، `role="dialog"` + `aria-modal="true"` + `aria-labelledby`، `overscroll-contain` داخلي، بلا تمرير أفقي | `frontend/src/components/dashboard/AlertDetailsSheet.test.tsx` (7 اختبارات) |
| بند 5 | تصحيح أعداد خريطة الأدلة: `EnqueueSystemAsync` = 23 موضعًا، الأرشفة = 8+2، «31 catch» تاريخي بلا أثر حالي | `docs/audit/head-alerts-18-evidence-map-20261103-addendum3.md` |
| بند 6 | `GET /api/alerts/{id}` و`/{id}/recipients` و`PATCH /{id}/read` تُرجع **404** بجسم عربي `{ message }` بلا `title` إنكليزي | `backend/src/DocGenerator.Api/Controllers/AlertsController.cs` + `AlertsIntegrationTests.MissingAlert_NotFoundBody_IsArabicMessageWithoutEnglishTitle` |

## 3) قرارا المالك المسجَّلان في هذه الحزمة

- **(أ) شريط الإنابة بلا سطح إجراء:** `§18:3056` أُعيدت صياغته إلى «تعليم القراءة متاح من
  صفحة `/alerts` وحدها؛ شريط الإنابة معاينة سياق للقراءة فقط بلا سطح إجراء» مع ملاحظة
  مؤرخة 2026‑11‑03، متسقة مع إزالة `AlertsPanel` في `PR‑5b`.
- **(ب) 404 عربية موحّدة في `AlertsController`:** ثابت واحد `NotFoundMessage`
  («التنبيه غير موجود أو لا تملك صلاحية عرضه») للأسطح الثلاثة، وأُضيف بند دين تقني في
  `§9.1` لتوحيد 404 العربية في **كل** المتحكمات (مؤجَّل؛ بلا أثر على المستخدم لأن
  `getApiErrorMessage` يحوّل 404 إلى نص عربي في الواجهة أصلًا).

## 4) أرقام البوابات (محليًا، على رأس الفرع)

| البوابة | النتيجة |
|---|---|
| `dotnet build -v q` | 0 تحذير / 0 خطأ |
| `dotnet test` | **2943 ناجح / 0 فاشل** (`Application` 2183 · `Api` 760) |
| `npx oxlint src` | نظيف |
| `npx tsc -b` | نظيف |
| `npx vitest run` | **160 ملفًا / 1680 اختبارًا** ناجحًا |
| `npm run build` | ناجح |
| `node scripts/subhead-guards.mjs` | نظيف |

## 5) حدود هذا الملحق

- لا يعدّل الأصل ولا الملاحق 1–3، ولا يغيّر حالة أي بند خارج البنود الستة أعلاه.
- **لا يدّعي سلوكًا غير موجود:** `AlertDetailsSheet` لا يطبّق حصر تركيز (focus trap)،
  والاختبار يوثّق فقط: `Escape`، زر الإغلاق، سمات الحوار، `overscroll-contain`، وأهداف
  اللمس `min-h-11`/`min-w-11`.
- **لا هجرة قاعدة بيانات** في هذه الحزمة؛ قائمة الهجرات المعلّقة في `RUN_GUIDE.md` §9
  لم تتغيّر (تبقى هجرات `SQLite` الأربع بانتظار نافذة النشر).
