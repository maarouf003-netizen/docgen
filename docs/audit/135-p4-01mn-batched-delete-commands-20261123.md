# `P4-01m/n` — مرحلتان مجمّعتان: حذف إجراء التنفيذ وحذف المتدخل — 2026-11-23

> **قرار التجميع:** مرحلتان مستقلتان (`ExecutionActions` و`Interventions`) في PR واحد
> لتوفير دورة تحقق، مع مراجعة نقدية وفحص سلوك لكل منهما — كسابقتَي `k/l`.
> الـ`commit` واحد لأن `DocumentsController` و`DependencyInjection` مشتركان بين المرحلتين
> (وهو عرف الدفعات السابقة `k/l`)، أما ملفات التنفيذ فمعزولة تمامًا.

## `P4-01m` — حذف إجراء/ملاحظة تنفيذ

| قبل | بعد |
|---|---|
| `ExecutionActionService.DeleteExecutionActionAsync` تفتح `_tx.RunAsync` وتحفظ وتُدقّق | `DeleteExecutionActionCommand` + `DeleteExecutionActionCommandHandler` يملكان المعاملة والتدقيق |
| ضمن عقد `IExecutionActionService` | أُزيلت من العقد والتنفيذ (لا مستدعٍ غير المتحكم) |
| `DocumentsController.DeleteAction` يستدعي الخدمة | يستدعي `ICommandHandler<DeleteExecutionActionCommand, bool>` |

- **السلوك محفوظ حرفيًا:**
  - `false` عند غياب الإجراء أو عدم تطابق `DocumentId`، و`false` عند غياب المستند —
    قبل أي كتابة أو تدقيق (كالأصل).
  - رسالة التدقيق `"action"` بنفس النص: `حذف {TypeLabel}: {ToPlainText}` مع لاحقة
    `DocumentText.AuditWithActor`.
  - الترتيب داخل المعاملة: `Remove` ← `SaveChanges` ← `LogAsync` (بلا إبطال كاش — كالأصل).
- المتحكم: `DocumentsController.cs:931` — بلا تغيير في فحص الوصول (`ManageActions`)
  ولا في ترجمة `false` إلى `404`.

## `P4-01n` — حذف متدخل

| قبل | بعد |
|---|---|
| `DocumentInterventionService.DeleteAsync` تفتح `_tx.RunAsync` وتحذف وتُدقّق | `DeleteDocumentInterventionCommand` + معالج يملكان المعاملة والتدقيق |
| ضمن عقد `IDocumentInterventionService` | أُزيلت من العقد والتنفيذ (لا مستدعٍ غير المتحكم) |
| `EnsureSupportedSide` خاصة بالخدمة | `internal static` مشتركة بين الخدمة والمعالج (بلا نسخ منطق) |

- **السلوك محفوظ حرفيًا:**
  - `KeyNotFoundException` عند غياب الملف، و`ArgumentException` عند صفة «عرض وايداع»
    بنفس رسالة الأصل: `لا يمكن إضافة متدخل في ملف صفة «عرض وايداع»` (النص الأصلي
    يقول «إضافة» حتى في الحذف — نُقل كما هو).
  - `false` عند غياب المتدخل أو عدم انتمائه للملف (بلا حفظ أو تدقيق).
  - الحذف الصريح لوقعة `DocumentOccurrence` ثم المتدخل، ثم `SearchText` ← `UpdatedAt`
    (`DateTime.UtcNow`) ← `SaveChanges` ← تدقيق `"intervention"` بنفس النص:
    `حذف متدخلًا: {DisplayName}` مع لاحقة `AuditWithActor`.
- المتحكم: `DocumentsController.cs:599` — بلا تغيير في فحص الوصول (`Edit`) ولا في `404`.

## ملاحظة حدود المعاملة (مقبولة ومتسقة مع `a`–`l`)

في الأصل كانت قراءة المستند/الإجراء **قبل** `_tx.RunAsync`، وفي المعالج تُنفَّذ القراءات
داخل معاملة الوسم `ITransactionalRequest` (لأن `TransactionBehavior` يلتف حول النواة كاملة).
هذا هو **نفس** النمط المعتمد في الدفعات العشر السابقة (مثل `CreateBranchCommand` حيث
انتقلت قراءات التحقق إلى داخل المعاملة)، ولا يغيّر أي عقد أو نتيجة ظاهرة؛ بل يجعل
إعادة المحاولة (`ExecutionStrategy`) تشمل القراءة والكتابة معًا. في `n` تعني أيضًا أن
مسار «المتدخل غير موجود» يفتح معاملة ويُكملها بلا كتابة — أثر داخلي صرف، لا سلوك.

## المراجعة النقدية — `P4-01m`

