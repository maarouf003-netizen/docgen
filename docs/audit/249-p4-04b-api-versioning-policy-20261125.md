# `P4-04b` — سياسة إصدار `API` وسجل الإهمال وحارسه — 2026-11-25

> **النطاق:** إغلاق الفجوات **غير الكاسرة** المتبقية من `P4-04` (سياسة موثّقة + `ADR` +
> سجل إهمال + حارس آلي) **بلا تغيير سطح `API` العام**. لا تغيير مسار ولا `DTO` ولا شكل
> استجابة ولا هجرة ولا اعتمادية جديدة.
>
> **المرجع:** [`docs/API_VERSIONING.md`](../API_VERSIONING.md)،
> [`docs/adr/0011-api-versioning.md`](../adr/0011-api-versioning.md)،
> `docs/audit/54`، `docs/audit/97`، والمراجعتان `250`/`251`.

## 1. ما كان ناقصًا قبل هذه الجولة

1. **لا سياسة إصدار مكتوبة:** `P4-04` ثبّت العقد (`operationId` + لقطة + عميل مولَّد) لكن
   لم يحدّد متى يُدخل `/api/v1` أو `Accept-Version`، ولا نافذة إهمال، ولا قاعدة «لا كاسر
   بلا إصدار». النتيجة: خطر حذف/إعادة تسمية يمرّ بلا نسخة تعايش.
2. **لا سجل إهمال:** لا مكان موحّد يعلن `operationId` المهجور وبديله وموعد إزالته.
3. **لا حارس يربط علامة الإصدار بالاسم/المسارات:** اسم اللقطة `docgen-v1.json` و
   `info.version` كانا متطابقين **بالمصادفة** لا بالإنفاذ.
4. **أرقام قديمة في الفهرس:** `docs/README.md` كان يقول «239 عملية» بينما اللقطة الحالية
   تحوي **245 عملية** (نمت بعد `P4-01`)، و`P4-04` في الخريطة كان يذكر 239 بلا تأريخ.

## 2. القرار المنفّذ

1. **سياسة ملزمة** في `docs/API_VERSIONING.md`:
   - تمييز «إصدار العقد» (`info.version = v1`) عن «إصدار السطح» (غير موجود).
   - السطح الحالي `/api/…` بلا إصدار، والمستهلك الموثّق الوحيد هو `SPA` المضمَّن.
   - شروط إدخال الإصدار (مستهلك خارجي / تغيير كاسر / انحراف مولَّد غير عازل).
   - عند التفعيل: **بادئة `/api/v1`** هي الافتراضي، و`Accept-Version` مرفوض الآن.
   - الإهمال: **إعلان ← تشغيل مزدوج ← إزالة**، بنافذة **≥90 يومًا + دورة إصدار كاملة**.
   - **لا تغيير كاسر بلا إصدار**؛ ما دامت الآلية مؤجلة فالكاسر **ممنوع**.
2. **`ADR-0011`** يسجّل القرار والبدائل المرفوضة (البقاء بلا إصدار، `Accept-Version` الآن،
   `Asp.Versioning` الآن، إصدار بالتاريخ، إصدار لكل متحكم، الاعتماد على المولَّد بدل
   السياسة، الفحص اليدوي بدل الحارس) والمالك/الاعتمادية.
3. **سجل `docs/openapi/deprecations.json`** — فارغ `[]` حاليًا، بمخطط مغلق:
   `operationId`، `deprecatedSince`، `removalVersion` و/أو `removalDate`، `replacement`.
4. **حارس `ApiVersioningPolicyTests`** (4 اختبارات، `Unit`، بلا قاعدة/مضيف):
   - `Snapshot_FileNameAndVersionMarkerAgreeWithRoutes`: اسم اللقطة ينتهي بـ`-{info.version}.json`،
     وأي مسار `/api/vN` يطابق إصدار العقد.
   - `Snapshot_OperationIdsAreStableAndUnique`: كل عملية لها `operationId` بنمط
     `Controller_Action` وفريد، والعدد ≥100.
   - `PolicyAndAdr_AreLinkedAndDocumentTheContractPaths`: وجود السياسة و`ADR`، وذكر مساري
     اللقطة والسجل في السياسة، وربط الاثنين من `docs/README.md`.
   - `DeprecationManifest_MatchesSchemaAndSnapshot`: مطابقة المخطط (مفاتيح مغلقة، تواريخ
     `yyyy-MM-dd`، `removalVersion` لاحق للإصدار الحالي، `removalDate` بعد `deprecatedSince`،
     `replacement` غير فارغ، `operationId` موجود، لا تكرار)، والاتجاهان: كل سطر في السجل
     يجب أن تقابله عملية `deprecated: true` في اللقطة، وكل عملية موسومة يجب أن تكون مسجّلة.
5. **تحديث الفهرس والخريطة:** رابط السياسة و`ADR-0011` وسجل الإهمال، وتصحيح عدد العمليات
   إلى **245** مع تأريخ الـ239 السابقة، وإضافة حالة `P4-04b` مع النطاق المتبقي صراحة.

## 3. الملفات

