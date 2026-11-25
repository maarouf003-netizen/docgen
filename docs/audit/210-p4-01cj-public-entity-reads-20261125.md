# `P4-01cj` — نقل قراءات سجل الجهات العشر إلى Query Handlers وحذف `IPublicEntityService` — 2026-11-25

> **النطاق:** المرحلة الحادية عشرة في سلسلة نقل ملكية `use-case`: إتمام مسح سجل الجهات
> المرجعية بتحويل **آخر عشرة أسطح قراءة** كانت على `IPublicEntityService` إلى استعلامات
> `IQuery<TResponse>` ومعالجات `QueryHandlerBase` تحت
> `backend/src/DocGenerator.Application/UseCases/PublicEntities/`، **قراءة صرفة بلا كتابة
> ولا معاملة ولا تدقيق جديد**. الفرع `refactor/p4-01cj-public-entity-reads` من `master`
> (`280ca26a`)، بحفاظ حرفي على السلوك والنطاق والرسائل.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | الملف |
|---|---|---|---|
| `ListPublicEntitiesQuery(Query)` | `ListPublicEntitiesQueryHandler` | `IPublicEntityRepository` | `ListPublicEntitiesQuery.cs` |
| `ListPublicEntitiesNeedingReviewQuery(Actor)` | `ListPublicEntitiesNeedingReviewQueryHandler` | `IPublicEntityRepository` + `IRepository<Branch>` | `ListPublicEntitiesNeedingReviewQuery.cs` |
| `CountPublicEntitiesNeedingReviewQuery(Actor)` | `CountPublicEntitiesNeedingReviewQueryHandler` | `IPublicEntityRepository` + `IRepository<Branch>` | `CountPublicEntitiesNeedingReviewQuery.cs` |
| `ListPublicEntityChangeEventsQuery(Query, Actor)` | `ListPublicEntityChangeEventsQueryHandler` | `IPublicEntityRepository` + `IRepository<Branch>` | `ListPublicEntityChangeEventsQuery.cs` |
| `ExportPublicEntityChangeEventsQuery(Query, Actor)` | `ExportPublicEntityChangeEventsQueryHandler` | `IPublicEntityRepository` + `IRepository<Branch>` + `IAuditLogger` | `ExportPublicEntityChangeEventsQuery.cs` |
| `PreviewPublicEntityImportQuery()` | `PreviewPublicEntityImportQueryHandler` | `IPublicEntityWriteRules` | `PreviewPublicEntityImportQuery.cs` |
| `ListPublicEntityGroupsQuery(Query, Actor)` | `ListPublicEntityGroupsQueryHandler` | `IPublicEntityRepository` + `IRepository<Branch>` | `ListPublicEntityGroupsQuery.cs` |
| `FindSimilarPublicEntityGroupsQuery(GroupId, Threshold, MaxResults)` | `FindSimilarPublicEntityGroupsQueryHandler` | `IPublicEntityRepository` | `FindSimilarPublicEntityGroupsQuery.cs` |
| `ListPublicEntityGroupEntriesQuery(GroupId, Actor)` | `ListPublicEntityGroupEntriesQueryHandler` | `IPublicEntityRepository` + `IRepository<Branch>` | `ListPublicEntityGroupEntriesQuery.cs` |
| `ListParentEditSuggestionsQuery(Query, Actor)` | `ListParentEditSuggestionsQueryHandler` | `IRepository<ParentEditSuggestion>` | `ListParentEditSuggestionsQuery.cs` |

