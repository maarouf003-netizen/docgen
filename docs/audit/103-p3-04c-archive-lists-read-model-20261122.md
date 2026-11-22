# `P3-04c` — Read model لقوائم المحامين الخمس — 2026-11-22

> **النطاق:** تحويل القوائم الخمس المتبقية من `DocumentResponse` الكامل
> (`WithStandardIncludes`) إلى إسقاط صف القائمة نفسه المستخدم في `P3-04b`:
> المحذوفة، المشطوبة، المنفذة، المحالة للبداية، وتذكيرات الشطب.
> **كل المسارات القديمة باقية بلا تغيير، والجديد إضافي.**
> **لا هجرة ولا تغيير في بيانات/قواعد العرض.**

## 1. التصميم

1. **`DocumentListRow`** (Application): توسّعت بحقول العرض الخاصة بالأرشيف —
   `Version` و`DeletedAt` و`StruckOffDate` و`ExecutedExecutionDate` و
   `ExecutedDepositDate` و`ForcedExecutionDate` و`BaraetDate` و`StartReferralDate`.
2. **`DocumentListItemDto.FromRow`**: النقل الوحيد لبناء الكيان المؤقت واستدعاء
   `DocumentResponse.FromEntity` نفسه — فلا نسخة ثانية من قواعد `displayStatus`/
   الرقم الفعّال/التدوير/آخر نشاط. (نُقل من `DocumentSearchService.ToListItem`
   ليتشاركه البحث وتذكيرات الشطب.)
3. **`DocumentRepository`**:
   - `ListRowSelector` تعبير إسقاط واحد لكل القوائم.
   - `LoadListRowChildrenAsync(items, ct, includeDeletedChildren)` — قراءات دفعية
     مفهرسة لمعرّفات الصفحة (أرقام أساس/قيد/إجراءات/أطراف/إنابة مصدر)، ومع
     `includeDeletedChildren: true` تُتجاوز فلاتر الأبناء لقائمة المحذوفات فقط
     (لأن كل جدول ابن يحمل `HasQueryFilter(... Document.IsDeleted)`).
   - بناة استعلام مستخرجة ومشتركة حرفيًا بين القراءة الكاملة والمُسقَطة:
     `BuildDeletedQuery` / `BuildStruckOffQuery` / `BuildExecutedQuery` /
     `BuildReferredToStartQuery`. لا نسخ لمنطق النطاق أو الفلاتر.
   - `GetListRowsByIdsAsync` لتحميل بطاقات معرّفات مختارة سلفًا في SQL
     (تذكيرات الشطب) بإسقاط خفيف.
4. **`DocumentSearchService`**: `SearchDeletedListAsync` / `SearchStruckOffListAsync` /
   `SearchExecutedListAsync` / `SearchReferredToStartListAsync`، كلها تستخدم
   `BuildListResult` نفسه. **`StrikeOffReminderService.GetListAsync`** يعيد استخدام
   `SearchStrikeOffReminderIdsAsync` (الاختيار/العدّ/الترتيب/الترقيم في SQL) ثم
   يحمّل البطاقات ويعيد ترتيبها صريحًا كما في المسار القديم.
5. **`DocumentsController`** — مسارات إضافية بنفس الصلاحيات/النطاق/الفلاتر:
   - `GET /api/documents/deleted/list`
   - `GET /api/documents/struck-off/list`
   - `GET /api/documents/executed/list`
   - `GET /api/documents/referred-to-start/list`
   - `GET /api/documents/strike-off-reminders/list`
6. **الواجهة**: الصفحات الخمس تستخدم `DocumentListItemDto` والمسارات الجديدة؛
   `RenewalModal` يقبل النوع المُسقَط (يحتاج `version`)؛ وُسّع
   `sourceLabel(source?: LastActivitySource | string | null)` بدل أي `as`.
7. **OpenAPI/العميل**: أُعيد توليد اللقطة `docs/openapi/docgen-v1.json` ثم
   `npm run generate:api`.

