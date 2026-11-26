# `P6-02a` — تفكيك `DocumentForm` إلى وحدات — 2026-11-26

> **النطاق:** الخطوة الأولى من `P6-02` (تفكيك النماذج والعروض): تفكيك
> `frontend/src/pages/DocumentForm.tsx` من **1526** سطرًا إلى قشرة تركيبية
> **449** سطرًا + سبع وحدات مسؤولة، بلا تغيير سلوكي/عقود `API`/هجرات.
>
> **المرجع:** `docs/PERFECTION_ROADMAP.md` §`P6-02`، `AGENTS.md` (المعايير،
> mobile-first، التواريخ الحرة، سياسة الوثائق)، ولقطتا المراجعة `280`/`281`.
>
> **لا تغيير في:** `DocumentUpsertRequest`/`DocumentResponse`، مسارات `API`،
> التحقق الخلفي، أو الحمولة المرسلة. `has-pending-model-changes` غير متأثر
> (لا تغيير خلفي أصلًا).

## 1. المشكلة المقيسة

| البند (عند `c5a92045`) | القيمة |
|---|---:|
| `frontend/src/pages/DocumentForm.tsx` | **1526** سطرًا |
| حالات `useState` | **33** |
| آثار `useEffect` | **4** (دوائر، تحميل ملف، إنابات، استئنافات) |
| مسؤوليات الملف | حالة الأطراف + التحميل + التحقق + بناء الحمولة + واجهة القسم الأساسي + النوافذ |

الحد الملزم في `DoD` هو **600 سطر** للملف الواحد. الملف كان يخلط خمسة أنواع
مسؤوليات ويجعل أي تعديل في حمولة الحفظ يمرّ عبر ملف ضخم، فكان أكبر مكسب صيانة
متاح في `P6`.

## 2. البنية بعد التفكيك

| الملف | الأسطر | المسؤولية |
|---|---:|---|
| `pages/DocumentForm.tsx` | **449** | قشرة تركيبية: الـhooks + قسم الصفة + تركيب الأقسام/النوافذ + الحذف |
| `pages/documentForm/useDocumentParties.ts` | 491 | حالة ومنطق الأطراف (كفلاء/ورثة/أموال/طالبو التنفيذ/جهات/أشخاص/ممثلون) |
| `pages/documentForm/useDocumentFormState.ts` | 253 | النموذج الأساسي، التحميل/التصفير، المبالغ، المتدخلون، حذف الملف |
| `pages/documentForm/DocumentBasicsSection.tsx` | 243 | قسم «المعلومات الأساسية» (الدائرة/الملف/الورود/الجهات الطالبة) |
| `pages/documentForm/documentFormPayload.ts` | 241 | `createEmptyUpsertForm` + `buildDocumentPayload` (نقية) |
| `pages/documentForm/useDocumentReferences.ts` | 123 | دوائر/إنابات/استئنافات ونوافذ الاختيار |
| `pages/documentForm/documentFormValidation.ts` | 122 | تحقق ما قبل الإرسال (رسائل وترتيب كما هو) |
| `pages/documentForm/useDocumentSubmit.ts` | 101 | الإرسال: تحقق → حمولة → `POST/PUT`/تحذير الرقم |
| `pages/documentForm/documentFormPayload.test.ts` | 262 | **11** اختبارًا للحملة النقية |
| `pages/documentForm/documentFormValidation.test.ts` | 232 | **17** اختبارًا للتحقق |

كل الوحدات الجديدة **< 600** سطرًا، و`DocumentForm` انخفض من 1526 إلى 449
(**−70.6%**).

## 3. خريطة نقل السلوك (لا إعادة كتابة)

المنهج: نقل حرفي للشيفرة من `c5a92045`، ثم إزالة التكرار فقط حيث لا يغيّر السلوك.

