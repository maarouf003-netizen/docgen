# `P3-04b` — Read model لقائمة الملفات الرئيسية — 2026-11-22

> **النطاق:** إضافة مسار `GET /documents/list` يعيد `DocumentListItemDto` مصغّرًا
> لقائمة الملفات الرئيسية، وإسقاط الاستعلام بلا `WithStandardIncludes`، وتحويل
> `DocumentsList` إليه. **المسار القديم `GET /documents` باقٍ بلا تغيير.**
> **لا هجرة ولا تغيير في بيانات/قواعد العرض.**

## 1. التصميم

1. `DocumentListRow` (Application): حقول بطاقة القائمة فقط + أرقام الأساس + تاريخ
   القيد + آخر إجراء + ملخصات الأطراف، ويحقّق `IDocumentExecutionState`.
2. `DocumentRepository.SearchListAsync`: نفس `ApplySearchFilters` والترتيب/الترقيم،
   ثم `Select` إلى `DocumentListRow` (بلا `Include` للكيان الكامل)، وقراءات دفعية
   مفهرسة لمعرّفات الصفحة (أرقام أساس/قيد/إجراءات/أطراف/إنابة مصدر).
3. `DocumentSearchService.SearchListAsync`: يبني **كيانًا مُجمَّعًا في الذاكرة** من
   الصف + العناصر الدفعية، ثم يستدعي `DocumentResponse.FromEntity` **نفسه** — فلا
   نسخة ثانية من قواعد الحالة/الرقم الفعّال/التدوير/آخر نشاط.
4. `DocumentListItemDto` + `DocumentsController.List` (`[HttpGet("list")]`) بنفس
   صلاحيات/فلاتر `Search`؛ `OpenAPI` والعميل المولَّد أُعيد توليدهما.
5. الواجهة: `DocumentListItemDto` نوعًا، وتحويل `DocumentsList` إلى
   `/documents/list`؛ توسيع مساعدات العرض (`isExecutedLike`/`fileNumberLabel`/
   `StatusSource`) لتقبل `null` من الفهرس المولَّد.

## 2. قياس A/B (نفس التشغيل، نفس البصمة `a3f7e28a…`، `50k`)

| الاستعلام | قديم (`SearchAsync`) | جديد (`SearchListAsync`) | التغيّر | الخطط |
|---|---:|---:|---:|---:|
| `documents-list-page1` | 145.89ms | **81.62ms** | **−44.1%** | 18 → 9 |
| `documents-list-text` | 1408.13ms | **329.41ms** | **−76.6%** | 18 → 9 |
| `portal-list-page1` (ضابط) | 117.97ms | 117.97ms | — | 5 |
| `stats-dashboard` (ضابط) | 275.11ms | 275.11ms | — | 3 |

القياسان الجديدان أُضيفا إلى كتالوج `P3-01` ويُنفَّذان في **نفس التشغيل** على نفس
القاعدة — فلا أثر لاختلاف البيئة (زمن البذر `164.7s`، والبصمة مطابقة).

## 3. ما كشفته المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | `execSubStatus` سقط من `DocumentListItemDto` | خطأ `tsc` في `StatusSource` ومسار الحالة الاحتياطي | أُضيف إلى DTO الخلفي والخريطة، وأُعيد توليد المخطط/العميل |
| 2 | `Document.GeneralEntitySide` غير قابل للفراغ بينما الصف nullable | `CS8601` | `row.GeneralEntitySide ?? GeneralEntitySideCatalog.Applicant` |
| 3 | `DocumentResponse` غير قابل للإسناد إلى `DocumentListItemDto` لأن `hasAppeals/needsRotation/isDraft` مطلوبة | أخطاء `tsc` في الصفحات الأخرى | جُعلت اختيارية في نوع الواجهة (الخادم يرسلها دائمًا) مع إبقاء `DocumentResponse` متوافقًا |
| 4 | مساعدات `displayFileNumber`/`isExecutedLike`/`fileNumberLabel` تقبل `undefined` فقط | أخطاء `TS2345` | توسيع التوقيعات إلى `null` (نمط `P6-06`) |
| 5 | اختبارات `DocumentsList` تراقب `/documents?` | فشل متوقع بعد تغيير المسار | حُدّثت مطابقات الاختبار إلى `/documents/list?` |
| 6 | الخطر الأكبر: انحدار عرض صامت | إعادة استخدام `FromEntity` + اختبار حقول | تغطية حقول البطاقة في اختبار جديد + بقاء المسار القديم للمقارنة |
| 7 | بقية القوائم الخمس | لم تُحوَّل في هذه الجولة | **P3-04c** موثّق: نفس النمط لكل قائمة (حذف/مشطوب/منفذ/محال/تذكيرات) |

## 4. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `SearchList_ProjectsCardFields...` | 1/1 |
| `ListEndpoint_ReturnsProjectedCardShape` | 1/1 |
| `Application` كامل | **2489/2489** (2488 + اختبار القائمة) · 0 فشل |
| `Api` كامل | **1549/1549** (غير تسلسلي) + **5/5** (تسلسلي) · 0 فشل |
| `npx tsc -b` | 0 |
| `vitest` كامل | **1808/1808** |
| `build` | ناجح |

## 5. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
