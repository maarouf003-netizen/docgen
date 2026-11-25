# `P4-01cl` — نقل قراءات كتب المطالعة إلى Query Handlers — 2026-11-25

> **النطاق:** المرحلة الثالثة عشرة في سلسلة نقل ملكية `use-case`: تحويل **كل** أسطح
> القراءة الستة على `IReviewLetterService` — قائمة الكتب بحسب الدور، وكتاب برسائله،
> وعدّاد بانتظار الرد، وعدّاد الردود غير المطّلع عليها، وكتب ملف محدد، وأسماء فروع
> الإدارة — إلى استعلامات `IQuery<TResponse>` ومعالجات `QueryHandlerBase` تحت
> `backend/src/DocGenerator.Application/UseCases/ReviewLetters/`، **قراءة صرفة بلا كتابة
> ولا معاملة ولا تدقيق جديد**. الفرع `refactor/p4-01cl-review-letter-reads` من `master`
> (`59ff704b`)، بحفاظ حرفي على السلوك والنطاق والرسائل ورموز الحالة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `SearchReviewLettersQuery(ActorUserId, Role, ActorBranchId, Q, Page, PerPage, AdministrativeBranch, ActorSectionId)` | `SearchReviewLettersQueryHandler` | `IReviewLetterRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<ReviewLetterListItemDto>` |
| `GetReviewLetterQuery(Id, ActorUserId, Role, ActorBranchId, ActorSectionId)` | `GetReviewLetterQueryHandler` | `IReviewLetterRepository` + `IDocumentRepository` + `IRepository<ExecutionCircuit>` + `IAppealRepository` + `IDelegationRepository` + `TimeProvider` + `TimeZoneInfo` | `ReviewLetterDto` |
| `CountPendingReviewLettersQuery(BranchId, OwnerSectionId)` | `CountPendingReviewLettersQueryHandler` | `IReviewLetterRepository` | `int` |
| `CountUnseenReviewRepliesQuery(ActorUserId)` | `CountUnseenReviewRepliesQueryHandler` | `IReviewLetterRepository` | `int` |
| `ListReviewLettersByDocumentQuery(DocumentId, ActorUserId, Role, ActorBranchId, ActorSectionId)` | `ListReviewLettersByDocumentQueryHandler` | `IReviewLetterRepository` + `IDocumentRepository` + `IRepository<ExecutionCircuit>` + `IAppealRepository` + `IDelegationRepository` + `TimeProvider` + `TimeZoneInfo` | `List<ReviewLetterListItemDto>` |
| `GetReviewLetterAdministrativeBranchesQuery()` | `GetReviewLetterAdministrativeBranchesQueryHandler` | `IReviewLetterRepository` | `List<string>` |

