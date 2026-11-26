# `P6-02g` — تفكيك `StatusChangeModal` — 2026-11-27

> تفكيك `frontend/src/components/StatusChangeModal.tsx` من **605** إلى **320**
> سطرًا + حقول الإجراء ونموذجها، بلا تغيير سلوكي.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---:|---:|
| `StatusChangeModal.tsx` | **605** | **320** |
| `StatusChangeTargetFields.tsx` | — | 265 |
| `statusChangeModel.ts` | — | 76 |
| `statusChangeModel.test.ts` | — | 27 |

الحارس: **7 ⇒ 6** ملفات مجمّدة.

## 2. التوزيع

| الوحدة | المحتوى |
|---|---|
| `statusChangeModel` | `StatusFields`، `emptyFields`، مفاتيح المبالغ/العملات، `DATE_PLACEHOLDER` |
| `StatusChangeTargetFields` | حقول الإجراء المختار (تريث/براءة/جبري/تحويل بدل/شطب/سير/إحالة/عودة) |
| `StatusChangeModal` | الحالة/الأهداف، بناء الحمولة، الإرسال، تحذير الرقم، النافذة |

## 3. ما نُقل حرفيًا

- شروط كل إجراء ونصوصه و`onChange` و`MultiAmountEditor` (بما فيها المزاد والتحذير).
- `buildPayload` والتحقق و`normalize` ومفتاح التزامن وتحذير إعادة الرقم — بقيت في النافذة.
- لا تغيير في `StatusFields` (30 حقلًا) ولا في العملات الافتراضية.

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `StatusChangeModal.test.tsx` (بلا تعديل) | **25/25** |
| `statusChangeModel.test.ts` جديد | **2/2** (الحالة الابتدائية والعملات + محاذاة المفاتيح) |

## 5. البوابات

`tsc`/`oxlint` نظيفان، و`module-size-guard` نظيف مع 6 ملفات مجمّدة. البوابات
الكاملة تُشغَّل قبل الـPR (الحزمة المتوقعة 1906/182).

## 6. الهجرات

**لا هجرة.**
