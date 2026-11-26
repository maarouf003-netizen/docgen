# `P6-02m` — تفكيك `BranchesManagement` — 2026-11-27

> تفكيك `frontend/src/pages/BranchesManagement.tsx` من **870** إلى **113** سطرًا:
> خطّاف قيادة، وجدول/بطاقات، ونافذة تعديل، ولوحة الشعب/النقل/التعاقب — بلا تغيير
> سلوكي/عقود/هجرات. **بهذا تُغلق `DoD` بند `P6-02` («لا ملف >600 سطر»).**

## 1. النتيجة

| الملف | قبل | بعد |
|---|---|---:|
| `pages/BranchesManagement.tsx` | **870** | **113** |
| `pages/branchesManagement/useBranchesManagement.ts` | — | 400 |
| `pages/branchesManagement/BranchSectionsPanel.tsx` | — | 232 |
| `pages/branchesManagement/BranchEditModal.tsx` | — | 140 |
| `pages/branchesManagement/BranchesTable.tsx` | — | 106 |
| `pages/branchesManagement/branchesManagementModel.ts` | — | 23 |

كلها < 600، وحُذف `BranchesManagement` من قائمة الحارس: **1 ⇒ 0**، فصارت
`FROZEN_MAX_LINES` فارغة — أي أن أي ملف جديد >600 يُفشل الحارس بلا استثناءات.

## 2. التوزيع

| الوحدة | المسؤولية |
|---|---|
| `useBranchesManagement` | قائمة الفروع، إنشاء/تعديل/حذف، تحميل الشعب/الدوائر/التعاقب، الإنشاء/التسمية/التفعيل/الحذف، ونقل الدائرة |
| `BranchesTable` | جدول المكتب وبطاقات الجوال والحالة والاستخدام (نمط `DocumentsList`) |
| `BranchEditModal` | نافذة التعديل وتأكيد الحذف في المكان نفسه |
| `BranchSectionsPanel` | تبويبات الشعب/نقل الدائرة/سجل التعاقب لكل فرع أب |
| `branchesManagementModel` | تسميات أحداث التعاقب وتحقق الحقول المشترك |

## 3. ما نُقل حرفيًا

- `useCancellableRequest('/branches')` وحالة التحميل/الخطأ/إعادة الجلب.
- تحقق الاسم ← الكود ← المحافظة برسائله الحرفية، وحمولتا `POST`/`PUT` (بما فيها
  `address`/`phone` → `null` عند الفراغ و`version` في التعديل).
- كل مسارات الشعب (`/sections`: إنشاء/تسمية/تفعيل/حذف) والدوائر (`/execution-circuits/.../transfer`)
  والتعاقب (`/head-succession`)، ورسائل الخطأ والمنع (شعبة بدوائر لا تُحذف).
- `window.confirm` لحذف الشعبة، وتأكيد الحذف داخل نافذة الفرع.
- كل التسميات والمعرّفات التي تستهدفها اختبارات الصفحة (`branch-*`/`edit-branch-*`/`sub-branch`…).

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `BranchesManagement.test.tsx` (بلا تعديل) | **13/13** |
| `BranchLawyers.test.tsx` (بلا تعديل) | **مشمول في الحزمة الكاملة** |

## 5. البوابات (المقاسة على الفرع المكدَّس)

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts` | نظيف |
| `tsc -b` | نظيف |
| `vitest run` | **1934/1934** في 186 ملفًا |
| `build`/`budget` | نجح · إجمالي **1748.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `generate:api` | نظيف / لا انحراف |
| `module-size-guard` | نظيف مع **0** ملفات مجمّدة |

## 6. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