## 2. تدقيق العقود (حقلًا بحقل)

| الحقل | قاعدة البيانات | `DocumentListRow` | `DocumentListItemDto` | تستهلكه الواجهة |
|---|---|---|---|---|
| `id/createdAt/updatedAt` | `Documents` | ✅ | ✅ | المفتاح والترتيب/العرض |
| `version` | `Documents.Version` | ✅ | ✅ | `RenewalModal` (RF-010) |
| `borrowerName/Father/Family` | `Documents` | ✅ | ✅ | `fullName` |
| `applicant` | `Documents` | ✅ | ✅ | `applicantName` |
| `generalEntitySide` | `Documents` | ✅ | ✅ | `isExecutedLike`/`fullName` |
| `execStatus/execSubStatus/executedStatus/isDraft/displayStatus` | `Documents` عبر `FromEntity` | ✅ | ✅ | شارة الحالة |
| `fileNumber/displayFileNumber/fileType/isDraft` | `Documents` + أرقام الأساس | ✅ | ✅ | `displayFileNumber` |
| `branchName/court` | `Documents` + `Branches` | ✅ | ✅ | سطر البطاقة/الجدول |
| `sourceDelegationId` | `Documents` | ✅ | ✅ | إخفاء إعادة المناب (F9/و7) |
| `deletedAt` | `Documents.DeletedAt` | ✅ | ✅ | عمود «تاريخ الحذف» |
| `struckOffDate` | `Documents.StruckOffDate` | ✅ | ✅ | عمود/سطر «تاريخ الشطب» |
| `executedExecutionDate/executedDepositDate` | `Documents` | ✅ | ✅ | «تاريخ التنفيذ» |
| `delegationReturnDate` | `DocumentDelegations.ReturnDate` | ✅ | ✅ | تنفيذ المناب |
| `baraetDate/forcedExecutionDate` | `Documents` | ✅ | ✅ | تاريخ التنفيذ حسب الحالة |
| `startReferralDate` | `Documents.StartReferralDate` | ✅ | ✅ | عمود «تاريخ الإحالة» |
| `executionApplicants/executedNaturalPersons/executedPublicEntities` | جداول الأطراف | ✅ | ✅ | `applicantName`/`fullName` |
| `lastActivityDate/lastActivitySource` | إجراءات/قيد/إنشاء | ✅ | ✅ | «تاريخ آخر إجراء» |
| `executionActions` | `ExecutionActions` | ✅ | ✅ | حقل مشترك مع P3-04b |

كل حقل أُضيف في `P3-04c` له مستهلك فعلي في إحدى الصفحات الخمس؛ ولا حقل زائد.

## 3. قياس A/B — نفس التشغيل ونفس البصمة (`50k`)

- المقياس: `50000`، البذرة `20261121`، زمن البذر **153.7s**،
  البصمة **`a3f7e28ace7f054f2c74aa8c558f72d6`** (مطابقة لـ`P3-04b`).
- البناء `Release` على `PostgreSQL` محمول محلي؛ `pg_stat_statements` غير متوفرة
  (سُجّل غيابها ولم تُختلق أرقامها).

| الاستعلام | قديم (p50/p95) | جديد (p50/p95) | التغيّر p50 | الصفوف | البصمة | الخطط |
|---|---:|---:|---:|---:|---|---:|
| `documents-struck-off-page1` | 596.63 / 612.26ms | **70.18 / 76.58ms** | **−88.2%** | 20 | `total=589;first=49998` (متطابقة) | 17 → 9 |
| `documents-executed-page1` | 816.12 / 865.00ms | **89.87 / 95.48ms** | **−89.0%** | 20 | `total=14392;first=49994` (متطابقة) | 18 → 9 |
| `strike-off-reminders-page1` | 355.73 / 379.70ms | **275.88 / 285.67ms** | **−22.4%** | 20 | `total=13519;first=2` (متطابقة) | 19 → 10 |
| `documents-deleted-page1` | 23.15 / 27.97ms | 19.73 / 25.84ms | −14.7% | 0 | `total=0;first=` (متطابقة) | 2 → 2 |
| `documents-referred-to-start-page1` | 58.53 / 62.97ms | 52.81 / 59.28ms | −9.8% | 0 | `total=0;first=` (متطابقة) | 2 → 2 |

