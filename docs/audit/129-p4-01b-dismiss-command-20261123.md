# `P4-01b` — أول use-case يملك معاملته — 2026-11-23

> **النطاق:** نقل `DismissAsync` من `StrikeOffReminderService` إلى
> `DismissStrikeOffReminderCommandHandler`، مع نقل **ملكية المعاملة** إلى المعالج
> عبر `ITransactionalRequest`. **بلا تغيير سلوكي** (نفس الشروط، نفس التدقيق، نفس النتيجة).

## 1. ما تغيّر

| قبل | بعد |
|---|---|
| `StrikeOffReminderService.DismissAsync` يفتح `_tx.RunAsync` ويكتب ويُدقّق | `DismissStrikeOffReminderCommand` + معالج يملك الكتابة والتدقيق، و`TransactionBehavior` يفتح المعاملة |
| الخدمة تحمل `IUnitOfWork`/`ITransactionRunner`/`IAuditLogger` للكتابة | الخدمة قراءة فقط: أُزيلت الحقول والوسائط الثلاثة |
| `IStrikeOffReminderService.DismissAsync` ضمن العقد | أُزيل من العقد (لا مستدعي آخر) |
| المتحكم يستدعي الخدمة | يستدعي `ICommandHandler<DismissStrikeOffReminderCommand, bool>` |

- **السلوك محفوظ حرفيًا:** المالك فقط (`CreatedById == OwnerId`)، `false` عند الغياب/عدم
  الملكية (404)، `DateTime.UtcNow`، رسالة التدقيق نفسها، والتدقيق **لا يُكتب عند الفشل**
  (المعالج يُدقّق بعد النجاح فقط داخل المعاملة).
- `TransactionBehavior` أصبح **مُستخدَمًا فعليًا** في الإنتاج لأول مرة (أمر موسوم).

## 2. المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | كسر بناء اختبارات تُنشئ `DocumentsController` | وسيط بناء جديد → 3 × `CS7036` | أُضيف `null!` في الاختبارات الثلاثة |
| 2 | خطر تغيير دلالة التدقيق عند النتيجة `false` | `AuditBehavior` يُدقّق بعد النجاح عمومًا | لم يُوسم الأمر بـ`IAuditedRequest`؛ التدقيق داخل نواة المعالج بعد النجاح فقط |
| 3 | خطر بقاء حقول/وسائط غير مستخدمة في الخدمة | إزالة الكتابة | حُذفت `_uow`/`_tx`/`_audit` من الخدمة والمنشئ |
| 4 | خطر معاملة متداخلة | الخدمة لم تعد تفتح معاملة | `TransactionBehavior` يفتحها مرة واحدة |
| 5 | بناء/تنسيق | `build -warnaserror` و`format` | 0/0 ونظيف |
| 6 | تغطية السلوك | اختبارات `StrikeOffRemindersIntegrationTests` (مالك/غير مالك/404) | تمر كما هي (Api 901) |

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` | **757 نجح / 1655 متجاوز / 0 فشل** |
| `Api` غير التسلسلي | **901 نجح / 615 متجاوز / 0 فشل** |
| `Api` التسلسلي | **0 نجح / 5 متجاوز / 0 فشل** |
| الحرّاس الستة | نظيفة |

## 4. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
