# `P6-02l` — تفكيك `BranchManagementModal` — 2026-11-27

> تفكيك `frontend/src/components/entity/BranchManagementModal.tsx` من **834** إلى
> **122** سطرًا: قيادة الحالة والأفعال في خطّاف، واللوحات وبطاقة الجهة الأم في
> مكوّنات عرض — بلا تغيير سلوكي/عقود/هجرات.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---|---:|
| `components/entity/BranchManagementModal.tsx` | **834** | **122** |
| `components/entity/branchManagement/useBranchManagement.ts` | — | 389 |
| `components/entity/branchManagement/BranchManagementPanels.tsx` | — | 316 |
| `components/entity/branchManagement/ParentBranchCard.tsx` | — | 140 |
| `components/entity/branchManagement/branchManagementFormat.ts` | — | 2 |

كلها < 600، وحُذف `BranchManagementModal` من قائمة الحارس: **2 ⇒ 1** ملف مجمّد.

## 2. التوزيع

| الوحدة | المسؤولية |
|---|---|
| `useBranchManagement` | تحميل القيود والاقتراح المعلّق، المعاينة الموحّدة، تنفيذ التعديل/الدمج/الإلغاء/التوحيد، اقتراح الجهة الأم وسحبه |
| `BranchManagementPanels` | لوحات الأفعال الأربعة + لوحة المعاينة (ملخص/تحذيرات/قيود/إجمالي) |
| `ParentBranchCard` | بطاقة «الجهة الأم» للقراءة فقط ونموذج اقتراح تعديلها |
| `branchManagementFormat` | منسّق الأعداد العربي المشترك |
| `BranchManagementModal` | التركيب: الترويسة، التحميل/الخطأ/النجاح، الحالة الفارغة، قائمة الفروع، اللوحات، التذييل |

## 3. ما نُقل حرفيًا

- شرطا تحميل القيود والاقتراح (`groupId`/`pending`/`perPage=10`) والفشل الصامت للاقتراح.
- كل مسارات الأفعال: `branches/preview` ثم `rename-branch`/`merge`/`abolish`/`unify`،
  و`entries/:id/suggest-parent-edit` و`parent-edit-suggestions/:id/withdraw`.
- شروط التنفيذ: بلا أخطاء معاينة، ومعاينة للنوع نفسه، وعدم الانشغال.
- تحقق التوحيد (الاسم الحرفي) والناجي المصحّح، وتصفير الحالة بعد النجاح، واستدعاء
  `onCommitted` بالرسائل نفسها.
- استبعاد المصدر من هدف الدمج، واستبعاد الفرع نفسه من هدف الإلغاء، وإخفاء لوحة
  التوحيد عند أقل من فرعين.
- حُفظت كل التسميات والمعرّفات التي تستهدفها اختبارات النافذة (`bm-*`/`bmp-*`).

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `BranchManagementModal.test.tsx` (بلا تعديل) | **10/10** |
| `EntityRegistryReview.test.tsx` (بلا تعديل) | **4/4** |

## 5. البوابات (المقاسة على الفرع المكدَّس)

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts` | نظيف |
| `tsc -b` | نظيف |
| `vitest run` | **1934/1934** في 186 ملفًا |
| `build`/`budget` | نجح · إجمالي **1748.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `generate:api` | نظيف / لا انحراف |
| `module-size-guard` | نظيف مع **1** ملف مجمّد |

## 6. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
