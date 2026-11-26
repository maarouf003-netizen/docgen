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
- Architecture 41/41 · Application 778 · Api 916 (محليًا؛ والاختبار الجديد `PostgresFact`
  **نُفّذ على `PostgreSQL 17` محلي مؤقت ونجح**: حماية/فك حماية + وجود صف في
  `DataProtectionKeys`). مرجعه الحاسم `backend-postgres` في `CI`.
- `migration-watchlist-guard`: 8 هجرات / 7 أسطر غير مشطوبة.
- `migration-safety-guard`: 8 هجرات، لا عمليات هدم.
- `dotnet format --verify-no-changes`: نظيف.

## 4. بعد النشر (تحقق مطلوب)

1. اختفاء تحذير الحاوية العابرة من سجل الإقلاع.
2. وجود صفوف في `DataProtectionKeys` بعد أول طلب يوقع/يتحقق من كوكي.
3. إعادة نشر/إعادة تشغيل ثم بقاء جلسة قائمة (اختبار يدوي بكوكي موجود).

## 5. حدود ومخاطر معلنة

- المفاتيح تُخزَّن **غير مشفّرة** داخل القاعدة (تحذير «No XML encryptor» قد يبقى)؛
  الحماية تعتمد على تشفير القاعدة/المزوّد وصلاحيات الوصول. حماية المفاتيح بشهادة
  (`ProtectKeysWithCertificate`) خيار لاحق يحتاج إدارة شهادات.
- `KeyPath` بديل للخوادم التي لا تريد تخزين المفاتيح في القاعدة؛ يجب أن يكون قرصًا
  دائمًا ومحميًا، وإلا عادت المشكلة.
- لا سياسة تدوير مفاتيح صريحة بعد؛ تدوير `Data Protection` يمكن أن يُبطل الجلسات
  القائمة ويحتاج نافذة.