| # | الوجد | الدليل (ملف:سطر) | الإصلاح | الحالة |
|---|---|---|---|---|
| 1 | إزالة عضو من العقد | `IExecutionActionService.cs` (بلا `DeleteExecutionActionAsync`)؛ بحث شامل: صفر مستدعٍ | أُزيلت من العقد والتنفيذ، والمتحكم يفوّض للأمر | ✅ |
| 2 | مطابقة الرسائل والتدقيق | الأصل عند الوالد `ExecutionActionService.cs:135-139` ↔ `DeleteExecutionActionCommand.cs:52-54` | مطابقة حرفية: `حذف`، `TypeLabel`، `ToPlainText`، `"action"`، `AuditWithActor` | ✅ |
| 3 | حدود المعاملة | الأصل: `Remove`/`Save`/`Audit` داخل `_tx.RunAsync` | المعالج موسوم `ITransactionalRequest`؛ القراءات والأثر داخل المعاملة (انظر الملاحظة) | ✅ |
| 4 | ترتيب التدقيق | لا كاش؛ حفظ ← تدقيق | نفس الترتيب | ✅ |
| 5 | الإسقاط `DTO` | الناتج `bool` فقط | `false`/`true` بنفس الشروط | ✅ |
| 6 | الحالات الحدّية | إجراء غائب/ملف مختلف/مستند غائب | `false` قبل أي أثر جانبي (كالأصل) | ✅ |
| 7 | الاختبارات | `ServiceTests.cs:3037,3054` + تأكيد جديد للنص | مساعد بنفس التوقيع + تأكيد `حذف إجراء: إجراء — المنفذ عليه: أحمد خالد الخطيب` | ✅ |
| 8 | الحجم والأصول | المعالج 58 سطرًا، بلا تكرار منطق | مساعدات الأصل (`DocumentText`/`HtmlInputSanitizer`) مستعملة كما هي | ✅ |
| 9 | البناء والتنسيق | `build -warnaserror` / `format --verify-no-changes` | 0/0 ونظيف | ✅ |
| 10 | العزل | ملفات المرحلة: المعالج + DI + المتحكم + `ServiceTests` | لا تداخل مع ملفات `n` (عدا الملفين المشتركين) | ✅ |

## المراجعة النقدية — `P4-01n`

| # | الوجد | الدليل (ملف:سطر) | الإصلاح | الحالة |
|---|---|---|---|---|
| 1 | إزالة عضو من العقد | `IDocumentInterventionService.cs` (بلا `DeleteAsync`)؛ بحث شامل: صفر مستدعٍ | أُزيلت من العقد والتنفيذ | ✅ |
| 2 | مطابقة الرسائل والتدقيق | الأصل عند الوالد `DocumentInterventionService.cs:132-152` ↔ `DeleteDocumentInterventionCommand.cs:41-59` | مطابقة حرفية للرسالة والتدقيق، بما فيها «إضافة» في رسالة الحذف | ✅ |
| 3 | حدود المعاملة | الأصل: الحذف/الحفظ/التدقيق داخل `_tx.RunAsync` | موسوم `ITransactionalRequest`؛ نفس الأثر داخل المعاملة (انظر الملاحظة) | ✅ |
| 4 | ترتيب التدقيق وإبطال الكاش | لا كاش: وقعة ← متدخل ← `SearchText` ← `UpdatedAt` ← حفظ ← تدقيق | نفس الترتيب حرفيًا | ✅ |
| 5 | الإسقاط `DTO` | الناتج `bool` | نفس الشروط | ✅ |
| 6 | الحالات الحدّية | ملف غائب/صفة «عرض وايداع»/متدخل لغيره | `KeyNotFoundException`/`ArgumentException` بنفس النص/`false` | ✅ |
| 7 | الاختبارات | `DocumentInterventionServiceTests.cs` + اختبارا انحدار جديدان | مساعد بنفس التوقيع + تأكيد التدقيق + اختبارا الغياب/الصفة | ✅ |
| 8 | الحجم والأصول | المعالج 62 سطرًا؛ `EnsureSupportedSide` صارت `internal static` | لا نسخ منطق التحقق | ✅ |
| 9 | البناء والتنسيق | `build -warnaserror` / `format --verify-no-changes` | 0/0 ونظيف | ✅ |
| 10 | العزل | ملفات المرحلة: المعالج + الخدمة/العقد + DI + المتحكم + اختبارات | المتحكم وDI فقط مشتركان مع `m` | ✅ |
| 11 | بديل الاختبار الوحدوي للمتحكم | `DocumentAccessProjectionTests` كان يراقب `DeleteAsync` في البديل | بديل `ICommandHandler<DeleteDocumentInterventionCommand, bool>` يراقب التفويض | ✅ |
| 12 | ثغرة في اختبار الصفة الجديد (كشفها `backend-postgres`) | `DocumentInterventionServiceTests.Delete_DepositSide_ThrowsWithOriginalMessage`: `AddAsync` ينادي `ClearChanges` فيفصل `_document`، فلم يُكتب التغيير | الكتابة عبر `ExecuteUpdateAsync` مباشرة في القاعدة ثم إعادة تحميل المعالج لها | ✅ |

## التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0 تحذير / 0 خطأ |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| اختبارات مركزة (`DocumentServiceTests` + `DocumentInterventionServiceTests`) | 0 نجح / 290 متجاوز (Postgres-gated) / 0 فشل |
| اختبارات وحدة المتحكم (`DocumentAccessProjection`/`DocumentGenerationDisabled`/`TransferBranchGuard`) | **7/7** |
| `Application` (بلا PostgreSQL) | **757 نجح / 1657 متجاوز** (‏+2 اختبار Postgres جديد) **/ 0 فشل** |
| `Api` غير التسلسلي | **901 نجح / 615 متجاوز / 0 فشل** |
| `Api` التسلسلي | **0 نجح / 5 متجاوز / 0 فشل** |
| الحرّاس الستة | نظيفة |

## الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6** (`P0-04`, `P0-06`, `P0-07`, `P0-07b`,
`P3-02` ×2)، ولم يُشغَّل `dotnet ef database update` لأي منها.

> لم تُحرَّر اللقطات المجمّدة `128`–`134`؛ هذا التحديث في لقطة جديدة مؤرخة.
