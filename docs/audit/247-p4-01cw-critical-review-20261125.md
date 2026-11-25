# `P4-01cw` — المراجعة النقدية: تكافؤ/عقود/إغلاق دين القراءة — 2026-11-25

> **نطاق المراجعة:** بعد نقل القراءات الإحدى عشرة من `IHeadAlertService` (6) و
> `IStrikeOffReminderService` (3) و`IAuditLogService` (2)، وقبل الدمج. المنهج:
> مقارنة كل دالة أصلية بمعالجها بعد التطبيع، وعدّ سطح الواجهات والخدمات والتسجيلات،
> وفحص نصي لعدم الكتابة، ومسح شامل لبقايا المستدعين/الاستيرادات/الحقول، ومطابقة كل
> رقم في الوثائق بالريبو.

## أسئلة المراجعة والنتائج

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل نُقلت كل أعضاء القراءة المعلنة؟ | عدّ `ADR-0010` §2.4 قبل/بعد | نعم — **6/6** تنبيهات + **3/3** تذكيرات شطب + **2/2** تدقيق = **11/11** |
| 2 | هل بقي أي عضو قراءة على واجهة خدمة؟ | مسح `public interface I*Service` | **0**؛ `IHeadAlertService` بقي بـ4 أعضاء كتابة نظامية فقط |
| 3 | هل كل عضو منقول قراءة صرفة؟ | قراءة الأجسام والمستدعين | نعم — لا `SaveChanges`/`AddAsync`/`Remove`/معاملة/تدقيق في أي معالج |
| 4 | هل تكافئ كل دالة معالجها دلاليًا؟ | مقارنة الجمل الفعّالة بعد التطبيع | نعم — **77/77** جملة، **0** فرق دلالي؛ 38 سطر تمرير ميكانيكي فقط |
| 5 | هل تكافئ المساعدات نسخها الأصلية؟ | مقارنة `ToPagedResult`/`RequireBranch`/`CountUserScopeAsync`/`ToDetailDto`/`NormalizePaging` | نعم — **5/5** حرفية (تغيّر `private`→`internal` ومعامل المستودع فقط) |
| 6 | هل تغيّرت نصوص الرفض أو رموز الحالة؟ | تتبّع النصوص العربية اتجاهين | لا — **6/6** نص، `missing = 0`، `added = 0`؛ 403/400 كما هي |
| 7 | هل تغيّر سطح `IHeadAlertService` بأكثر من الحذف المقصود؟ | عدّ قبل/بعد | لا — **10 → 4**، والأربعة الباقية هي الكتابة النظامية بأسمائها وتوقيعاتها |
| 8 | هل بقيت واجهة/خدمة قراءة ميتة أو مسجَّلة؟ | بحث `IStrikeOffReminderService|IAuditLogService|AuditLogService|StrikeOffReminderService` في `backend/` | **0** في `src`؛ الذكر الوحيد في اختبار التوصيل (نفي) |
| 9 | هل صار أي حقل أو معامل منشئ ميتًا؟ | عدّ حقول `HeadAlertService` واستخدامها في partials | حُذف حقلان ومعاملان: `_branches` (**كان ميتًا قبل النقل**) و`_delegations` (مات بعد النقل)؛ المنشئ **9 → 7**، و**0** حقل ميت |
| 10 | هل بقيت استيرادات/مساعدات ميتة في الخدمات المتبقية؟ | مسح `using` و`private static` بعد الحذف | لا استخدام لـ`IRepository<Branch>`/`HeadAlertSummaryPage`/`HeadAlertTitle` داخل `HeadAlertService` بعد النقل |
| 11 | هل كل معالج مسجَّل في `DI` مرة واحدة؟ | عدّ آلي للتسجيلات مقابل الأصناف وفحص التكرار | **229/229/0** (**111** استعلامًا + **118** أمرًا)؛ 111 نوع استعلام مميز بلا تكرار |
| 12 | هل بقيت قراءات تُستدعى عبر الخدمة في `src`؟ | بحث أسماء الأعضاء الستة/الثلاثة/الاثنين | **0**؛ المتحكمات تستدعي المعالجات مباشرة |
| 13 | هل تغيّر مسار/صلاحية/شكل استجابة أي نقطة؟ | مقارنة المتحكمات الثلاثة قبل/بعد | لا — نفس المسارات و`[Authorize]` وحرّاس القدرات/الجلسة و`Ok`/`NoContent`/`NotFound`/`Forbid`/`ApiProblem` وأشكال الأجسام |
| 14 | هل انزاح ترتيب الحرّاس أو النطاق؟ | مقارنة كتل List/Sent/ByDelegation/Details/Recipients/Unread/Audit/Changes/StrikeOff | لا — الحرّاس قبل الاستعلام بنفس الترتيب، ونفس `User.GetBranchId()`/`GetUserId()`/`GetSectionId()` و`IsAdmin`/`IsHead`/`IsSubHead` |
| 15 | هل تغيّر سلوك ترقيم/ترتيب/مؤشر أي قائمة؟ | مقارنة `Page`/`PerPage`/`Cursor`/`hasMore`/`OrderBy` | لا — نفس التطبيع والترتيب والـkeyset والـ`page=1` مع المؤشر |
| 16 | هل بقيت قراءة تكتب تدقيقًا؟ | مسح مسارات القراءة مقابل `IAuditLogger` | لا — لا معالج قراءة يكتب تدقيقًا؛ استثناءات `IPortalService` الموثّقة لم تُمسّ |
| 17 | هل أُضيفت هجرة أو تغيّر النموذج؟ | `has-pending-model-changes` بسلسلة وهمية | لا — نظيف، المعلّق يبقى **6** |
| 18 | هل غُطّي النقل باختبارات؟ | عدّ مواضع النداء والاختبارات | نعم — جسران وحزمتان، **65** موضع نداء، **17** موضع إنشاء محدَّث، واختبار توصيل جديد للأحد عشر |
| 19 | هل تحوّل أي اختبار فعلي إلى تخطٍّ جديد؟ | أرقام Application/Api قبل/بعد | لا — Application 771 → **772** (اختبار التوصيل فقط)؛ Api كما هي 901/615 + 0/5 |
| 20 | هل كل رقم في الوثائق مطابق للريبو؟ | جرد الأعداد المذكورة | نعم — 11/6/3/2، 229/111/118، 77/77، 6/6، 65، 17، 53→55، 3→8 |
| 21 | هل المتحكمات رقيقة فعلًا؟ | فحص كل `Controllers/*.cs` بأنماط المعاملة/الحفظ | **0** مطابقة؛ لا `DbContext` ولا `ITransactionRunner` |
| 22 | هل بقي partial فارغ بعد حذف ملف القراءات؟ | فحص `partial class` بعد تجريد التعليقات | **0** جسم فارغ |
| 23 | هل الواجهة المحذوفة بلا كاتب/احتفاظ فعلاً؟ | قراءة `AuditLogService` كاملة | نعم — عضوَا قراءة فقط؛ لا كاتب/احتفاظ يُنقل، فحُذف الملفان معًا |
| 24 | هل خدمة الشطب بلا كاتب؟ | قراءة `StrikeOffReminderService` كاملة | نعم — ثلاثة أعضاء قراءة فقط؛ الإلغاء بقي في `DismissStrikeOffReminderCommand` |

