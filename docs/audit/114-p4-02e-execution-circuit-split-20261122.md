# `P4-02e` — تفكيك `ExecutionCircuitService` — 2026-11-22

> **النطاق:** تفكيك `ExecutionCircuitService` (1296 سطرًا) إلى `partial` بحسب المسؤولية،
> **بلا تغيير سلوكي ولا توقيع عام**. أقصى ملف بعد التفكيك **317** سطرًا.
> **واجهة `IExecutionCircuitService` بقيت في الملف الرئيسي حرفيًا بلا نقل.**

## 1. التقسيم

| الملف | المحتوى | الأسطر |
|---|---|---:|
| `ExecutionCircuitService.cs` | usings + `IExecutionCircuitService` + التعريف + الحقول + المنشئ + القراءات المرجعية المخزَّنة | **148** |
| `…Queries.cs` | `ListMine`/`ListFiles`/`ListForLawyer`/`ListForDelegation`/`CircuitStats` + `ToDtoAsync` | **139** |
| `…Crud.cs` | `Create`/`Rename`/`SetActive`/`Delete` (مع التطهير وفك الإنابات) | **280** |
| `…Transfer.cs` | نقل ملكية الدائرة والتنبيه البديل والخلافة | **124** |
| `…Referral.cs` | `ReferFiles` + نواتها (نقل الملفات والوقائع والتنبيه المدمج) | **317** |
| `…PendingRegistrations.cs` | إعادة القيد + تنظيف التنبيهات + مزامنة تنبيهات النقل | **239** |
| `…Helpers.cs` | حراس الفرع/الملكية/الاسم + رسالة التكرار + تدقيق الحقول | **121** |

- كل الملفات `public sealed partial class ExecutionCircuitService`، والملف الرئيسي وحده
  يحمل تعريف الواجهة ووثيقة النوع.
- لم يُنقل أي حقل ثابت/ساكن خارج الملف الرئيسي، ولا تغيّر ترتيب التهيئة.

## 2. المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | خطر فقدان/تكرار عضو أثناء النقل | مقارنة انعكاسية لأسماء الأعضاء قبل/بعد: **55 = 55** بلا فرق | لا فقدان |
| 2 | خطر تغيّر نص/سلوك أثناء القص | مقارنة مجموع الأسطر الدلالية: الفرق الوحيد `public sealed class` → `public sealed partial class` | متوقع ومقصود |
| 3 | حد `P4-02` ≤600 سطر | أكبر ملف **317** (`…Referral.cs`) | متحقق |
| 4 | نسيان `partial` في الملف الرئيسي | كان سيسبب `CS0260` | صُحّح قبل البناء |
| 5 | صيغة `dotnet format` | سطر فارغ ناقص بعد `namespace` في الملفات الجديدة | أُصلح وتأكد `--verify-no-changes` |
| 6 | الاعتماد على ترتيب الملفات | لا `using static` ولا ترتيب تهيئة؛ الترتيب أبجدي فقط | لا أثر |
| 7 | بقايا أسماء/مسارات قديمة | لا مراجع كود/اختبارات/سكربتات لاسم الملف القديم | لا بقايا |

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` (بلا `PostgreSQL`) | **753 نجح / 1655 متجاوز (Postgres-gated) / 0 فشل** |
| `Api` غير التسلسلي | **901 نجح / 615 متجاوز / 0 فشل** |
| `Api` التسلسلي | **0 نجح / 5 متجاوز (Postgres-gated) / 0 فشل** |
| أكبر ملف بعد التفكيك | **317** سطرًا (من 600) |

## 4. حالة `P4-02`

اكتمل نطاق الخدمات الخمس المعلن في الخريطة (`DocumentRepository`,
`DocumentStatusService`, `DocumentDelegationService`, `DocumentAppealService`,
`ExecutionCircuitService`). **`DoD` العام «لا ملف >600 سطر في Application/Infrastructure»
لم يتحقق بعد** لملفات خارج النطاق المعلن (مثل `HeadAlertService` 1137،
`DocumentService` 1110، `CorrespondenceService` 1049) وتحتاج قرارًا بتوسيع النطاق.

## 5. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