| البند | التفصيل |
|---|---|
| العقود | `IPublicEntityService` حُذف نهائيًا (10 → 0 عضوًا) مع تسجيله في `DI`؛ ونُقل السجلان العامان `EntityRegistryActor` و`EntityRegistryListQuery` من ملف الواجهة إلى `Services/EntityRegistryActor.cs` قبل الحذف |
| الخدمة | بقي `PublicEntityService` **تنفيذًا وحيدًا لـ`IPublicEntityWriteRules`** (قرار موثّق أدناه)؛ تقلّصت حقوله **12 → 10** والمنشئ **12 → 10**: حُذف `_suggestions` بعد نقل آخر مستخدميه، و`_tx` الذي كان ميتًا أصلًا (تعريف وإسناد بلا استخدام) |
| الملفات الجزئية | حُذف `PublicEntityService.Registry.cs` بعد إفراغه بـ`git rm`؛ بقيت عشرة partials كلها غير فارغة (`Changes` صارت مساعدات قراءة فقط) |
| المساعدات | `ToEntryDto` و`NormalizeOptional` و`ToParentEditSuggestionDto` صارت `internal static` (مصدر واحد، بلا نسخ)؛ `GetFilteredChangeEventsAsync` و`ToChangeEventDto` صارتا `internal static` مع تمرير المستودعات صريحًا؛ `BuildImportCandidatesAsync` بقي على الخدمة ويستهلكه معالج المعاينة عبر `IPublicEntityWriteRules` |
| `DI` | عشرة تسجيلات `IQueryHandler<Query, Response>` جديدة في `DependencyInjection.cs`؛ الإجمالي **134 معالجًا / 134 تسجيلًا / 0 غير مسجَّل** |
| المتحكم | `EntityRegistryController` يحقن المعالجات العشرة بدل `IPublicEntityService` (26 → 35 معامل منشئ)، ويستدعيها؛ نفس المسارات والوسوم وفحوص القدرات ورموز الحالة وأجسام الاستجابة، و`ExportChangeEvents` يبقى `File(...)` حرفيًا |
| الاختبارات | جسر جديد `PublicEntityReadQueryExtensions` (عشرة امتدادات بنفس توقيعات الخدمة القديمة) وتحويل **59 موضع نداء** في `PublicEntityServiceTests` بلا تغيير أي توقّع؛ وتحديث مصانع البناء الثلاثة (`PublicEntityServiceTests`/`PB001NormalizationTests`/`PortalScopingTests`)؛ وحُذف حقل `_registry` الميت من `PB001NormalizationTests` وحُوِّل `_entities` في `PortalScopingTests` إلى متغير محلي للقواعد |

## قرارات موثّقة

- **بقاء `PublicEntityService`:** لم يُحذف الصنف لأنه التنفيذ الوحيد لـ`IPublicEntityWriteRules`
  الذي يستهلكه 17 معالج أمر؛ حُذف منه كل سطح قراءة عام، وبقي عقد الكتابة المشترك كما هو.
- **معاينة الاستيراد:** `PreviewImportAsync` كانت غلافًا سطريًا حول
  `BuildImportCandidatesAsync` التي لا تزال لازمة لـ`CommitPublicEntityImportCommand`
  عبر `IPublicEntityWriteRules`؛ استُهلك العقد القائم بدل تكرار 56 سطرًا، وهو حرّاس/بناء
  قراءة لا يكتب (نفس سابقة `P4-01cg` مع حرّاس التسمية).
- **تدقيق التصدير:** `ExportChangeEventsAsync` كانت — وما زالت — تكتب سطر تدقيق
  `"export_change_events"` بعد بناء الملف. هذا **أثرها الجانبي الوحيد** ونُقل حرفيًا؛ إسقاطه
  كان سيغيّر سلوكًا قائمًا. بقية الاستعلامات التسعة بلا أي تدقيق أو كتابة.

## إثبات الحفاظ على السلوك (لكل دالة)

| الدالة | أسطر الوالد/المعالج بعد التطبيع | `diffs` | الاستثناءات |
|---|---|---|---|
| `ListAsync` | 35 / 35 | 0 | — |
| `ListNeedsReviewAsync` | 15 / 15 | 0 | — |
| `CountNeedsReviewAsync` | 7 / 7 | 0 | — |
| `ListChangeEventsAsync` | 6 / 6 | 1 | تمرير `_entities`/`_branches` صريحًا إلى المساعد المشترك |
| `ExportChangeEventsAsync` | 9 / 9 | 1 | تمرير `_entities`/`_branches` صريحًا إلى المساعد المشترك |
| `PreviewImportAsync` | 1 / 1 | 1 | `_rules.BuildImportCandidatesAsync` بدل الاستدعاء الذاتي |
| `ListGroupsAsync` | 71 / 71 | 0 | — |
| `FindSimilarToGroupAsync` | 25 / 25 | 0 | — |
| `ListEntriesByGroupAsync` | 18 / 18 | 0 | — |
| `ListParentEditSuggestionsAsync` | 18 / 18 | 0 | — |
| **الإجمالي** | **205 / 205** | **3 أسطر موثّقة** | أسطر ربط `var request/actor = query.…` مستثناة من التطبيع |