| العنصر | المصدر (قبل) | الوجهة (بعد) |
|---|---|---|
| 9 حالات أطراف + `isMirror`/`draftPartyReps` | `DocumentForm.tsx:135-160` | `useDocumentParties` |
| معالجات الكفلاء/الورثة/الأموال/الجهات/الممثلين | `:394-760` | `useDocumentParties` (منقولة حرفيًا) |
| قفل تاريخ الحجز المرتبط بالإنابات | `:397-411` | `setEstate` في `DocumentForm` + `referencedAssetIds` من `useDocumentReferences` |
| تحميل الدوائر/الإنابات/الاستئنافات | `:121-134`, `:318-375` | `useDocumentReferences` (نفس حارس `cancelled`) |
| 18 حالة أساسية + التحميل/التصفير/المتدخلون | `:97-231`, `:233-314` | `useDocumentFormState` |
| تحقق ما قبل الإرسال (ترتيب الرسائل) | `:786-862` | `validateDocumentForm` |
| بناء الحمولة (تطبيع/تصفير/فلاتر/عملات) | `:864-1001` | `buildDocumentPayload` |
| `POST/PUT` + تحذير إعادة الرقم + مفتاح عدم التكرار | `:1002-1016` | `useDocumentSubmit` |
| قسم المعلومات الأساسية | `:1175-1347` | `DocumentBasicsSection` |

**الدليل على التكافؤ:** `git show c5a92045:frontend/src/pages/DocumentForm.tsx`
هو المصدر الحرفي، وملفات الاختبار السبعة القائمة (`DocumentForm.*.test.tsx`)
عبرت **دون تعديل سطر واحد** (170 اختبارًا في 9 ملفات، §4). كما أن
`useDocumentParties` و`useDocumentFormState` تحتفظان بالتعليقات القرارية
المرتبطة بكل قاعدة (قرار 5/7، قرار 10/F1، RF-010، RF-011).

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| اختبارات الوحدات الجديدة (حملة/تحقق) | **28/28** (11 + 17) |
| ملفات `DocumentForm` السبعة القائمة (بلا تعديل) | **142/142** ضمن 9 ملفات = **170** |
| الحزمة الكاملة `npx vitest run` | **1896/1896** في **178** ملفًا |
| خط الأساس قبل التغيير | 1868 في 176 ملفًا (فرق **+28** = الاختبارات الجديدة فقط) |

تغطي اختبارات الحملة: تطبيع التواريخ والأرقام العربية، حذف/إبقاء مفاتيح
`DateTime` الفارغة، قصّ الملاحق، عملات الخانات بلا تكرار، ترقيم الكفلاء
(موضعي/مستقر)، تصفير عنوان المقترض للمخزَّن فقط، أوضاع «منفذ عليه/عرض وايداع»،
مسح الوكيل القانوني لممثل مخزَّن، و`version` (RF-010). وتغطي اختبارات التحقق:
الدائرة (مع/بلا سجل، والمناب)، رقم/سنة/تاريخ القيد، السنة المستقبلية بالأرقام
العربية، التجديد بعد الشطب، ربط السجل المرجعي للطرفين، وتعارض الممثل/الورثة.

## 5. حارس حجم الوحدات (جديد)

`frontend/scripts/module-size-guard.mjs` — ratchet يمنع التراجع:

- أي ملف مصدر جديد (`src/**/*.ts(x)` عدا الاختبارات/التعريفات/`generated.ts`)
  يتجاوز **600** سطر ⇒ فشل.
- الملفات القائمة فوق الحد مُدرجة في `FROZEN_MAX_LINES` (12 ملفًا) بقيمها
  المقيسة؛ **تنقص فقط**، ولا تُرفع يدويًا. إدخال مجمّد بلا ملف ⇒ فشل (كي لا
  تتقادم القائمة).
- `--self-test`: **12/12** حالة (الحد، النمو، الانكماش، الإدخال القديم،
  والاستثناءات).
- أُضيف إلى وظيفة `guards` في `.github/workflows/ci.yml`
  (`Self-test frontend module size guard` + `Frontend module size guard`).

التشغيل على الشجرة بعد التغيير:
`module-size-guard: نظيف — 262 ملفًا مصدريًا، 12 ملفًا مجمّدًا فوق 600 سطرًا.`

## 6. البوابات (بعد التغيير)

