# حزمة تسليم النشر — DocGenerator

> **الغرض:** تمكين المهندس المسؤول عن النشر من تركيب النسخة والتحقق منها دون معرفة مسبقة بالمشروع.
> **الحالة:** `W3a` مُتحقق على قاعدة اختبار (`docs/audit/337-w3a-supabase-20261128.md`)؛
> `W3b` (الخادم النهائي) يُنفَّذ بهذه القائمة ويُوقَّع عند الإنجاز.
> **لا أسرار في هذا الملف:** كل القيم السرية تُضبط في مدير أسرار الخادم.

## 1. ما يحتاج المهندس معرفته قبل البدء (يُعبَّأ عند التسليم)

| البند | القيمة |
|---|---|
| إصدار `PostgreSQL` | … (نوصي 15+؛ يفضَّل 16/17) |
| توفر امتداد `pg_trgm` | … (إلزامي لميزة البحث؛ `CREATE EXTENSION pg_trgm`) |
| طريقة التشغيل | `Dockerfile` الجاهز / `dotnet publish` + خدمة نظام |
| اسم النطاق العام | … |
| بروكسي عكسي؟ وهل يُنهي `TLS`؟ | … (`KnownProxies` أو `Security__TlsTerminatedByApp=true`) |
| النسخ الاحتياطي | لقطات/`PITR` من المزوّد / `pg_dump` مجدول |
| مسؤول الأسرار | … |
| مسؤول الأرشيف (استرجاع سجلات التدقيق) | … |

## 2. المتطلبات المسبقة

- `PostgreSQL` يعمل، وامتداد `pg_trgm` متاح.
- `.NET SDK 10` (أو صورة `Docker` فقط) و`Node 24` إن بُني الواجهة محليًا (الـ`Dockerfile` يبنيها داخليًا).
- أدوات `dotnet-ef 10.0.10` على جهاز النشر أو في الصورة.
- متغيرات البيئة (أسماء فقط، القيم من مدير الأسرار):
  `DATABASE_URL`، `Jwt__Secret` (≥32 بايت)، `Bootstrap__AdminPassword`،
  `AllowedHosts` (النطاق الفعلي + عنوان فحص الصحة)،
  `Security__HstsEnabled=true`، `Security__HttpsRedirectionEnabled=true`،
  `Security__CspReportOnly=false`، `Swagger__Enabled=false`،
  `Database__AutoMigrate=false`، `DataProtection__PersistToDatabase=true`،
  و`Security__KnownProxies__0` **أو** `Security__TlsTerminatedByApp=true`.

## 3. قبل النشر — الهجرات (إلزامي)

```powershell
# سلسلة الاتصال من مدير الأسرار، لا تُكتب في المستودع
$env:POSTGRES_CONNECTION = '<Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require>'

dotnet ef database update --context DocGeneratorPostgresDbContext `
  --project backend/src/DocGenerator.Infrastructure `
  --startup-project backend/src/DocGenerator.Api
```

- المتوقع: **9 هجرات** في `__EFMigrationsHistory`.
- الهجرة الأخيرة المعلّقة سابقًا: `20261127105840_W1_DepositExecutedBackfill` (بيانات فقط:
  «سبق تنفيذ الإيداع»؛ متكررة وآمنة).
- **لا تُشغّل** `--context DocGeneratorDbContext` (لا هجرات له).
- إن كان `Database:AutoMigrate=false` (الإنتاج) فالتطبيق **يرفض الإقلاع** إن بقيت هجرة ناقصة — هذا مقصود.

## 4. البناء والتشغيل

**الخيار أ — `Docker`:**
```bash
docker build -t docgen:<version> .
docker run -d --name docgen -p 8080:8080 --env-file /secure/docgen.env docgen:<version>
```

**الخيار ب — `dotnet publish`:**
```powershell
dotnet publish backend/src/DocGenerator.Api/DocGenerator.Api.csproj -c Release -o <path>
# ثم خدمة نظام تشغّل: dotnet DocGenerator.Api.dll --urls "http://0.0.0.0:8080"
```
الواجهة تُخدم من `wwwroot` داخل نفس العملية (لا حاجة لخادم ويب منفصل).

