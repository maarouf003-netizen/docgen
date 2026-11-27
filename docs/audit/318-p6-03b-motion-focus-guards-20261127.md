# `P6-03b` — تقليل الحركة وحراس البؤرة/الحركة وتوسيع `axe`/375px — 2026-11-27

> دفعة `P6-03b`: تعميم احترام `prefers-reduced-motion`، وإصلاح كل حالة تركيز بلا
> بديل مرئي، وإضافة حارس ثابت جديد في `CI`، وتوسيع تدقيق `axe` وفحص `375px` من
> أربع شاشات إلى تسع — بلا تغيير سلوك وظيفي أو `API`/هجرات.

## 1. النتيجة

| العنصر | قبل | بعد |
|---|---:|---:|
| كتلة `prefers-reduced-motion` في `index.css` | لا | **نعم** (عامة لكل الحركات/الانتقالات) |
| حركات بلا `motion-safe:` | 3 | **0** |
| `outline-none` بلا بديل تركيز | 6 ملفات | **0** |
| حارس الحركة/البؤرة في `CI` | لا | **`a11y-guard.mjs` (17 حالة ذاتية)** |
| شاشات `axe` | 4 | **9** |
| شاشات فحص `375px` | 4 | **9** |

## 2. الحركة

- `src/index.css`: كتلة
  `@media (prefers-reduced-motion: reduce)` تُصفّر مدة الحركة/الانتقال و`scroll-behavior`
  لكل العناصر والزوائد، فتعمل حتى مع حركات لم تُوسم صراحة.
- وُسمت الحركات الجوهرية الثلاث بـ`motion-safe:` كي تُلغى أصلًا لا تُختصر فقط:
  `ForumPage` (هياكل التحميل)، `ReviewsList` (نبض «جديد»)،
  `ReviewStatusBadge` (نبض «بانتظار رد»).

## 3. البؤرة

أُضيف بديل تركيز مرئي (`focus-visible:ring-2`) حيث كان `outline-none` وحده:

| الملف | الموضع |
|---|---|
| `components/appeal/AppealRotationModal.tsx` | عنوان التأكيد القابل للتركيز برمجيًا |
| `components/form/applicantSide/ApplicantAssetsSection.tsx` | قائمتا المالك ونوع الحصة |
| `components/form/applicantSide/ApplicantGuarantorsSection.tsx` | قائمة نوع العنوان |
| `components/form/FieldInfoHint.tsx` | لوحتا التلميح (أُزيل `outline-none` فظهر مؤشر المتصفح) |
| `pages/BranchLawyers.tsx` | قائمة الفرع |
| `pages/UsersManagement.tsx` | قائمتا الدور والفرع (إنشاء وتعديل) |

## 4. الحارس `a11y-guard.mjs`

قواعد ثابتة (بلا حزم) على `frontend/src` + `index.css` + `index.html`:

1. `transition-all` / `transition: all` ممنوع.
2. أي رمز صنف يبدأ بـ`animate-` بلا `motion-safe:` مرفوض.
3. `outline-none` بلا `focus-visible:ring-*`/`focus:ring-*`/`focus-visible:outline-*`
   في النص نفسه مرفوض.
4. `type="date"` مرفوض (قاعدة حقول التواريخ النصية).
5. `user-scalable=no` و`maximum-scale=1` مرفوضان.
6. غياب كتلة `prefers-reduced-motion` من `index.css` مرفوض.

`--self-test` **17 حالة** (8 مصادر + 2 CSS + 3 HTML + 4 استثناءات قياس)، وخطوتان
في وظيفة `guards` بـ`CI`: الاختبار الذاتي ثم الفحص الفعلي.

## 5. توسيع `axe` و`375px`

أُضيفت الشاشات: `/forum`، `/stats`، `/branches/manage`، `/calendar` (محامٍ)،
`/suggestions` — في `e2e/a11y.spec.ts` و`e2e/mobile-overflow.spec.ts` بنفس عتبة
`critical`/`serious` وقائمة التبييض الفارغة. صار مجموع E2E **25 اختبارًا**
(16 سطح مكتب + 9 جوال).

**أُصلح بفعل البوابة (لا بتبييض):**

- `aria-expanded` كان موضوعًا على `<textarea>` (`#forum-composer`) وهو غير مدعوم على
  دور `textbox` (`aria-allowed-attr` — `critical`). أُزيل من `AutoResizeTextarea`
  وبقي `aria-controls` (مدعوم)، مع تأكيد في `MessageComposer.test`.
- تباين عداد المحارف `text-gray-400` على الأبيض (`color-contrast` — `serious`)
  رُفع إلى `text-gray-500`.

## 6. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `ReviewStatusBadge.test.tsx` جديد | **2/2** (نبض `motion-safe` فقط، ولا حركة عند «تم الرد») |
| `a11y-guard.mjs --self-test` | **17/17** |
| `vitest run` (الفرع المكدَّس) | **1936/1936** في 187 ملفًا |
| اختبارات الصفحات المتأثرة (Forum/Reviews/BranchLawyers/UsersManagement) | عبرت بلا تعديل |

## 7. البوابات

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts` | نظيف |
| `tsc -b` | نظيف |
| `build`/`budget` | نجح · إجمالي **1748.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `a11y-guard` / `module-size-guard` | نظيفة |
| `generate:api` | لا انحراف |
| **E2E (axe/375px الجديدة)** | **لم تُشغَّل محليًا**: لا PostgreSQL ولا Docker على جهاز التنفيذ؛ تُثبتها وظيفة `e2e` الحاجبة في `CI` |

## 8. المتبقي في `P6-03` (معلن)

- قياس تباين الألوان آليًا.
- توكن/مكوّن `focus` موحّد بدل تكرار أصناف Tailwind.
- تدقيق بقية الشاشات (نموذج/عرض الملف، الإدارة التفصيلية…) وحركات داخل المكونات
  غير المفحوصة.

## 9. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