**ملاحظة صدق:** البذرة الحتمية لا تُنتج ملفات محذوفة ولا محالة للبداية، فقياس هذين
الصفين يثبت **تطابق النتيجة والشكل** لا تحسّن الزمن؛ الأرقام المهمة هي الثلاثة
غير الفارغة.

**ضوابط عدم التدهور** (استعلامات لم تُمسّ، نفس التشغيل):

| الضابط | P3-04b | P3-04c | الحكم |
|---|---:|---:|---|
| `documents-list-page1` | 145.89ms | 124.76ms | تحسّن |
| `documents-list-text` | 1408.13ms | 1323.34ms | تحسّن |
| `portal-list-page1` | 117.97ms | 118.12ms | ثابت (ضوضاء) |
| `stats-dashboard` | 275.11ms | 246.51ms | تحسّن |

التقرير الخام أُنتج محليًا في `docs/audit/measurements/20261122-p3-01-50000.{json,md}`
ولم يُودَع في المستودع التزامًا بسياسة الاحتفاظ (`docs/audit/measurements/README.md` §2):
هذا قياس تحقّق لا خط أساس جديد، وحجم JSON تجاوز حد `8MB`.

## 4. ما كشفته المراجعة النقدية

| # | الوجد | الدليل | الإصلاح | متبقٍ |
|---|---|---|---|---|
| 1 | **أبناء الملف المحذوف تسقط** لأن كل جدول ابن يحمل فلترًا يستثني أبناء المحذوف، بينما المسار القديم كان `IgnoreQueryFilters` على الاستعلام الكامل | `*Configuration.HasQueryFilter(... Document.IsDeleted)`؛ اختبار يقارن القديم/الجديد | `LoadListRowChildrenAsync(..., includeDeletedChildren: true)` لقائمة المحذوفات فقط + اختبار يثبت تطابق الأطراف/آخر نشاط/الرقم الفعّال | لا |
| 2 | `RenewalModal` يحتاج `version` (RF-010) وهو غير موجود في DTO القائمة | خطأ `tsc` بعد تحويل النوع | أُضيف `Version` إلى الصف والDTO والمخطط المولَّد | لا |
| 3 | `lastActivitySource` في النوع المولَّد `string \| null` أوسع من اتحاد `LastActivitySource` | خطأ `tsc` في `sourceLabel` | توسيع التوقيع إلى `LastActivitySource \| string \| null` (بلا `as`) | لا |
| 4 | احتمال تعارض توجيه `.../list` مع `{id:int}` | فحص المسارات + اختبارات API ناجحة | لا تعارض (مقطع حرفي إضافي) | لا |
| 5 | ترقيم `perPage=0` يختلف بين `P3-04b` (20) والقوائم القديمة (`Clamp` → 1) | قراءة المسارات القديمة | المسارات الجديدة تُبقي تطبيع القديم حرفيًا | لا |
| 6 | ترتيب تذكيرات الشطب قد يضيع بعد التحميل الدفعي | المسار القديم كان يعيد الترتيب صريحًا | `GetListAsync` يعيد الترتيب من `rows` + اختبار ترتيب/عدّ | لا |
| 7 | تعقيم `PrintCount` في المسارات القديمة | `OkSanitized` | الDTO لا يحمل العدّاد أصلًا فلا حاجة لتعقيم | لا |
| 8 | خلاصة الحقول: هل هناك حقل عرض غير مُسقَط؟ | مقارنة كل صفحة بحقولها | لا (الجدول في §2) | لا |

