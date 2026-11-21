# `P6-06` (الجزء الخامس) — إحالة `ExecutedNaturalPersonDto` — 2026-11-21

> **النطاق:** إحالة `ExecutedNaturalPersonDto` اليدوي في `frontend/src/types/document.ts`
> إلى `components['schemas']['ExecutedNaturalPersonDto']`.
> **لا تغيير في `API`/`DTO`/`backend`.**
> **PR:** [#247](https://github.com/maarouf003-netizen/docgen/pull/247).

## 1. ما نُفِّذ

- `types/document.ts`: حُذف تعريف `ExecutedNaturalPersonDto` اليدوي (17 حقلًا) وصار:
  `export type ExecutedNaturalPersonDto = components['schemas']['ExecutedNaturalPersonDto'];`
- لم تُحتَج أي تعديلات إضافية: مساعدات العرض/النماذج التي وُسِّعت في `P6-06/3`
  (`tripleName`, `PersonFields`, `heirRepresentativeDetail`, `RepresentativeFields`,
  `hasRepresentative`...) تقبل `null` أصلًا.

## 2. المراجعة النقدية

- **`tsc -b` لم يُظهر أي خطأ** بعد الإحالة — دليل أن إصلاح nullability في الجزء الثالث
  كان جذريًا لا ترقيعًا لكل نوع.
- **`vitest`:** التشغيل الأول المتزامن مع حمل النظام أظهر فشل حالة واحدة (لا علاقة لها
  بالأنواع؛ الأنواع تُحذف وقت التشغيل). إعادة التشغيل الكاملة مرّت **1808/1808**. لم يُخفَ
  الفشل؛ هذه ثالث مرة يظهر فيها تقلّب تحت الحمل الثقيل، ويُوثَّق هنا.
- **لا تغيير سلوكي:** نفس الحقول والقيم؛ `heirs` ما زال `ExecutedHeirDto[] | null`
  والمستهلكون يستخدمون `?? []`.

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `npx oxlint src` | 0 |
| `npx tsc -b` | 0 |
| `npx vitest run` (إعادة) | **1808/1808** |
| `npm run build` | ناجح |
| الخلفية | لم تُلمس؛ لا هجرة جديدة (المعلّقة 6) |

## 4. الحدود

- بقي `DocumentResponse`/`AssetDto`/`GuarantorDto`/`ExecutionApplicantDto`.
- `ExecutionApplicantDto` يحمل `nature?: PartyNature` (اتحاد يدوي)؛ إحالته تحتاج
  `mapApplicantNature` صريحًا أو إبقاء الحقل يدويًا — قرار مرحلي موثّق.