- **المساعدات المسحوبة/الموسومة:** `GetFilteredChangeEventsAsync` 22/22 `diffs = 0` بعد
  تمرير المستودعين؛ `ToChangeEventDto` 17/17؛ `ToParentEditSuggestionDto` 19/19؛
  و`ToEntryDto`/`NormalizeOptional` تغيّر VisibleOnly من `private` إلى `internal static` بلا لمس الجسم.
- **مقارنة النصوص الحرفية (اتجاهان):** كل المجموعات `missing = 0` و`added = 0` بعد تطبيع
  اسم الربط داخل استيفاء واحد (`{query.Governorate}` ← `{request.Governorate}`)؛ رسائل
  الاستثناءات (`"المجموعة غير موجودة"`، `"المجموعة غير نشطة"`) ومعرّف التدقيق
  `"export_change_events"` ونصوصه كما هي حرفيًا.
- **النطاق/الصلاحيات:** فحص `isHead`/`BranchId`/`CreatedBy.BranchId`/`CreatedById` وترتيب
  الحرّاس كما هو؛ التحقق من القدرات بقي في المتحكم بنفس المواضع (`CanManageEntityRegistry`،
  `HasFullAccess`).
- **بلا كتابة:** فحص نصي شامل للملفات العشرة: لا `SaveChangesAsync`/`AddAsync`/`Remove`/
  `_uow`/`_tx`/`ITransactionalRequest`/`IAuditedRequest` — ما عدا سطر تدقيق التصدير القائم.

## مراجعة نقدية أولى

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل أُضيفت كتابة أو معاملة إلى أي استعلام؟ | فحص نصي للملفات العشرة | لا — صفر، عدا تدقيق التصدير القائم |
| 2 | هل تغيّر ترتيب الحرّاس أو شروط النطاق؟ | مقارنة سطرية بعد التطبيع | لا — `diffs = 0` في سبعة، وسطر ربط واحد في ثلاثة |
| 3 | هل تغيّر سطح `IPublicEntityService` بأكثر من الحذف المقصود؟ | عدّ آلي | حُذف كاملًا (10 → 0) مع نقل سجلَي السياق إلى ملف مستقل بلا تعديل حقول |
| 4 | هل تكرار مساعدات بين الخدمة والمعالجات؟ | بحث عن تعريف كل مساعد | تعريف واحد لكل من `ToEntryDto`/`ToParentEditSuggestionDto`/`GetFilteredChangeEventsAsync`/`ToChangeEventDto` |
| 5 | هل صار أي حقل أو معامل منشئ ميتًا؟ | عدّ كل حقل بعد النقل | الخدمة 10/10 مستخدمة؛ حُذف `_suggestions` و`_tx` الميتان |
| 6 | هل بقي مستدعٍ للطريقة على الخدمة في `src`؟ | بحث شامل | 0 — المتحكم يستدعي المعالجات، والخدمة لا تعرّف قراءات عامة |
| 7 | هل كل معالج مسجَّل في `DI`؟ | عدّ آلي مقابل الأصناف | 134/134/0 بلا تكرار |
| 8 | هل تغيّر مسار/صلاحية/شكل استجابة أي نقطة؟ | مقارنة المتحكم + `generate:api` | لا — نفس المسارات والفحوص ورموز الحالة، والعميل المولَّد بلا فرق |
| 9 | هل صار partial فارغًا أو بقي ملف ميت؟ | عدّ أعضاء كل partial | `Registry.cs` أُفرغ فعلًا فحُذف؛ 10 partials متبقية كلها غير فارغة |
| 10 | هل نُقل تدقيق التصدير أو تغيّر نصه؟ | مقارنة جسم الدالة | لا — نفس الموضع والنص والشرط، بعد بناء الصفوف وقبل إنشاء المصنّف |
| 11 | هل تغيّر سلوك `PreviewImport` (تاريخ المعاينة/العدّادات)؟ | مقارنة الدالة والمساعد | لا — `DateTime.UtcNow` ونفس `BuildImportCandidatesAsync` حرفيًا |
| 12 | هل تغيّر نطاق رئيس القسم في القوائم/العدّ/الأحداث/الاقتراحات؟ | مقارنة الشروط | لا — نفس الشروط والترتيب والفاصل الحتمي |
| 13 | هل أُضيفت هجرة أو تغيّر النموذج؟ | `has-pending-model-changes` | لا — نظيف |

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 (171 ملفًا) + `oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api`
بلا فرق · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 134/134/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات `PublicEntityServiceTests`) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
