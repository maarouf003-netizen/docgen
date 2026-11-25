# `P4-01cw` — المراجعة العدائية والملحق التصحيحي — 2026-11-25

> **ملحق مستقل** بعد المراجعة النقدية `247`: يبحث في البقايا، والنسخ القديمة،
> والانحرافات الصامتة، ويصحّح ما وُجد. لا يُحرَّر أي وثيقة سابقة.

## 1. البقايا والنسخ القديمة

| الفحص | النتيجة |
|---|---|
| مستدعو الخدمات المحذوفة (`IStrikeOffReminderService`/`IAuditLogService`/`StrikeOffReminderService`/`AuditLogService`) في `src` | **0** |
| مستدعو أعضاء القراءة الستة على `IHeadAlertService` في `src` | **0** — `AlertsController` يستدعي المعالجات |
| مستدعوها في الاختبارات | **65** موضعًا عبر جسرين فقط، لا استدعاء خدمة |
| بدائل اختبارية قديمة | `FakeAuditLogService` استُبدل بـ`FakeSearchAuditLogsHandler`؛ حُذفت من `CreateFailureAlertService` ستة تفويضات قراءة صارت خارج العقد |
| مصانع/منشئات `HeadAlertService` | **17** موضعًا محدَّثًا (إسقاط معاملَي `branches`/`delegations`) |
| `partial` فارغة | **0**؛ `HeadAlertService.Reads.cs` حُذف كاملًا لا أُفرغ |
| ملفات فارغة أو واجهات معلّقة | **0**؛ الواجهتان المحذوفتان غير مسجَّلتين وغير مذكورتين في `DI` |
| `using` ميتة في المتحكمات المعدّلة | لا — أُضيف `UseCases.Audit`/`UseCases.Reminders`، وبقي `Application.Services` حيث يلزم (`HeadAlertActor`، `IWordDocumentGenerator`…) |

## 2. الانحرافات الصامتة (فحص عقدي)

| الخطر | الفحص | النتيجة |
|---|---|---|
| تغيّر شكل `{ count }` في عدّاد التنبيهات | قراءة `UnreadCount` قبل/بعد | لا — حُسب `count` محليًا ثم `Ok(new { count })`؛ نفس JSON |
| فقدان `OkSanitized` في بطاقة تذكيرات الشطب | مقارنة `GetStrikeOffReminders` | لا — ما زال `OkSanitized(result)`، والقائمة `Ok(result)` |
| اختلاف 404 Messages | مقارنة `AlertsController.NotFoundMessage` | لا — الثابت لم يُمسّ، ويُستخدم في Get/Recipients/MarkRead |
| تبديل 403/400 | مراجعة ترتيب الفحوص داخل المعالجات | لا — الدور/الفرع قبل `Validate`/`ParseOptional`، وقيمة نطاق غير معروفة `ArgumentOutOfRangeException` كما كانت |
| توسيع/تضييق النطاق | `SearchAuditLogsQuery` يستقبل `ScopeBranchId`/`ScopeSectionId` من نفس حرّاس المتحكم | لا تغيير |
| تدقيق غير مقصود في قراءة | مسح 13 ملف استعلام | **0** `IAuditLogger`/`IAuditedRequest` |
| ترتيب تغييرات الحقول | مقارنة `OrderBy(c => c.Id)` | لا تغيير |
| مؤشر keyset | `DocumentListCursorKinds.ReminderLastActivity` نفسه | لا تغيير |

## 3. التكرار والمساعدات

- لا نسخة ثانية من منطق `ToPagedResult`/`RequireBranch`/`CountUserScopeAsync`/`ToDetailDto`:
  نُقلت إلى `HeadAlertReadHelpers` ويستهلكها الاستعلامات الستة (المتحكم لا يعرفها).
- `NormalizePaging` مصدر واحد في `StrikeOffReminderReadHelpers` للاستعلامات الثلاثة.
- تدقيق الصفحة (`Math.Max`/`Math.Clamp`) بقي داخل كل معالج كما كان داخل كل دالة — لم
  يُنشأ مساعد جديد يتجاوز النطاق؛ الفرق الدلالي **0**.

## 4. تصحيحات وُجدت وأُنجزت

1. **حقل ميت سابق:** `HeadAlertService._branches` (ونظيره في المنشئ) لم يكن مستخدمًا في
   أي partial قبل هذه المرحلة — حُذف مع `_delegations` الذي صار ميتًا بعد النقل.
2. **تعليق عقدي قديم:** `DelegationParties` كان يشير إلى
   `IHeadAlertService.ListByDelegationAsync` المحذوف — حُدِّث إلى
   `ListHeadAlertsByDelegationQuery`.
3. **XML مشوّه:** سطر `<summary>    ///` في واجهة التنبيهات صُحّح، وتحديث ملخّص
   `HeadAlertService` ليعكس كونه سطح كتابة.
4. **اسم حقل اختباري مضلّل:** `SubHeadAuditScopeTests._service` صار `_reads` لأنه حزمة
   معالجات لا خدمة.
5. **`secrets-guard`:** فشل قراءة ملفات محذوفة لأن `git ls-files` يرى الفهرس قبل
   `git add`؛ حُلّ بتجهيز الحذف (staging) لا بتعديل خط الأساس — **لا تغيير في
   `scripts/secrets-baseline.json`**.
6. **تسمية الاستعلامات:** `GetStrikeOffRemindersQuery`/`GetStrikeOffReminderListQuery`
   لا تتصادم مع `GetRemindersQuery` (الإحصاءات)؛ و`ListHeadAlertsForActorQuery` يستخدم
   خاصية `Filter` لتفادي `query.Query` مع `HeadAlertListQuery` القائم.

## 5. الحكم

دين القراءة المعلن **مغلق بالكامل**: لا سطح قراءة صرف على أي خدمة، و**11/11** استعلامًا
مسجَّلًا مرة واحدة، وسطح كتابة التنبيهات الأربعة سليم، و**0** مستدعٍ قديم، و**0**
انحراف صلاحية/نطاق/استجابة، بلا هجرة وبلا تغيير عقد خارجي.
