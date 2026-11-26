# `P6-02b` — تفكيك `DocumentView` — 2026-11-26

> **النطاق:** الخطوة الثانية من `P6-02`: تفكيك `frontend/src/pages/DocumentView.tsx`
> من **947** سطرًا إلى قشرة **380** سطرًا + ترويسة/نوافذ/hook، بلا تغيير سلوكي أو
> عقدي أو هجرات.
>
> **المراجع:** `docs/audit/282…284`، `docs/PERFECTION_ROADMAP.md` §`P6-02`،
> و`AGENTS.md`.

## 1. النتيجة المقيسة

| الملف | قبل | بعد |
|---|---:|---:|
| `pages/DocumentView.tsx` | **947** | **380** (**−59.9%**) |
| `pages/documentView/DocumentViewHeader.tsx` | — | 463 |
| `pages/documentView/DocumentViewModals.tsx` | — | 229 |
| `pages/documentView/useDocumentViewModals.ts` | — | 83 |
| `pages/documentView/useDocumentViewModals.test.ts` | — | 89 |

كل الوحدات **< 600**، وحُذف `DocumentView` من قائمة الحارس المجمّدة (12 ⇒ 11).

## 2. توزيع المسؤوليات

| الوحدة | المسؤولية |
|---|---|
| `DocumentView.tsx` | الجلب (`doc`/`delegations`/`appeals`)، اشتقاق الصلاحيات، اللوحات الأربع، تبويبات الجوال، حذف الإنابة |
| `DocumentViewHeader.tsx` | الشارة/العنوان، أزرار المكتب، قائمة «المزيد» للجوال، شريط الهوية اللاصق — مع قوائمها العائمة محليًا |
| `DocumentViewModals.tsx` | نوافذ الإجراءات/الحالة/النقل/التنبيه/الأرقام/التاريخ/الوقوعات/الاستئناف/الإنابة/حذف الإنابة |
| `useDocumentViewModals.ts` | 19 حالة نافذة + تصفير نوافذ الاستئناف عند تبديل الملف (`id`) |

## 3. ما نُقل حرفيًا

- شروط فتح كل نافذة وترتيبها ونصوصها و`onChanged`/`onSaved` (بما فيها
  `loadDelegations(); load()` بعد التسجيل/الإتمام) — كما هي.
- شروط أزرار الترويسة: `canEdit`، `canCreateAppeal`، `isExecuted`،
  `isDelegationExecuted`، `canTransfer`، `canDirectAlert` — كما هي.
- مفاتيح قائمة «المزيد» وترتيب فهارس `listRef` (ملاحة الأسهم) — كما هي.
- تصفير القوائم عند تبديل الملف (كان في الصفحة) صار داخل الترويسة/الـhook،
  بنفس الاعتمادية `[id]`.
- `MobileIdentityField` وشريط الهوية انتقلا إلى الترويسة بنفس البنية والأنماط.

الاستثناء الوحيد غير الدلالي: شرط `appealFormVariant && doc &&` صار
`modals.appealFormVariant &&` لأن `doc` مضمون غير `null` بعد بوابة التحميل
(السطور التالية للبوابة فقط).

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `DocumentView.test.tsx` القائم (بلا تعديل) | **78/78** |
| `DocumentView.interventions.test.tsx` القائم | **3/3** |
| `useDocumentViewModals.test.ts` جديد | **3/3** (الحالة الابتدائية، الفتح/الإغلاق، تصفير الاستئناف عند تبديل الملف مع بقاء بقية النوافذ) |
| الحزمة الكاملة | **1899/1899** في **179** ملفًا (خط الأساس بعد `P6-02a`: 1896/178؛ الفرق +3 للاختبار الجديد فقط) |

## 5. البوابات

| البوابة | النتيجة |
|---|---|
| `npx oxlint src scripts` | **0** |
| `npx tsc -b` | **0** |
| `npx vitest run` | **1899/1899** |
| `npm run build` | نجح |
| `npm run budget` | إجمالي **1741.2kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `npm run guard` (CSP) | نظيف — 93 ملفًا |
| `npm run generate:api` + `git diff` | لا انحراف |
| `module-size-guard` self-test/run | **12/12** · نظيف (11 مجمّدًا) |
| الخلفية | لا تغيير؛ بوابات `CI` (`backend`/`backend-postgres`) هي المرجع |

## 6. المراجعات

- `283` — المراجعة النقدية (تدقيق التكافؤ والملاحظات).
- `284` — المراجعة العدائية (محاولة إفشال اختبار التصفير + حالات حدّية).

## 7. `DoD` المتبقي

`P6-02` **ما زال جزئيًا**:

| الملف | الأسطر |
|---|---:|
| `components/form/ApplicantSideSections.tsx` | 952 |
| `components/form/ExecutedSideSections.tsx` | 681 |

بقية الملفات >600 مجمّدة بالحارس (10 ملفات) وخارج تسمية `P6-02` الأصلية.
الخطوة التالية: `P6-02c` — تفكيك `ApplicantSideSections` (وأخته `ExecutedSideSections`).

## 8. الهجرات

**لا هجرة جديدة.** لا تغيير خلفي؛ `has-pending-model-changes` يبقى «No changes».
