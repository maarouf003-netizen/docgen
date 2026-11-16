# P2-02e — إصلاح دلالة أخطاء صلاحيات الإنابة (400 → 403/404) — 2026-11-16

> نطاق هذه الجولة: إصلاح الثغرة التي كشفها `P2-02d` — فشل الصلاحية في مسارات كتابة
> الإنابة كان يُعيد `400` بدل `403`، والإنابة غير الموجودة `400` بدل `404`.

## 1. الثغرة والسبب

- `EnsurePendingByOwner` كان يرمي `ArgumentException` **لفحص الملكية**، و
  `GlobalExceptionHandler` يعيّن `ArgumentException` إلى `400`.
- `CreateAsync` كان يرمي `ArgumentException` لملكية المصدر ولغياب الملف المنيب.
- `UpdateAsync`/`DeleteAsync` كانا يرميان `ArgumentException("الإنابة غير موجودة")` (‏`400`).
- النتيجة: عميل الواجهة لا يميّز «غير مصرّح» من «مدخلات خاطئة»، وقد تظهر رسالة 400
  بدل إخفاء/رفض صلاحية.

## 2. الإصلاح

| الموضع | قبل | بعد |
|---|---|---|
| `CreateAsync` — مصدر مفقود | `ArgumentException` → 400 | `KeyNotFoundException` → 404 |
| `CreateAsync` — ملكية المصدر | `ArgumentException` → 400 | `UnauthorizedAccessException` → 403 |
| `UpdateAsync`/`DeleteAsync` — إنابة مفقودة | `ArgumentException` → 400 | `KeyNotFoundException` → 404 |
| `EnsurePendingByOwner` — الملكية | `ArgumentException` → 400 (بعد فحص الحالة) | `UnauthorizedAccessException` → 403 **(قبل فحص الحالة)** |
| `EnsurePendingByOwner` — حالة غير معلّقة | `ArgumentException` → 400 | كما هو (`400`: خطأ حالة عمل لا صلاحية) |

- **ترتيب الفحص** صار الملكية أولًا: لا يكشف حالة إنابة الغير برسالة 400، ويُعيد 403 دائمًا
  لمن لا يملك.
- الرسائل العربية بقيت كما هي (اختبارات تحتوي "لا تملكه"/"غير موجودة" ما زالت صالحة).

## 3. تحديثات الاختبارات (توقعات كانت تُثبّت السلوك الخاطئ)

- `DocumentDelegationServiceTests`: `Create_NotOwner_Throws` و`Update_NotOwner_Throws`
  → `UnauthorizedAccessException`؛ و`SoftDeletedSource_…` (Update على مصدر محذوف منطقيًا)
  → `KeyNotFoundException` (المورد المخفي = 404).
- `DelegationsIntegrationTests`: `Create_ByLawyerNotOwner_Forbidden` (كان `_BadRequest`).
- `RF003AuthzProofTests.T1c`: تعديل/حذف إنابة الغير → `Forbidden` (كان `BadRequest`).
- `DelegationScopeMatrixTests`: أُضيفت خلايا الكتابة — تعديل/حذف الغير `403` (في
  الاتجاهين)، وتعديل/حذف معرّف غير موجود `404`، وتأكيد بقاء الإنابة للمالك.

## 4. التحقق

- `DocumentDelegation` tests + `RF003` + `DelegationScopeMatrix`: خضراء.
- بناء `-warnaserror` والمجموعتان الكاملتان يُعادان قبل الدمج.
- ملاحظة محلية: تعذّر مؤقتًا الوصول إلى `api.nuget.org` فأصدر NuGet تحذير `NU1900`
  (تدقيق ثغرات الحزم) ورُقّي إلى خطأ محليًا؛ استُثني صراحةً أثناء التحقق المحلي فقط
  (`-p:WarningsNotAsErrors=NU1900`) — وهو تحذير شبكة لا علاقة له بالكود، و`CI` يحميه
  فصل خطوة `restore` (بلا `-warnaserror`) عن البناء (`--no-restore`).

## 5. حدود

- بقية مسارات الإنابة (Assign/Redirect/Reject/Register/Complete) ما تزال تستخدم
  `ArgumentException` لأخطاء الحالة والصلاحية معًا؛ توحيدها بند لاحق عند لمسها (المبدأ:
  الصلاحية 403، الحالة/المدخل 400، المفقود 404).
- لا تغيير في العقود الناجحة ولا الهجرات.