## 5. التحقق بعد النشر

1. `GET /healthz` ⇒ `Healthy` (العملية حيّة).
2. `GET /readyz` ⇒ `200` (قاعدة البيانات + صندوق التنبيهات).
3. `/healthz/outbox` ⇒ سليم.
4. دخول بحساب إداري، ثم: إنشاء ملف، قائمة الملفات، الإحصاءات، بوابة الجهة، تنبيه.
5. التأكد من بقاء الجلسة/الحماية بعد إعادة تشغيل الخدمة (مفاتيح `Data Protection` في القاعدة).

## 6. النسخ الاحتياطي والاستعادة (مرجع مجرَّب)

```bash
# نسخ
pg_dump --format=custom --no-owner --no-privileges --file=docgen-<date>.dump "$DATABASE_URL"

# استعادة إلى قاعدة اختبار (ليس الإنتاج)
createdb docgen_restore_check
psql -d docgen_restore_check -c "CREATE EXTENSION IF NOT EXISTS pg_trgm;"
pg_restore --no-owner --no-privileges -d docgen_restore_check docgen-<date>.dump
# تحقق: عدد الجداول والهجرات والمستخدمين، ثم:
dropdb docgen_restore_check
```

**ملاحظات مجرَّبة:** تفريغ `--schema=public` وحده لا يحمل `CREATE EXTENSION pg_trgm`؛ أنشئه قبل
الاستعادة. الاستعادة إلى قاعدة جديدة تعارض `CREATE SCHEMA public;`؛ احذف المخطط قبل الاستعادة أو
استخدم قائمة `TOC` مفلترة.

## 7. التراجع (Rollback)

1. أوقف الخدمة الجديدة وأعد الصورة/النسخة السابقة.
2. إن فشل النشر بعد الهجرات: **لا تعكس الهجرات تلقائيًا**؛ استعد نسخة ما قبل النشر إن لزم.
3. الهجرات الحالية **إضافية/بيانات فقط** (لا حذف أعمدة/جداول)؛ `W1_DepositExecutedBackfill`
   متكررة وآمنة.
4. أعد تشغيل النسخة السابقة وتحقق من `/readyz` قبل إعلان التراجع.

## 8. الأمان والتشغيل

- لا أسرار في المستودع أو الصور أو السجلات. `DATABASE_URL` وغيره من مدير الأسرار.
- **دوّر** مفاتيح بيئة الاختبار الحالية بعد إغلاق `W3a` (مفتاح `Render` وكلمة مرور قاعدة الاختبار).
- `AllowedHosts` صريح، و`HSTS`/إعادة توجيه HTTPS، و`CSP` مفروض (`CspReportOnly=false`).
- سجل التدقيق: أرشفة شهرية بعد 24 شهرًا إلى مجلد **مشفّر** خارج مجلد الموقع + بصمة تحقق،
  وحذف بعد 5 سنوات بعد «تجربة جافة». مفتاح التشفير في مدير أسرار الخادم ونسخة مقفلة عند المسؤول.
- الأرشيف ضمن النسخ الاحتياطي الدوري.

## 9. قائمة قبول `W3b` (يوقّعها المهندس)

- [ ] `__EFMigrationsHistory` = 9، و`AutoMigrate=false` يُقلع بنجاح.
- [ ] `/readyz` = 200 و`/healthz/outbox` سليم.
- [ ] `DataProtectionKeys` تبقى بعد إعادة التشغيل.
- [ ] دخان وظيفي (ملف/قائمة/إحصاءات/بوابة/تنبيه).
- [ ] نسخة احتياطية مأخوذة، واستعادة تجريبية ناجحة، وحذف قاعدة الاختبار.
- [ ] `TLS`/`HSTS`/`CSP`/`AllowedHosts` على النطاق الحقيقي.
- [ ] الأسرار في مدير الأسرار، ومفاتيح الاختبار مدوّرة.
- [ ] أرشيف التدقيق مجهّز (مجلد/تشفير/بصمات) ومدرج في النسخ.

**مرجع إضافي:** `RUN_GUIDE.md` §9 (تطبيق الهجرات) و`docs/audit/337-w3a-supabase-20261128.md`
(نتائج التجربة الفعلية).
