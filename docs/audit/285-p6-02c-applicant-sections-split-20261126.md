# `P6-02c` — تفكيك `ApplicantSideSections` — 2026-11-26

> **النطاق:** الخطوة الثالثة من `P6-02`: تفكيك
> `frontend/src/components/form/ApplicantSideSections.tsx` من **952** سطرًا إلى
> قشرة **157** سطرًا + أربعة أقسام، بلا تغيير سلوكي/عقود/هجرات.
>
> **المراجعات:** `286` (نقدية)، `287` (عدائية).

## 1. النتيجة

| الملف | قبل | بعد |
|---|---:|---:|
| `ApplicantSideSections.tsx` | **952** | **157** |
| `applicantSide/ApplicantAssetsSection.tsx` | — | 381 |
| `applicantSide/ApplicantGuarantorsSection.tsx` | — | 262 |
| `applicantSide/ApplicantContractSection.tsx` | — | 219 |
| `applicantSide/ApplicantBorrowerSection.tsx` | — | 171 |
| `applicantSide/ApplicantAssetsSection.test.tsx` | — | 37 |

كل الوحدات < 600. لم يُحذف الملف من قائمة الحارس في هذه المرحلة (يبقى مجمّدًا
على 952 ويُحذف في `P6-02d` مع نهاية الدفعة) — والنقص تحت الحد مسموح.

## 2. توزيع المسؤوليات

| الوحدة | المسؤولية |
|---|---|
| `ApplicantSideSections` | تركيب الأقسام فقط + `makeFieldHelpers` |
| `ApplicantContractSection` | السند/العقد/القرار، ملاحق العقد، المتضمن/الملاحظات، مبالغ المصرفي/العادي |
| `ApplicantBorrowerSection` | المقترض/المنفذ عليه (طبيعي/اعتباري)، الورثة، الممثل الشرعي |
| `ApplicantGuarantorsSection` | بطاقات الكفلاء (أكورديون، حتى 49)، ورثتهم وممثلوهم |
| `ApplicantAssetsSection` | أنواع الأموال الخمسة، الملاك/الحصص/الحجز، أزرار الإضافة |

## 3. ما نُقل حرفيًا

- شروط إظهار العنوان (`borrowerHasHeirs`/`borrowerRepStored`) وقاعدة تعارض
  الورثة/الممثل — كما هي.
- حساب `guarantorAddState` وترقيم الكفيل المستقر وأكورديون البطاقات — كما هو.
- فلاتر أنواع الأموال، خيارات الملاك، قفل الحجز، وحدود كل نوع — كما هي.
- `lock` (الملف المناب): إخفاء أقسام الأموال وأزرار الإضافة/الحذف — كما هو.

التغيير الوحيد الشكلي: استخراج `ASSET_ADD_BUTTONS` و`AssetField`/
`AssetRightShareSeizureFields` إلى ملف الأصول (كانت مساعدة داخلية في نفس الملف).

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| اختبارات `DocumentForm` السبعة (بلا تعديل) | **142/142** |
| اختبارات `components/form` القائمة | **37/37** |
| اختبار جديد `ApplicantAssetsSection` (قفل/عرض) | **2/2** — يسدّ فجوة اكتشفها الاختبار العدائي (`287`) |

## 5. البوابات (لهذه المرحلة)

`tsc` و`oxlint` نظيفان، والاختبارات الموجّهة 181/181. البوابات الكاملة
تُشغَّل مرة واحدة في نهاية دمج `P6-02c+d` قبل الـPR.

## 6. الهجرات

**لا هجرة.** لا تغيير خلفي.