| البند | التفصيل |
|---|---|
| العقود | حُذفت الأعضاء الستة من `IReviewLetterService`؛ صارت الواجهة **فارغة فحُذفت نهائيًا** (6 → 0) مع تسجيلها في `DI`. لم تُضف أعضاء جديدة إلى `IReviewLetterReplyWriteRules`/`IReviewLetterCreateWriteRules` |
| الخدمة | حُذف `ReviewLetterService.Queries.cs` (كان يحوي الأسطح الستة فقط). الخدمة الآن تنفيذ وحيد لعقدَي مساعدات الكتابة، وتقلّصت حقولها **15 → 12** ومنشئها **15 → 12**: حُذف `_uow`/`_tx`/`_audit` الميتة أصلًا (تعريف وإسناد بلا أي مستخدم بعد نقل الكتابات في `P4-01bl`/`P4-01bm`) |
| المساعدات | `CanViewAsync` (كانت تُستدعى من `GetByIdAsync` وحده) و`InFileScopeAsync` (من `ListByDocumentAsync` وحده) نُقلتا حرفيًا إلى معالج الاستعلام الوحيد لكل منهما؛ `OwnerSectionOfAsync`/`FileOwnerSectionAsync`/`FollowsDocumentAsync` بقيت تعريفًا واحدًا على الخدمة بصيغة `internal static` تستقبل المستودعات صراحةً، وتفوّض إليها عقود الكتابة؛ و`ToDto`/`ToListItem`/`FileContextOf` صارت `internal static` تستقبل `TimeProvider`/`TimeZoneInfo` صراحةً، ويستدعيها معالجان اثنان وعقد تسطير الكتاب |
| عدم الكتابة | لا `SaveChangesAsync`/`AddAsync`/`Add(`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/`IAuditedRequest`/`IAuditLogger`/`LogAsync`/`RunAsync` في ملفات الاستعلام الستة (فحص نصي شامل: 0 مطابقة) |
| `DI` | حُذف تسجيل `IReviewLetterService` وأُضيفت ستة تسجيلات `IQueryHandler<Query, Response>`؛ الإجمالي **137 → 143** تسجيلًا مقابل **143** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكم | `ReviewLettersController` يحقن المعالجات الستة بدل الخدمة (منشئ **6 → 11** معاملًا)، ويستدعيها بنفس المسارات (`api/review-letters` وما تحته) ونفس `[Authorize]` وحرّاس `IsSubHead && SectionId is null` و`CanSeeAdministrativeBranch` و`CanCreateReviewLetters`/`CanReplyReviewLetters` ونفس `Ok(...)`/`Forbid()`/`ApiProblem` ورموز الحالة وأشكال الأجسام (`{ count }`، `{ administrativeBranches }`، `PagedResult` كما هو) |
| الاختبارات | جسر جديد `ReviewLetterReadQueryExtensions` (ستة امتدادات بنفس توقيعات الخدمة القديمة) وتحويل **40 موضع نداء** (30 في `ReviewLetterServiceTests`، و10 في `SubHeadReviewCorrespondenceTests`) بلا تغيير أي توقّع؛ وتحديث موضعَي البناء المباشر (`new ReviewLetterService(...)`) إلى المنشئ الجديد (12 معاملًا) |
| تنظيف ميت | حُذف حقل `_service` الميت من `ReviewLetterServiceTests` وحقل `_letters` الميت من `SubHeadReviewCorrespondenceTests` (بعد تحويل كل نداءاتهما إلى المعالجات، صارا مستخدمَين داخل المنشئ فقط فتحوّلا متغيرين محليين)، وحُذفت استيرادات ميتة: `Common.Security` من ثلاثة ملفات خدمة، و`Common.Interfaces` من `Compose`/`Formatting`، و`Common`/`Security`/`DTOs` من `Access` |

> **قرار موثّق:** لم يُحقن أي عقد كتابة (`IReviewLetterCreateWriteRules`/
> `IReviewLetterReplyWriteRules`) داخل معالج قراءة، ولم يُكرَّر منطق حرّاس الوصول؛
> بدلًا من ذلك رُقّيت المساعدات المشتركة إلى `internal static` تستقبل مستودعاتها
> صراحةً (مصدر واحد)، فبقي كل معالج قراءة معتمدًا على مستودعاته فقط.
>
> **ملاحظة صدق:** `ReviewLetterServiceFactory` المذكور في التكليف **غير موجود** في
> المستودع؛ الاختباران يبنيان `ReviewLetterService` مباشرةً، وقد حُدّث موضعا البناء.

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإعادة تسمية الحقول إلى
> بارامترات (`_letters`/`_documents`/`_circuits`/`_appeals`/`_delegations`)، وإسقاط
> تأهيل `ReviewLetterService.`، ثم مقارنة تدفّق الرموز (tokens) لا مجرد الأسطر.

