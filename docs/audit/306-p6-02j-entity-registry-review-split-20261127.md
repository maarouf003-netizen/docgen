# `P6-02j` — تفكيك `EntityRegistryReviewManagement` — 2026-11-27

> تفكيك `frontend/src/pages/EntityRegistryReviewManagement.tsx` من **642** إلى **46**
> سطرًا: تبويبات، وتبويب التعديل، وتبويب الإضافة، وخطّاف تأجيل البحث — بلا تغيير
> سلوكي/عقود/هجرات.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---|---:|
| `pages/EntityRegistryReviewManagement.tsx` | **642** | **46** |
| `pages/entityRegistryReviewManagement/EditEntityTab.tsx` | — | 319 |
| `pages/entityRegistryReviewManagement/AddEntityTab.tsx` | — | 202 |
| `pages/entityRegistryReviewManagement/ReviewTabs.tsx` | — | 53 |
| `pages/entityRegistryReviewManagement/reviewTabsModel.ts` | — | 7 |
| `hooks/useDebouncedCallback.ts` | — | 33 |
| `hooks/useDebouncedCallback.test.ts` | — | 47 |

كلها < 600، وحُذف `EntityRegistryReviewManagement` من قائمة الحارس: **4 ⇒ 3** ملفات مجمّدة.

## 2. التوزيع

| الوحدة | المسؤولية |
|---|---|
| `reviewTabsModel` | نوع `TabId` وحلّ `?tab=` (الافتراضي «تعديل») |
| `ReviewTabs` | زر التبويب بحالتي `aria-selected` والمعطّل المؤجل |
| `EditEntityTab` | البحث المؤجّل، نتائج البحث، القائمة المختارة، قائمة الأفعال، ونوافذ التسمية/الدمج/الحلول |
| `AddEntityTab` | نموذج إنشاء قيد جهة أم (اسم/نوع/صيادة/محافظة/فرع/أسماء بديلة/تغطية) |
| `useDebouncedCallback` | تأجيل نداء البحث 300ms مع تنظيف المؤقّت وثبات الهوية |
| الصفحة | تركيب الترويسة والتبويبات واختيار المحتوى |

## 3. ما نُقل حرفيًا

- `doSearch` وشروطها (`q.trim()`، `excludeIds` من المختار، `perPage=50`)، وتأجيل 300ms.
- شروط الأفعال ورسائلها (تسمية = 1، دمج ≥2، حلول ≥1) وفتح النافذة المناظرة.
- البحث/الإضافة/الإزالة/«مسح الكل» ووسم النجاح، وقائمة `role="menu"` عبر `createPortal`.
- نموذج الإضافة كاملًا: التحقق (اسم ثم محافظة)، الحمولة، وتصفير الحقول بعد النجاح.
- `?tab=` المدعوم (`add`/`log`/`unify`/`suggestions`) والافتراضي `edit`.
- حُذف نوع حالة ميت (`'waiting'`) لم يكن يُضبط ولا يُقرأ.

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `EntityRegistryReviewManagement.test.tsx` (بلا تعديل) | **11/11** |
| `EntityRegistryReview.test.tsx` (بلا تعديل) | **4/4** |
| `useDebouncedCallback.test.ts` جديد | **4/4** (آخر قيمة، ثبات الهوية، أحدث دالة، تنظيف الإزالة) |

## 5. البوابات (المقاسة)

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts` | نظيف |
| `tsc -b` | نظيف |
| `vitest run` | **1928/1928** في 185 ملفًا |
| `build`/`budget` | نجح · إجمالي **1748.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `generate:api` | نظيف / لا انحراف |
| `module-size-guard` | نظيف مع **3** ملفات مجمّدة |

## 6. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
