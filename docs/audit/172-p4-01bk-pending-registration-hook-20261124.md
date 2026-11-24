# `P4-01bk` — خطاف تنظيف تنبيه إعادة القيد إلى Command — 2026-11-24

> **قرار النطاق:** `DeleteByPendingRegistrationAsync` من `ExecutionCircuitService` إلى
> أمر. وبقيت `SyncPendingAlertsAfterTransferAsync` في الخدمة لأنها تُستدعى داخل معاملة
> نقل المستند (ذرّية مع تغيير الملكية) ولا يجوز أن تفتح معاملة أخرى.

## قبل/بعد

| قبل | بعد |
|---|---|
| `DeleteByPendingRegistrationAsync` في `PendingRegistrations.cs` | `DeletePendingRegistrationAlertsCommand` في `UseCases/ExecutionCircuits` |
| ضمن `IExecutionCircuitService` | أُزيلت |

**حدود المعاملة:** معاملة واحدة لتصفية التنبيهات/الحفظ/إبطال الكاش، كما في الأصل.

## السلوك المحفوظ حرفيًا

1. `ExecutionCircuitRules.CleanupPendingAlertsAsync` هي التنفيذ الوحيد.
2. الحفظ فقط عند وجود إزالة، وإبطال كاش الدوائر.

## المراجعة النقدية

| # | الوجد | الدليل | الإصلاح | الحالة |
|---|---|---|---|---|
| 1 | إزالة الطريقة من العقد | `IExecutionCircuitService`؛ `git grep` | أُزيلت (لا مستدعي إنتاجي) | ✅ |
| 2 | الإبقاء على مزامنة النقل | `TransferDocumentCommand`/`TransferAllDocumentsCommand` | بقيت في الخدمة (معاملة المستدعي) | ✅ |
| 3 | حدود المعاملة | الأصل | مطابقة | ✅ |
| 4 | `DI` | `DependencyInjection.cs` | 89 معالجًا / 0 غير مسجَّل | ✅ |

## التحقق

| البوابة | النتيجة |
|---|---|
| `build -warnaserror` / `format` | 0/0 ونظيف |
| `Architecture` / `Application` / `Api` | 29/29 · 758/1659/0 · 901/615/0 و0/5/0 |
| الحرّاس الستة | نظيفة |

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