| الدالة | الأسطر الفعّالة (قبل/بعد) | فرق الرموز | الاستثناءات الموثّقة |
|---|---|---|---|
| `SearchAsync` → معالج البحث | 33 / 38 | 0 | سطرا ربط `var page`/`var perPage` بدل إعادة إسناد المعاملين؛ تعليق «على مستوى الخدمة» صار «على مستوى الاستعلام»؛ تمرير `clock`/`timeZone` إلى `ToListItem` |
| `GetByIdAsync` → معالج الكتاب | 5 / 6 | 0 | تمرير `clock`/`timeZone` إلى `ToDto` (الطلب موزّع على سطرين) |
| `CountPendingForHeadAsync` | 1 / 1 | 0 | — |
| `CountUnseenRepliesForLawyerAsync` | 1 / 1 | 0 | — |
| `GetAdministrativeBranchesAsync` | 1 / 1 | 0 | — |
| `ListByDocumentAsync` → معالج كتب الملف | 14 / 17 | 0 | تمرير `appeals`/`delegations` إلى `FollowsDocumentAsync`، و`circuits` إلى `FileOwnerSectionAsync`، و`clock`/`timeZone` إلى `ToListItem` |
| `CanViewAsync` (منقولة) | 19 / 22 | 0 | تمرير المستودعات الأربعة إلى المساعدتين المشتركتين |
| `InFileScopeAsync` (منقولة) | 10 / 10 | 0 | تمرير `circuits` إلى `FileOwnerSectionAsync` |
| **الإجمالي المنقول** | **84 / 96** | **0** | لا فرق منطقي |

| المساعد (بقي على الخدمة) | قبل/بعد | فرق |
|---|---|---|
| `OwnerSectionOfAsync` | 7 / 7 | 0 (مستودعان صراحةً) |
| `FileOwnerSectionAsync` | 5 / 5 | 0 (مستودع صراحةً) |
| `FollowsDocumentAsync` | 8 / 8 | 0 (مستودعان صراحةً) |
| `ToDto` | 17 / 17 | 0 (ساعة/منطقة صراحةً) |
| `ToListItem` | 23 / 23 | 0 (ساعة/منطقة صراحةً) |
| `FileContextOf` | 14 / 14 | 0 (ساعة/منطقة صراحةً) |
| `ToMessageDto` | 9 / 9 | 0 |
| `GenerateUniqueNumberAsync` | 10 / 10 | 0 |
| `NormalizePrefix` | 7 / 7 | 0 |
| `ResolveBranchCodeAsync` | 3 / 3 | 0 |

- **مقارنة النصوص الحرفية (اتجاهان):** المقطع المنقول يحوي **7** نصوص حرفية مميزة
  (رسائل `KeyNotFoundException`/`UnauthorizedAccessException` ورسالة «الفرع/الشعبة»)،
  والنتيجة `missing = 0` و`added = 0`.
- **النطاق/الصلاحيات:** شروط `CanViewAsync` (محامي/متابع، رئيس قسم بلا دائرة، رئيس
  شعبة بدائرة، مدير/مشرف) و`InFileScopeAsync` (حارس `null` الصريح للفرع الإرثي)
  بقيت بنفس المواضع والترتيب؛ وحرّاس المتحكم لم يتغيّر أي سطر منها.
- **بلا كتابة:** الاستعلامات الستة تقرأ فقط عبر
  `SearchForLawyerAsync`/`SearchForScopeAsync`/`SearchAllAsync`/
  `GetByIdWithDetailsAsync`/`CountPendingForScopeAsync`/
  `CountUnseenReplyLettersForLawyerAsync`/`ListByDocumentAsync`/
  `GetAdministrativeBranchesAsync` ومستودعات القراءة المساعدة؛ لا حفظ ولا معاملة ولا تدقيق.
- **الفاصل الحتمي/الترتيب:** `Math.Max(1, page)` و`Math.Clamp(perPage, 1, 100)` بنفس
  الترتيب قبل حجب المدير، و`currentYear` يُحسب عبر `ServerClock.CurrentYear` كما كان.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 (171 ملفًا) + `oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api`
بلا فرق (`git diff --exit-code -- src/api/generated.ts` = 0) · الحرّاس الستة نظيفة ·
`dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة · `has-pending-model-changes` نظيف ·
`DI` 143/143/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات قراءات المطالعات المعدَّلة) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