| الملف | الحالة | الدور |
|---|---|---|
| `docs/API_VERSIONING.md` | جديد | السياسة الملزمة (المصدر الوحيد لقواعد الإصدار/الإهمال) |
| `docs/adr/0011-api-versioning.md` | جديد | القرار والبدائل المرفوضة والمالك/الاعتمادية |
| `docs/openapi/deprecations.json` | جديد | سجل الإهمال (`[]`)، مخططه موثّق ومحروس |
| `backend/tests/DocGenerator.Api.Tests/ApiVersioningPolicyTests.cs` | جديد | الحارس الآلي (4 اختبارات) |
| `docs/README.md` | محدَّث | الفهرس: `ADR-0011`، السياسة، السجل، واللقطات `249…251` |
| `docs/PERFECTION_ROADMAP.md` | محدَّث | حالة `P4-04b` والنطاق المتبقي الصريح |

## 4. برهان أن الحارس يفشل فعلًا (لا يمرّ كاذبًا)

نُفِّذت طفرات مؤقتة على نسخة العمل ثم أُعيدت، وبقي الملفان مطابقين للمستودع:

| الطفرة | الاختبار الذي فشل | النتيجة |
|---|---|---|
| سطر إهمال لعملية غير موجودة (`Nope_DoesNotExist`) | `DeprecationManifest_MatchesSchemaAndSnapshot` | **FAIL** (exit 1) |
| مفتاح خارج المخطط (`unknownKey`) | `DeprecationManifest_MatchesSchemaAndSnapshot` | **FAIL** (exit 1) |
| سطر لعملية موجودة لكنها غير موسومة `deprecated: true` | `DeprecationManifest_MatchesSchemaAndSnapshot` | **FAIL** (exit 1) |
| تغيير `operationId` إلى `bad-id` في اللقطة | `Snapshot_OperationIdsAreStableAndUnique` | **FAIL** (exit 1) |
| إعادة الملفات إلى أصلها | الاختبارات الأربعة | **4/4 PASS** |

> الطفرات لم تُلمس في الالتزام: `git diff` للقطة = 0، والسجل عاد `[]`.

## 5. التحقق (محلي)

| البوابة | الأمر | النتيجة |
|---|---|---|
| البناء | `dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror` | **0 Warning / 0 Error** |
| التنسيق | `dotnet format backend/DocGenerator.sln --verify-no-changes --no-restore` | `exit 0` |
| المعمارية | `DocGenerator.Architecture.Tests` | **29/29** |
| `Api` (غير تسلسلي) | `--filter "Category!=Sequential"` | **905 passed / 615 skipped / 1520 total** (+4 للاختبارات الجديدة؛ التخطي لأن `PostgreSQL` غير متاح محليًا) |
| `Api` (تسلسلي) | `--filter "Category=Sequential"` | **0 passed / 5 skipped** (بوابة `PostgreSQL` نفسها) |
| `Application` | بلا متغيّر `PostgreSQL` | **772 passed / 1661 skipped / 2433 total** (بلا تغيير) |
| الحراس الستة | `subhead`/`determinism`/`migration-watchlist`/`migration-safety`/`secrets`/`postgres-suite --self-test` | **0,0,0,0,0,0**؛ 7 هجرات مسجّلة، 6 سطور غير مشطوب |
| تدقيق الاعتماديات | `dependency-audit --self-test` + `dependency-audit` + `npm-audit-guard --self-test` + `npm-audit-guard` | **0,0,0,0**؛ لا High/Critical |
| EF | `has-pending-model-changes` (سلسلة وهمية، `--no-build`) | «No changes… since the last migration» |
| الواجهة | `npx oxlint src` / `npx tsc -b` / `npm run build` / `npm run guard` | **0 / 0 / ناجح / 94 ملفًا** |
| `vitest` | `npx vitest run` | **1809/1809** في **171** ملفًا (ملفات الواجهة لم تُمسّ) |
| العميل المولَّد | `npm run generate:api` ثم `git diff --exit-code -- src/api/generated.ts` | **exit 0** بعد إعادة ضجيج `CRLF` فقط (`git checkout`) |

## 6. الحدود الصريحة (ما لم يُغلق)

1. **آلية الإصدار وقت التشغيل مؤجلة بقرار موثّق** — لا `/api/v1` ولا `Accept-Version`
   ولا `[ApiVersion]`. **المالك:** مالك المشروع. **الاعتمادية:** `staging` للتشغيل المزدوج
   (`P5-04`)، وتغيير عنوان القاعدة في `frontend/src/api/client.ts`، وإعادة توليد
   `generated.ts`، وإحلال `types/document.ts` (`P6-06`). حتى التفعيل: **الكاسر ممنوع**.
2. **حارس الوثائق يفحص الوجود/الربط لا الدلالة** — يمكن «إرضاؤه» بذكر المسارين في نص
   السياسة؛ هذا حدّ متأصل في حراس المستندات، والمراجعة البشرية تبقى للدلالة.
3. **الحارس يعمل داخل `DocGenerator.Api.Tests`** — في `CI` يُنفَّذ ضمن `backend-postgres`
   (الذي يشغّل اختبارات `Api`)، وليس في وظيفة `backend` (لا تشغّل اختبارات `Api` أصلًا).
   الاختبارات الأربعة `[Fact]` عادية فلا تحتاج قاعدة أصليًا.
4. **`OpenAPI` لا يُخدم في الإنتاج** — يبقى `Swagger:Enabled=false` (`P2-03`)؛ اللقطة في
   المستودع هي المرجع. تفعيله محميًا في `staging/prod` بند متبقٍ من `P4-04`.

## 7. الهجرات

**لا هجرة جديدة.** لا تغيير في نموذج EF (`has-pending-model-changes` نظيف)، والقائمة
المعلّقة تبقى **6** كما هي في `RUN_GUIDE.md` §9 و`AGENTS.md`.