## تدقيق التكافؤ حقل بحقل (عيّنة المسار الكامل)

- `GET /api/alerts/{id}`: `[Authorize]` ← `User.GetUserId()/GetRoleEnum()/GetBranchId()` ←
  `HeadAlertActor` ← `GetHeadAlertDetailsQuery(Id, Actor)` ← `GetDetailRowAsync` ←
  فحص `IsRecipient`/`IsCreator`/`IsManagerOrAdmin` ← `HeadAlertReadHelpers.ToDetailDto`
  (20 حقلًا بنفس الترتيب، و`canViewRecipients = isOversight || isCreator`) ← `Ok` أو
  `ApiProblem 404` بالرسالة نفسها.
- `GET /api/documents/strike-off-reminders/list`: `CanViewStrikeOffReminders` ←
  `GetStrikeOffReminderListQuery(page, perPage, branchId, userId, ownerSectionId: null, cursor)`
  ← تطبيع الترقيم ← `CutoffUtc` ← `SearchStrikeOffReminderIdsAsync` (keyset
  `ReminderLastActivity`) ← `GetListRowsByIdsAsync` ← `DocumentListItemDto.FromRow` ←
  `NextCursor` ← `Ok(PagedResult)` كما هو.
- `GET /api/audit-logs`: `[Authorize(Roles=...)]` ← حرّاس `IsHead`/`IsSubHead` والنطاق
  ← `SearchAuditLogsQuery(...)` ← `Math.Max(1,page)`/`Math.Clamp(perPage,1,100)` ←
  `SearchAsync` ← `AuditLogDto` (7 حقول) ← `Ok` كما هو.

## تدقيق عدّ المعالجات وعدم التكرار

- 111 تسجيل `IQueryHandler<,>` + 118 تسجيل `ICommandHandler<,>` = **229**، مقابل **229**
  صنف معالج في `Application` → 0 غير مسجَّل و0 مكرر.
- حُذف تسجيلا خدمة (`IAuditLogService`/`IStrikeOffReminderService`)؛ وبقي
  `HeadAlertService` مسجَّلًا مع `IHeadAlertService`/`IHeadAlertManualWriteRules` لسطح الكتابة.
- لم يُسجَّل أي معالج في طبقة `Api`؛ ولا استعلام يطبّق `ITransactionalRequest`/`IAuditedRequest`.
