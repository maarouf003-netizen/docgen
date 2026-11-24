# `P4-01bd` — انتقالات حالة المستند إلى Commands — 2026-11-24

> **قرار النطاق:** نُقلت نقاط الكتابة الست لانتقالات الحالة من `DocumentStatusService`
> إلى أوامر تملك معاملاتها: `UpdateStatus` و`RevertStatus` و`ReturnFromReferredToStart`
> و`ConsiderExecutedByDelegation` و`UpdateExecutedStatus` (بالتحميلين) و`RestoreStruckOff`
> (بالتحميلين). وبقي في الخدمة كاتبا الوقوعات/التجديد اللذان يُستدعيان داخل معاملة
> مستدعٍ خارجي (`UpdateDocumentCommand`/`DocumentService`).

## قبل/بعد

| قبل | بعد |
|---|---|
| ست نقاط كتابة داخل `DocumentStatusService` (أربعة ملفات جزئية) | ستة أوامر ومعالجات في `UseCases/Documents/DocumentStatusCommands.cs` |
| ضمن عقد `IDocumentStatusService` (8 توقيعات) | أُزيلت؛ بقي `ApplyRenewalAsync` و`AddStruckOffOccurrenceAsync` فقط |
| المساعدات الحقلية/التجديد/توريث المناب/أرشفة التنبيهات خاصة في الخدمة | `DocumentStatusRules` (مصدر واحد بمعاملات صريحة) |
| الخدمة 1312 سطرًا في خمسة ملفات | ملف واحد (~50 سطرًا) + قواعد مشتركة |
| `DocumentsController` ينادي الخدمة | ينادي المعالجات الستة المسجَّلة |

**حدود المعاملة:** كل أمر يحتفظ بمعاملة `_tx.RunAsync` واحدة كما في الأصل، مع
`WithConcurrencyGuardAsync`/`WithNumberGuardAsync` في مواضعها. لا وسم
`ITransactionalRequest` (بلا تداخل مع `TransactionBehavior`). مساعدات التجديد/الشطب
تنضم لمعاملة المستدعي ولا تفتح معاملة.

## السلوك المحفوظ حرفيًا

1. حرّاس التسجيل المعلّق/الصفة/المناب/الشطب/الإحالة وترتيبها قبل أي تعديل.
2. آلة الحالات `IsAllowedStatusChange`/`CanRevert`، ولقطات `DocumentChangeTracker`
   قبل التغيير، وحقول الوقعات وتفاصيلها.
3. نصوص التدقيق (`status`/`executed-status`/`restore-struck-off`) ورسائل الرفض.
4. توريث/استرداد المناب وأرشفة `CleanupPendingCompletionAlertsAsync` داخل معاملة
   الحالة، وبترتيب: توريث ← أرشفة ← تنبيهات المرآة.
5. التجديد في `ReturnFromReferredToStart` يبقى قبل المعاملة كما في الأصل.

## المراجعة النقدية

| # | الوجد | الدليل | الإصلاح | الحالة |
|---|---|---|---|---|
| 1 | إزالة 8 توقيعات من العقد | `IDocumentStatusService.cs`؛ `git grep` عبر `backend/` | أُزيلت، ولا مستدعي خدمة متبقٍّ | ✅ |
| 2 | حرفية النصوص (اتجاهان) | مقارنة آلية مع الوالد `fc149245` | 73/73 حرفيًا و0 انحراف/0 إضافة | ✅ |
| 3 | حدود المعاملة | الأصل في الملفات الأربعة | مطابقة: معاملة واحدة لكل أمر، ومساعدات بلا معاملة | ✅ |
| 4 | الكود الميت | `Helpers`/`Transitions`/`ReferralExecuted`/`RenewalStruckOff` | حُذفت بعد النقل؛ الخدمة ملف واحد بلا `partial` | ✅ |
| 5 | مواضع الاختبار | 4 ملفات (~190 استدعاءً) | امتدادات بالتوقيع القديم + `DocumentStatusHandlers` في المصنع | ✅ |
| 6 | تأكيدات نصية | لم تكن هناك | `Equal` على تدقيق «مشطوب» و«التراجع» | ✅ |
| 7 | بناء المتحكم يدويًا | `DocumentAccessProjectionTests`/`TransferBranchGuardTests`/`DocumentGenerationDisabledTests` | عُدّلت مواضع البناء (فقد معامل + 6 معالجات) | ✅ |
| 8 | اختبار `404` | `DocumentsStatusNotFoundContractTests` | يستبدل المعالجات الستة بردّ `false` بدل خدمة وهمية | ✅ |
| 9 | تسجيل `DI` | `DependencyInjection.cs` | 75 معالجًا / 0 غير مسجَّل (بعد دمج `01bc`) | ✅ |
| 10 | الهجرات | لا تغيير مخطط | لا هجرة جديدة؛ المعلّق يبقى 6 | ✅ |

## التحقق

| البوابة | النتيجة |
|---|---|
| `build -warnaserror` / `format` | 0/0 ونظيف |
| مراجعة الحروف العربية (اتجاهان) | **0 انحراف / 0 إضافة** |
| `DI` | 75 / 0 |
| `Architecture` | 29/29 |
| `Application` | 758 / 1659 / 0 |
| `Api` غير التسلسلي / التسلسلي | 901 / 615 / 0 · 0 / 5 / 0 |
| الحرّاس الستة | نظيفة |

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.

> لم تُحرَّر اللقطات `128`–`164`.
