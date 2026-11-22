# `P4-02g` — تفكيك `DocumentService` — 2026-11-23

> **النطاق:** تفكيك `DocumentService` (1110 سطرًا) إلى `partial` بحسب المسؤولية،
> **بلا تغيير سلوكي ولا توقيع عام**. أقصى ملف بعد التفكيك **465** سطرًا.
> **واجهة `IDocumentService` بقيت حرفيًا في الملف الرئيسي بلا نقل.**

## 1. التقسيم

| الملف | المحتوى | الأسطر |
|---|---|---:|
| `DocumentService.cs` | usings + `IDocumentService` + التعريف + الحقول + المنشئ + قراءات الملف + `CurrentYear` | **128** |
| `…Crud.cs` | الإنشاء (بالترقيم/عدم التكرار) + التعديل (المرآة/الدوائر) + الحذف/الاستعادة | **321** |
| `…Transfer.cs` | نقل الملف الفردي + عدّ ملفات المالك + النقل الجماعي | **157** |
| `…Apply.cs` | `ApplyRequest` + تطبيع الأطراف/الورثة/الجهات + تاريخ القيد | **465** |
| `…BaseNumbers.cs` | قائمة التدوير + تاريخ الأرقام + التصحيح/التراجع/الحفظ | **81** |

- كل الملفات `public sealed partial class DocumentService`، والملف الرئيسي وحده
  يحمل الواجهة ووثيقة النوع.
- لم يُنقل أي حقل ثابت/ساكن خارج الملف الرئيسي، ولا تغيّر ترتيب التهيئة.
- `UnconfiguredCircuitRepository` لم يُمسّ (تعريفه في `DocumentWriteGuard.cs` أصلًا).

## 2. المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **فقدان `namespace` في الملفات الأربعة** | بناء فشل بـ **17 × `CS0535`** (واجهة غير منفَّذة) لأن الملفات عُدّت في النطاق العام | **أُضيف `namespace DocGenerator.Application.Services;` وأُعيد البناء 0/0** |
| 2 | نسيان `partial` في الملف الرئيسي | كان سيسبب `CS0260` | صُحّح قبل البناء |
| 3 | خطر فقدان/تكرار عضو | مقارنة انعكاسية: **46 = 46** بلا فرق | لا فقدان |
| 4 | خطر تغيّر نص/سلوك | مقارنة الأسطر الدلالية: الفرق الوحيد `public sealed class` → `public sealed partial class` | متوقع ومقصود |
| 5 | حد `P4-02` ≤600 سطر | أكبر ملف **465** (`…Apply.cs`) | متحقق |
| 6 | صيغة `dotnet format` | `--verify-no-changes` | نظيف |
| 7 | الاعتماد على ترتيب الملفات | لا `using static` ولا ترتيب تهيئة | لا أثر |
| 8 | مرجع قديم في تعليق اختبار | `RF001DuplicateRequestCharacterizationTests.cs` يشير إلى أسطر الملف القديم (450-452/459-466) | حُدِّث إلى `DocumentService.Transfer.cs` بلا أرقام أسطر |

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` (بلا `PostgreSQL`) | **753 نجح / 1655 متجاوز (Postgres-gated) / 0 فشل** |
| `Api` غير التسلسلي | **901 نجح / 615 متجاوز / 0 فشل** |
| `Api` التسلسلي | **0 نجح / 5 متجاوز (Postgres-gated) / 0 فشل** |
| أكبر ملف بعد التفكيك | **465** سطرًا (من 600) |

## 4. حالة `P4-02`

المتبقي لبلوغ `DoD` العام: `CorrespondenceService` (1049)،
`DocumentContextBuilder` (912)، `DocumentNumberRegistry` (742)،
`PublicEntityService.Branches` (738)، `ReviewLetterService` (726)،
`UserManagementService` (710)، `PublicEntityService` (674)، `PortalService` (665)،
`PublicEntityService.Registry` (629).

## 5. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
