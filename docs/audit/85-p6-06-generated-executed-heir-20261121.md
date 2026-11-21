# `P6-06` (الجزء الرابع) — إحالة `ExecutedHeirDto` — 2026-11-21

> **النطاق:** إحالة `ExecutedHeirDto` اليدوي في `frontend/src/types/document.ts` إلى
> `components['schemas']['ExecutedHeirDto']`، وتوسيع مساعد خيارات الاستئناف ليقبل
> `string | null`. **لا تغيير في `API`/`DTO`/`backend`.**
> **PR:** [#246](https://github.com/maarouf003-netizen/docgen/pull/246).

## 1. ما نُفِّذ

1. `frontend/src/types/document.ts`: حُذف تعريف `ExecutedHeirDto` اليدوي (13 حقلًا)
   وصار `export type ExecutedHeirDto = components['schemas']['ExecutedHeirDto'];`.
2. `frontend/src/components/appeal/appealOptions.ts`: توسيع `heirOption`
   (`id`, `heirName`, `heirFather`, `heirFamily`) إلى `null | undefined`، لأن النوع
   المولَّد يصدّر الحقول النصية `string | null`.

## 2. المراجعة النقدية

- **`tsc -b` كشف خطأين فقط** (نفس الموضع في `appealOptions` عبر مسارين: طالبو التنفيذ
  والأشخاص الطبيعيون). عولجا بتوسيع توقيع `heirOption` مرة واحدة، **بلا `as`/`any`**.
- **لا تغيير سلوكي:** `triple()` يقبل `null | undefined` أصلًا ويستخدم `p?.trim()`؛
  و`h.id ?? 0` موجودة مسبقًا. لم تتغير قوائم الأطراف أو نصوصها.
- لا تعريف يدوي مكرر لـ`ExecutedHeirDto` بعد الآن.
- `RepresentativeEditor`/`HeirsEditors`/`viewFormat` جاهزة من الجزء الثالث (null-safe)
  فلم تحتج تعديلًا هنا — أثر تراكمي صحيح لإصلاح الجذر.

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `npx oxlint src` | 0 |
| `npx tsc -b` | 0 |
| `npx vitest run` | **1808/1808** |
| `npm run build` | ناجح |
| الخلفية | لم تُلمس؛ لا هجرة جديدة (القائمة المعلّقة 6) |

## 4. الحدود

- بقي نحو 750 سطرًا من الأنواع اليدوية (`DocumentResponse`, `AssetDto`, `GuarantorDto`,
  `ExecutionApplicantDto` — الأخير يحمل `nature` كاتحاد يدوي ويحتاج قرارًا واعيًا).
- `P6-06` ما زال **جزئيًا** بقرار الخريطة.
