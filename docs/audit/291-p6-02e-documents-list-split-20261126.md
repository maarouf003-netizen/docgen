# `P6-02e` — تفكيك `DocumentsList` — 2026-11-26

> **النطاق:** استكمال دين حجم الوحدات بعد إغلاق نطاق `P6-02`: تفكيك
> `frontend/src/pages/DocumentsList.tsx` من **1060** سطرًا إلى قشرة **474** سطرًا
> + منطق في hook + مكوّنات عرض، بلا تغيير سلوكي/عقود/هجرات.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---:|---:|
| `pages/DocumentsList.tsx` | **1060** | **474** |
| `pages/documentsList/useDocumentsList.ts` | — | 402 |
| `pages/documentsList/DocumentsListDisplay.tsx` | — | 330 |

كلها < 600، وحُذف `DocumentsList` من قائمة الحارس: **9 ⇒ 8** ملفًا مجمّدًا.

## 2. التوزيع

| الوحدة | المسؤولية |
|---|---|
| `useDocumentsList` | البحث/الفلاتر، `URL`/الجلسة، ترقيم المؤشر، «تحميل المزيد»، تمييز آخر ملف، تصدير إكسل، الصلاحيات |
| `DocumentsListDisplay` | `AppealBadge`/`FileNumber`/`StatusBadge`/`FilterSelect`/`ColumnFilter`/`MoreMenu`/`ActionsCell` |
| `DocumentsList` | تركيب الترويسة وأدوات الفلترة والنتائج (جدول/بطاقات) والنافذة |

## 3. ما نُقل حرفيًا

- منطق `P3-05b` كاملًا: حفظ/استعادة سلسلة المؤشرات، حد 20 صفحة، شروط
  `matchesSavedFilters`، وحارس `restoreAttempted`.
- فلتر الحالة من `URL` (`statusTouched`/`persistedStatus`) ومزامنة الرجوع/التقدم.
- مصادر خيارات الفلاتر (السجل/الإحصاءات/الاحتياطي) وشروط الصلاحيات (`BQ-001`).
- تصدير إكسل ومعالجة أخطاء `Blob`، وتمييز «آخر ملف فُتح» (403/404 يمسح التركيز).
- **التوافق:** `FileNumber`/`AppealBadge` ما زالا يُصدَّران من `DocumentsList`
  (إعادة تصدير) كي لا ينكسر `AppealIntegration.test`.

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `DocumentsList.test.tsx` | **77/77** (76 قائمًا + اختبار `perPage=20` جديد) |
| `AppealIntegration.test.tsx` | **4/4** |
| اختبار جديد | يثبت عقد الترقيم `perPage=20` (كشفه الاختبار العدائي، `293`) |

## 5. البوابات

| البوابة | النتيجة |
|---|---|
| `oxlint`/`tsc` | نظيفان |
| `vitest` | **1904/1904** في 181 ملفًا |
| `build`/`budget` | نجح · إجمالي **1746.2kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `generate:api` | نظيف / لا انحراف |
| `module-size-guard` | نظيف مع **8** ملفات مجمّدة |

## 6. الهجرات

**لا هجرة.** لا تغيير خلفي.
