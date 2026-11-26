# مفاتيح `Data Protection` دائمة — 2026-11-26

> **النطاق:** إصلاح تحذير الإنتاج «Storing keys in a directory that may not be persisted
> outside of the container»: كانت مفاتيح الحماية تُخزَّن في حاوية عابرة، فتُفقد الجلسات
> (وكوكي CSRF/الحماية) مع كل نشر أو إعادة تشغيل. الحل: تخزينها في `PostgreSQL`
> (جدول `DataProtectionKeys`) في الإنتاج، مع بديل مسار ملف دائم.
>
> **الأثر:** لا تغيير في واجهات/عقود/سلوك العمل؛ فقط بقاء الجلسات بعد النشر.

## 1. التغيير

| الملف | التغيير |
|---|---|
| `backend/Directory.Packages.props` + `DocGenerator.Infrastructure.csproj` | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` **10.0.10**، وتثبيت صريح لـ`System.Security.Cryptography.Xml` **10.0.15** (10.0.10 يحمل ثغرات `NU1903` عالية). |
| `DocGeneratorPostgresDbContext` | تطبيق `IDataProtectionKeyContext` و`DbSet<DataProtectionKey> DataProtectionKeys` + جدول `DataProtectionKeys`. |
| `MigrationsPostgres/20261126111327_AddDataProtectionKeys` | جدول إضافي: `Id` identity، `FriendlyName`، `Xml`. آمن/غير كاسر. |
| `Program.cs` | `AddDataProtection().SetApplicationName("DocGen")`؛ `PersistToDatabase=true` ⇒ `PersistKeysToDbContext`؛ وإلا `KeyPath` ⇒ `PersistKeysToFileSystem`. |
| `appsettings.json` | `DataProtection:PersistToDatabase=false`, `KeyPath=""` (التطوير/الاختبار). |
| `appsettings.Production.json` | `PersistToDatabase=true` (الإنتاج). |
| `appsettings.Development.json` | `PersistToDatabase=false` صراحةً (الاختبارات لا تلمس القاعدة لهذا الغرض). |
| `DataProtectionPersistenceTests` | اختبار `PostgresFact`: يحمي/يفك نصًا ويتأكد من وجود صف في `DataProtectionKeys`. |

## 2. القاعدة التجريبية

- طُبّقت الهجرة على `Supabase` (تطبيق إنتاجي تجريبي) **قبل** الدمج:
  `20261126111327_AddDataProtectionKeys`.
- التحقق: `__EFMigrationsHistory` = **8** صفوف، **52** جدولًا، وأعمدة الجدول الثلاثة موجودة.
- التطبيق الحالي (القديم) لا يعرف الجدول؛ إضافته لا تؤثر عليه.

## 3. التحقق

- `dotnet build -warnaserror` = 0/0.
- `has-pending-model-changes` = «No changes».
- Architecture 41/41 · Application 778 · Api **1579/1579** على `PostgreSQL 17` محلي
  (بعد إصلاح `PostgresBaselineTests` وتشغيل الحزمة الكاملة بلا تخطٍّ)، والاختبار الجديد
  `DataProtectionPersistenceTests` ضمنها. مرجعه الحاسم `backend-postgres` في `CI`.
- `migration-watchlist-guard`: 8 هجرات / 7 أسطر غير مشطوبة.
- `migration-safety-guard`: 8 هجرات، لا عمليات هدم.
- `dotnet format --verify-no-changes`: نظيف.
- **تمرين نسخ/استعادة بعد الإضافة:** نسخة `docgen-20261126T122116Z.dump` (8 هجرات)،
  استُعيدت على `PostgreSQL 17` عادي: **52** جدولًا، **8** هجرات، **0** صفوف في
  `DataProtectionKeys` (لا مفاتيح في الأرشيف)، و4 مشغّلات. نجح الفحص الدخاني.
- **عيب كشفته المراجعة النقدية:** `--exclude-table-data=DataProtectionKeys` غير المنصّص
  لا يطابق الاسم مختلط الحالة (يطويه `pg_dump` إلى أحرف صغيرة)، فكانت البيانات ستُنسخ.
  صُحّح إلى `public."DataProtectionKeys"` مع حارس يمنع الرجوع، وأُعيد التمرين.

## 4. بعد النشر (تحقق مطلوب)

1. اختفاء تحذير الحاوية العابرة من سجل الإقلاع.
2. وجود صفوف في `DataProtectionKeys` بعد أول طلب يوقع/يتحقق من كوكي.
3. إعادة نشر/إعادة تشغيل ثم بقاء جلسة قائمة (اختبار يدوي بكوكي موجود).

## 5. حدود ومخاطر معلنة

- المفاتيح تُخزَّن **غير مشفّرة** داخل القاعدة (تحذير «No XML encryptor» قد يبقى)؛
  الحماية تعتمد على تشفير القاعدة/المزوّد وصلاحيات الوصول. حماية المفاتيح بشهادة
  (`ProtectKeysWithCertificate`) خيار لاحق يحتاج إدارة شهادات.
- **النسخ الاحتياطي:** بيانات `DataProtectionKeys` **مستثناة** من أرشيف `pg_dump`
  (`--exclude-table-data`)، فلا تحمل النسخة مفاتيح تزوير جلسات. بعد الاستعادة يُنشئ
  التطبيق حلقة جديدة (تُبطل الجلسات القائمة، وهذا مقبول في DR).
- `KeyPath` بديل للخوادم التي لا تريد تخزين المفاتيح في القاعدة؛ يجب أن يكون قرصًا
  دائمًا ومحميًا، وإلا عادت المشكلة.
- لا سياسة تدوير مفاتيح صريحة بعد؛ تدوير `Data Protection` يمكن أن يُبطل الجلسات
  القائمة ويحتاج نافذة.