## 5. الاختبارات (قبل/بعد)

| البوابة | قبل (`P3-04b`) | بعد (`P3-04c`) | الفرق |
|---|---:|---:|---:|
| `Application` | 2489 | **2493** | +4 |
| `Api` غير تسلسلي | 1549 | **1551** | +2 |
| `Api` تسلسلي | 5 | **5** | 0 |
| `vitest` | 1808 | **1808** | 0 (حُدّثت مطابقات المسارات) |

اختبارات `P3-04c` الجديدة:

- `SearchDeletedList_ProjectsDeletedCardFields` (تطابق القديم/الجديد بما فيه أبناء المحذوف).
- `SearchStruckOffList_ProjectsStruckOffCardFields`.
- `SearchExecutedList_ProjectsExecutedCardFields`.
- `SearchReferredToStartList_ProjectsReferralCardFields`.
- `ArchiveListEndpoints_ReturnProjectedCardShape_WithListSpecificFields` (API للشكل المُسقَط).
- `ProjectedListEndpoint_MatchesLegacySelection_AndCarriesCardFields` (تذكيرات الشطب: عدّ/ترتيب/حقول).

اختبارات المسارات القديمة كلها باقية وناجحة.

## 6. بقايا المصطلحات وفروع المزوّد الصامتة

- `TestDb` / `TestDatabase` / `UseSqlite` / `SqliteException` / `usePostgres`:
  **لا مطابقات** في `backend/src` أو `backend/tools`.
- الإشارات إلى `SQLite` الباقية **تعليقات تاريخية** فقط (مثل
  `DependencyInjection.cs` و`Program.cs` التي تنص أن SQLite غير مدعوم في التشغيل —
  `ADR-0001`)، ولا مسار تشغيل ثانٍ.
- `ProviderName`: فحصان يرفضان غير Npgsql صراحةً (`GetByIdForUpdateAsync` و
  `AcquireScopeLockAsync`) — سلوك «رفض لا تخطٍّ» صحيح وغير ممسوس.
- كود `P3-04c` إضافي كله EF LINQ بلا أي فرع مزوّد.

## 7. الاصطلاحات

- `PostgresTestDb` و`[PostgresFact]` في كل اختبارات القاعدة الجديدة؛ لا تخطي.
- أرقام `test-report` مولَّدة بـ`node scripts/test-report.mjs <trx> --label "…"`.
- لا تغيير في تنسيق العملة/الأرقام (`Intl.NumberFormat`) ولا في قواعد `RF-014`
  للتواريخ الحرة — التواريخ تُسقَط كما هي من `Documents` ثم ينسّقها `FromEntity`.

## 8. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6** كما هي، ولم تُمس ملفات الهجرات.

## 9. CI والحماية

- البوابات الثماني مطلوبة: `backend` + `frontend` + `guards` + `audit` +
  `backend-postgres` + `migration-guard` + `CodeQL` + `bundle/SLO` — الفرع ينتظر
  اكتمالها قبل الدمج.
- `CodeQL` لا يعمل على `push` فقط؛ يحتاج حدث `synchronize` (تحديث `PR` بعد الدفع)
  ليعيد التحليل على رأس الفرع.

## 10. التحقق النهائي

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0 تحذير / 0 خطأ |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` | **2493/2493** · 0 فشل |
| `Api` غير تسلسلي | **1551/1551** · 0 فشل |
| `Api` تسلسلي | **5/5** · 0 فشل |
| `npx oxlint src` | نظيف |
| `npx tsc -b` | 0 |
| `npx vitest run` | **1808/1808** |
| `npm run build` | ناجح |
| `postgres-suite-guard` | نظيف — 43 مجموعة |
| `migration-watchlist/safety/subhead/determinism/secrets` | نظيفة |
| `test-report --self-test` / `npm-audit-guard --self-test` | ناجحان |