| البوابة | النتيجة |
|---|---|
| `npx oxlint src scripts` | **0** |
| `npx tsc -b` | **0** |
| `npx vitest run` | **1896/1896** (178 ملفًا) |
| `npm run build` | نجح (1.96s) |
| `npm run budget` | `JS=90` · إجمالي **1738.6kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `npm run guard` (CSP) | نظيف — 93 ملفًا |
| `npm run generate:api` + `git diff` | **لا انحراف** |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture.Tests` | **41/41** |
| `Application.Tests` | **786** ناجح / 1661 متجاوز / **0** فاشل |
| `Api.Tests` (`Category!=Sequential`) | **916** ناجح / 616 متجاوز / **0** فاشل |
| `Api.Tests` (`Category=Sequential`) | 0 ناجح / 5 متجاوز (Postgres-gated محليًا) |
| الحراس: `subhead`/`determinism`/`dead-code` (10/10)/`migration-watchlist` (8 هجرات/7 أسطر)/`migration-safety` (8) | نظيفة |
| `observability`/`backup-scripts`/`postgres-suite` self-tests | نظيفة |
| `module-size-guard` self-test/run | **12/12** · نظيف |

> `backend-postgres` هو المرجع القاطع لاختبارات `PostgresFact` (616+1661
> متجاوزة محليًا لغياب `DOCGEN_TEST_POSTGRES`)؛ لا تغيير خلفي في هذا الـPR.

## 7. المراجعة العدائية (ملخص؛ التفصيل في `281`)

- **محاولة إفشال اختبار الحملة:** إزالة تصفير `legalRepresentative` لممثل
  مخزَّن ⇒ فشل اختبار واحد من 11 فورًا، ثم أُعيد السطر.
- **محاولة تجاوز حارس الحجم:** رفع `LIMIT` إلى 700 ⇒ فشل الاختبار الذاتي
  «601 غير مجمّد مرفوض»، ثم أُعيد 600.
- **حادثة أدوات:** محاولة استبدال نص عربي عبر `Set-Content` أفسدت ترميز ملف
  اختبار؛ أُعيد كتابة الملف كاملًا بأداة التحرير (وفق `AGENTS.md`)، والدرس
  مثبّت: لا `Set-Content` للملفات العربية.

## 8. `DoD` — جزئي ومعلن

`P6-02` **لم يُغلق**: انخفض `DocumentForm` تحت الحد، لكن ما زالت ملفات فوق 600:

| الملف | الأسطر | الحالة |
|---|---:|---|
| `pages/DocumentView.tsx` | 947 | **متبقٍ** (`P6-02b`) |
| `components/form/ApplicantSideSections.tsx` | 952 | **متبقٍ** (`P6-02c`) |
| `components/form/ExecutedSideSections.tsx` | 681 | **متبقٍ** |
| `pages/DocumentsList.tsx` | 1060 | خارج تسمية `P6-02` الأصلية، مجمّد بالحارس |
| `pages/BranchesManagement.tsx` | 870 | مجمّد بالحارس |
| `components/entity/BranchManagementModal.tsx` | 834 | مجمّد بالحارس |
| `components/dashboard/DayRemindersModal.tsx` | 725 | مجمّد بالحارس |
| `components/form/InterventionModal.tsx` | 650 | مجمّد بالحارس |
| `pages/EntityRegistryReviewManagement.tsx` | 642 | مجمّد بالحارس |
| `components/form/documentFormConstants.ts` | 618 | مجمّد بالحارس |
| `components/StatusChangeModal.tsx` | 605 | مجمّد بالحارس |
| `pages/UsersManagement.tsx` | 602 | مجمّد بالحارس |

`DoD` الكامل («لا ملف > 600 واختبارات كل قسم») يبقى **غير متحقق** حتى تفكيك
`DocumentView` و`ApplicantSideSections` على الأقل. الحارس يضمن ألا يزيد الدين.

## 9. الهجرات

**لا هجرة جديدة.** لا تغيير في `backend/src` ولا النموذج؛ `has-pending-model-changes`
يبقى «No changes». لا حاجة لتطبيق شيء في نافذة النشر لهذا التغيير.
