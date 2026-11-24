# `P4-01bu` — نقل `AuthService.LoginAsync` إلى Command/Handler — 2026-11-24

> **النطاق:** إغلاق أول بند في «متبقٍ» خريطة `P4-01b`: ملكية مسار الدخول كاملة
> (القفل، التدقيق، ترقية صيغة الهاش) داخل معالج أمر واحد، مع الحفاظ الحرفي على
> السلوك، عبر الاستحقاق `P4-01bu` على فرع `refactor/p4-01bu-login-command`
> من `master` (`685037bc`).

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأمر | `LoginCommand(LoginRequest Request)` في `UseCases/Auth/LoginCommand.cs` |
| المعالج | `LoginCommandHandler` بمعاملاته السبعة: `IUserRepository` / `IUnitOfWork` / `IPasswordHasher` / `ITokenService` / `ITransactionRunner` / `IAuditLogger` / `IOptions<LockoutOptions>` |
| العقود | حُذف `LoginAsync` من `IAuthService`؛ بقي `GetUserAsync` وحده |
| الخدمة | `AuthService` صارت قراءةً فقط: حقل `_users` وحده، وحُذفت الحقول الستة (`_uow`/`_hasher`/`_tokenService`/`_tx`/`_audit`/`_lockout`) ومعاملات بانيها |
| الإسقاط | `UserDtoMapper.ToDto` (`Common/UserDtoMapper.cs`) مشترك بين `GetUserAsync` والدخول الناجح — لا نسخة ثانية من التطابق |
| المتحكم | `AuthController` يحقن `ICommandHandler<LoginCommand, LoginResult>` ويستدعي `HandleAsync(new LoginCommand(request), ct)`؛ نقطة `me` بقيت على `IAuthService` |
| `DI` | `services.AddScoped<ICommandHandler<LoginCommand, LoginResult>, LoginCommandHandler>();` |
| حدود المعاملة | لا وسم `ITransactionalRequest` (لا معاملة متداخلة)؛ المعالج يملك معاملاته الثلاث كما في الأصل: تحرير القفل المنتهي، مسار الفشل، مسار النجاح |
| الاختبارات | جسر `LoginCommandExtensions.LoginAsync` (توقيع `LoginAsync(LoginRequest, ct)` القديم) في `TestHelpers`، وتحديث أربعة ملفات اختبار |

## إثبات الحفاظ على السلوك

- **مقارنة حرفية للنصوص (اتجاهان):** 19 نصًّا فريدًا في الوالد و19 في المعالج؛
  `missing = 0` و`added = 0` — رسائل التدقيق كلها كما هي.
- **مقارنة سطرية لجسم الدالة:** 122 سطرًا في الوالد و122 في المعالج، وفرق واحد
  فقط: `ToDto(user)` ← `UserDtoMapper.ToDto(user)`.
- **تطابق الإسقاط:** جسم `ToDto` القديم وجسم `UserDtoMapper.ToDto` متطابقان حرفيًا
  (تحقق آلي بعد التطبيع).
- **ترتيب المنطق:** اختيار الحساب (بما فيه الفرع و`0`) ← استثناء القفل الجماعي ←
  فحص القفل ← تحرير القفل المنتهي داخل معاملة ← بوابة الفرع الإلزامي ← التحقق من
  كلمة المرور ← العدّاد/القفل التراجعي ← النجاح مع ترقية الهاش — نفس التسلسل.
- **حدود المعاملة:** ثلاث نقاط `_tx.RunAsync` بنفس المواضع ونفس ما بداخلها
  (عدّاد + تدقيق في معاملة واحدة؛ تحرير + تدقيق في معاملة واحدة).
- **المتحكم:** محدد المحاولات (`LoginIpPolicy`)، `Retry-After`، كوكيز الجلسة/`CSRF`،
  وترجمة `LoginStatus` الخمس بقيت كما هي حرفيًا — لم يُلمس أي سطر خارج حقن الأمر.
- **الترقية الشفافة:** `NeedsUpgrade`/`Hash`/`upgrade_password_format` بنفس الشروط،
  و`TokenVersion` لا يُلمس.

## مراجعة نقدية أولى (قبل الإثبات النهائي)

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل وُسم الأمر بـ`ITransactionalRequest` فتنشأ معاملة خارجية حول معاملات داخلية؟ | توقيع `LoginCommand` | لا وسم — السلوك مطابق |
| 2 | هل يضاعف `AuditBehavior` تدقيق الدخول؟ | الأمر لا ينفّذ `IAuditedRequest` | لا تدقيق مزدوج |
| 3 | هل يعمل `ValidationBehavior` بلا مدققات مسجّلة؟ | لا `IRequestValidator<LoginCommand>` | يمرّ بلا أثر |
| 4 | هل بقي في `AuthService` حقل أو معامل بلا استخدام بعد النقل؟ | قراءة الملف كاملًا + بناء `-warnaserror` | 0 حقول ميتة؛ الباني بحقل واحد |
| 5 | هل تكرّر إسقاط `UserDto` في موضعين؟ | استخراج `UserDtoMapper` ومقارنة الجسم | نسخة واحدة مشتركة |
| 6 | هل يُحلّ `IOptions<LockoutOptions>` في التشغيل؟ | `Program.cs:177` يربط القسم `Lockout` | مربوط مسبقًا |
| 7 | هل كل معالج مسجَّل في `DI`؟ | عدّ آلي للملفات مقابل `DependencyInjection.cs` | 103 معالجًا / 103 تسجيلات / 0 غير مسجَّل |
| 8 | هل بقيت استدعاءات `LoginAsync` على خدمة في `backend/src`؟ | بحث شامل | لا مستدعي متبقٍّ |

## التحقق

| البوابة | الأمر | النتيجة |
|---|---|---|
| backend | `build -warnaserror` · `format --verify-no-changes` | 0 تحذير/0 خطأ · نظيف |
| Architecture | `dotnet test …Architecture.Tests` | 29/29 |
| Application | `dotnet test …Application.Tests` | 758 ناجح / 1659 متخطّى (لا `PostgreSQL` محلي) / 0 فشل |
| Api | غير التسلسلي / التسلسلي | 901 / 615 / 0 · 0 / 5 / 0 |
| frontend | `oxlint` · `tsc -b` · `vitest run` · `build` · `guard` · `generate:api` | نظيف · نظيف · 1809/1809 · ناجح · نظيف (94 ملفًا) · بلا فرق |
| guards | الستة | نظيفة (حتمية: 0 استثناءات) |
| audit | `dependency-audit` · `npm-audit-guard` | نظيف (9 مشاريع) · نظيف |
| migration-guard | `has-pending-model-changes` | لا تغييرات في الموديل |
| `DI` | عدّ التسجيلات | 103 معالجًا / 0 غير مسجَّل |

> **ملاحظة صدق:** اختبارات `PostgreSQL` الحقيقية (ومنها اختبارات الدخول في
> `AuthServiceTests` و`RF012LoginAtomicityTests` و`AuthIntegrationTests`) تُخطّى
> محليًا لغياب خادم؛ بوابتا `backend-postgres` في `CI` هما المرجع القاطع قبل الدمج،
> ولا يُدّعى تشغيلهما هنا.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**، ولا يُشغَّل
`dotnet ef database update --context DocGeneratorPostgresDbContext` ضمن هذه المرحلة.
