# `P4-01a` — شريحة Command/Query رأسية — 2026-11-23

> **النطاق:** تأسيس خط أوامر/استعلامات (`P4-01`) بشريحة رأسية ضيقة في وحدة
> `Documents`: عقود + 3 سلوكيات + نقطتا `Get`/`Create`، **بلا تغيير سلوكي**.
> المتحكم يبقى مسؤولًا عن HTTP والصلاحية، والمعالج يفوض الخدمة الحالية.

## 1. ما أُضيف

| المكوّن | الموقع | الوصف |
|---|---|---|
| العقود | `Application/Common/Pipeline/PipelineContracts.cs` | `ICommand<T>`، `IQuery<T>`، `ICommandHandler`، `IQueryHandler`، `IPipelineBehavior`، `IRequestValidator`، وسما `ITransactionalRequest`/`IAuditedRequest` |
| الأساس | `…/PipelineHandlerBase.cs` | `CommandHandlerBase`/`QueryHandlerBase` + تركيب السلوكيات بترتيب التسجيل (الأول خارجي) |
| السلوكيات | `…/PipelineBehaviors.cs` | `ValidationBehavior` (يجمع أخطاء المدققين ويرفض قبل أي أثر)، `TransactionBehavior` (فقط للطلبات الموسومة)، `AuditBehavior` (بعد النجاح فقط للموسومة) |
| الاستعلام | `Application/UseCases/Documents/GetDocumentQuery.cs` | `GetDocumentQuery` + معالج يفوض `IDocumentService.GetAsync` |
| الأمر | `…/CreateDocumentCommand.cs` | `CreateDocumentCommand` + معالج يفوض `IDocumentService.CreateAsync` |
| التسجيل | `Application/DependencyInjection.cs` | السلوكيات الثلاثة كـ open generics بالترتيب + المعالجان |
| المتحكم | `DocumentsController` | `Get`/`Create` يستدعيان المعالجين؛ بقية المسارات كما هي |
| الاختبارات | `Application.Tests/PipelineBehaviorTests.cs` | 4 اختبارات: الترتيب، منع الآثار عند فشل التحقق، تخطي المعاملة/التدقيق لغير الموسوم، وأساس الاستعلام |

## 2. قرارات نطاق (مبرَّرة)

- **`TransactionBehavior`/`AuditBehavior` موسومان صراحةً** ولم يُطبَّقا على
  `CreateDocumentCommand`: الخدمة الحالية تملك المعاملة والتدقيق، ففرضهما في الخط
  كان سيضاعفهما (خرق «بلا تغيير سلوكي»). جاهزان للمعالجات التي تملك use-case كاملًا.
- **لا سلوك تفويض/عدم تكرار في الخط الآن**: المتحكم يفحص الصلاحية، والخدمة تدير
  مفتاح عدم التكرار — نقلهما مرحلة لاحقة عند نقل الملكية.
- `Get`/`Create` يحافظان حرفيًا على السلوك (نفس الخدمة، نفس الاستثناءات، نفس التدقيق
  و`CreatedAtAction` و`IdempotentReplayException`).

## 3. المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | كسر بناء اختبارات تُنشئ `DocumentsController` يدويًا | 3 × `CS7036` | أُضيفت الوسيطتان الجديدتان (`null!`) في الاختبارات الثلاثة |
| 2 | خطر معاملة متداخلة | الخدمة تفتح معاملتها | السلوك لا يعمل إلا للطلبات الموسومة، والاختبار يثبت التخطي |
| 3 | خطر تدقيق مزدوج | الخدمة تدقق أصلًا | نفس الضمان بالوسم، واختبار التخطي |
| 4 | ترتيب السلوكيات | اختبار ترتيب | `validate → tx-start → core → audit → tx-end` |
| 5 | فشل التحقق قبل الأثر | اختبار | لا معاملة/لا تدقيق/لا نواة |
| 6 | بناء/تنسيق | `build -warnaserror` و`format` | 0/0 ونظيف |

## 4. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` | **757 نجح / 1655 متجاوز / 0 فشل** (بعد +4 اختبارات) |
| `Api` غير التسلسلي | **901 نجح / 615 متجاوز / 0 فشل** |
| `Api` التسلسلي | **0 نجح / 5 متجاوز / 0 فشل** |
| الحرّاس الستة | نظيفة |

## 5. المتبقي في `P4-01`

توسيع تدريجي: نقل ملكية use-case كاملة (معاملة/تدقيق/عدم تكرار) إلى معالجات
موسومة، ثم تحويل بقية نقاط الكتابة module-by-module. لا تغيير في بوابة النشر.

## 6. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
