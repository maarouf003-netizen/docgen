# `P6-02d` — تفكيك `ExecutedSideSections` وإغلاق نطاق `P6-02` — 2026-11-26

> **النطاق:** الخطوة الرابعة والأخيرة في نطاق `P6-02` المسمّى: تفكيك
> `frontend/src/components/form/ExecutedSideSections.tsx` من **681** سطرًا إلى
> قشرة **174** سطرًا + خمسة أقسام، بلا تغيير سلوكي/عقود/هجرات.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---:|---:|
| `ExecutedSideSections.tsx` | **681** | **174** |
| `executedSide/ExecutedApplicantSection.tsx` | — | 207 |
| `executedSide/ExecutedEntitySection.tsx` | — | 173 |
| `executedSide/ExecutedStatusSection.tsx` | — | 132 |
| `executedSide/ExecutedNaturalPersonSection.tsx` | — | 130 |
| `executedSide/ExecutedContractSection.tsx` | — | 96 |

وحُذف `ApplicantSideSections` (952 ⇒ 157) و`ExecutedSideSections` (681 ⇒ 174)
من قائمة الحارس المجمّدة: **11 ⇒ 9** ملفًا.

## 2. توزيع المسؤوليات

| الوحدة | المسؤولية |
|---|---|
| `ExecutedSideSections` | تركيب الأقسام + `field` helper + `isDeposit` فقط |
| `ExecutedContractSection` | السند/القرار، المتضمن/الملاحظات، المبلغ المطلوب/المعروض |
| `ExecutedApplicantSection` | طالب التنفيذ/العرض (طبيعي/اعتباري)، الورثة/الممثل |
| `ExecutedEntitySection` | المنفذ عليه: جهة عامة (سجل) أو شخص اعتباري + أزرار الإضافة |
| `ExecutedNaturalPersonSection` | الأشخاص الطبيعيون المنفذ عليهم |
| `ExecutedStatusSection` | خيارات الحالة المرشَّحة، مبالغ التنفيذ/الإيداع، الشطب، التجديد |

## 3. ما نُقل حرفيًا

- ترشيح خيارات «الحالة» (`targetsOf` + استثناء «متداول» في «عرض وايداع») كما هو.
- شروط `hasEstate`/`aRepStored`/`pHasRep` وإخفاء/إظهار الممثل والورثة — كما هي.
- قفل الملف المناب: إخفاء أزرار الإضافة/الحذف وتعطيل الحقول — كما هو.
- مبالغ `paidAmountKeys`/`requiredAmountKeys` وعملاتها ونصوص `firstLabel`/
  `otherLabel` — كما هي.
- موضع زر «إضافة شخص طبيعي» بقي مع أزرار المنفذ عليه (نفس ترتيب DOM السابق).

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| اختبارات `DocumentForm` السبعة (بلا تعديل) | **142/142** |
| اختبارات `components/form` القائمة | **37/37** |
| `ApplicantAssetsSection.test.tsx` (من `P6-02c`) | **2/2** |
| `ExecutedEntitySection.test.tsx` جديد (قفل الإضافة/الحذف) | **2/2** |
| الحزمة الكاملة بعد الدفعة | **1903/1903** في **181** ملفًا |

## 5. البوابات (النهائية للدفعة)

| البوابة | النتيجة |
|---|---|
| `npx oxlint src scripts` | **0** |
| `npx tsc -b` | **0** |
| `npx vitest run` | **1903/1903** (181 ملفًا) |
| `npm run build` | نجح |
| `npm run budget` | إجمالي **1744.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `npm run guard` (CSP) | نظيف |
| `npm run generate:api` + `git diff` | لا انحراف |
| `module-size-guard` self-test/run | **12/12** · نظيف (9 مجمّدين) |

## 6. `DoD` — نطاق `P6-02` مكتمل، والحد العام معلن

الملفات المسمّاة في `P6-02` صارت جميعًا **< 600**:

| الملف | قبل | بعد |
|---|---:|---:|
| `DocumentForm.tsx` | 1526 | 449 (`P6-02a`) |
| `DocumentView.tsx` | 947 | 380 (`P6-02b`) |
| `ApplicantSideSections.tsx` | 952 | 157 |
| `ExecutedSideSections.tsx` | 681 | 174 |

**لكن** «لا ملف > 600» على مستوى الواجهة كلها **لم يتحقق بعد**: 9 ملفات خارج
تسمية `P6-02` ما زالت أكبر (أكبرها `DocumentsList.tsx` 1060)، وهي **مجمّدة**
بالحارس (تنقص فقط). تُعالَج ضمن `P6-03`/صقل لاحق، لا يُخفى الدين.

## 7. الهجرات

**لا هجرة.** لا تغيير خلفي.
