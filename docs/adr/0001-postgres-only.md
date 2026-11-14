# ADR-0001 — PostgreSQL فقط في التشغيل

- **الحالة:** معتمد (P0-01).
- **التاريخ:** 2026-11-14.
- **السياق:** `docs/PERFECTION_ROADMAP.md` §1.2 قرار 1، §4 `P0-01`.
- **يحل محل:** المسار الإنتاجي الثنائي `SQLite`/`PostgreSQL` و`Database:AllowSqliteInProduction`.

## 1. السياق

كان التطبيق يسجّل مزوّد قاعدة البيانات في `DependencyInjection.AddInfrastructure` حسب راية
`Database:UsePostgres`، ويسمح بالإقلاع على `SQLite` خارج `Development` عبر استثناء صريح
(`Database:AllowSqliteInProduction`). نتج عن ذلك:

- مساران للمخطط والهجرات (`Persistence/Migrations` و`Persistence/MigrationsPostgres`) واحتمال
  انحراف صامت بينهما.
- خطر وقوع صامت على `SQLite` عند نسيان ضبط `Postgres` في النشر.
- اختلافات سلوكية بين بيئة الاختبار (`SQLite`) وبيئة التشغيل (`PostgreSQL`): أنواع الوقت،
  الفهارس الجزئية، وقيود القاعدة.

القاعدة الفعلية الوحيدة في التشغيل هي `PostgreSQL` (نسخة تجريبية على Render/Supabase). لا توجد
حاجة تشغيلية لمسار `SQLite` إنتاجي.

## 2. القرار

1. **`PostgreSQL` هو المزوّد الوحيد** الذي يسجّله `AddInfrastructure`؛ لا فرع `UseSqlite` في `src`.
2. **سلسلة اتصال `PostgreSQL` إلزامية** عند الإقلاع: من `DATABASE_URL` (بصيغة `postgres://`) أو
   `ConnectionStrings:DefaultConnection` (بصيغة Npgsql الكلامية). لا قيمة افتراضية لـ`SQLite`.
3. **حذف `Database:AllowSqliteInProduction`** و`Database:UsePostgres` من الإعدادات والكود:
   وجود سلسلة اتصال صالحة لـ`Npgsql` هو الإشارة الوحيدة للمزوّد.
4. **النموذج يُوحَّد على صياغة `PostgreSQL` في `P0-02`** مع سحق الهجرات وحذف هجرات `SQLite`.
   يبقى فرع `ConfigureHeadAlertProviderSpecific` في `P0-01` مؤقتًا لأن هجرات `SQLite` ما زالت
   موجودة للاختبارات؛ وإزالته جزء من P0-02 لا من تغيير المزوّد التشغيلي.
5. **`SQLite` يبقى مؤقتًا في الاختبارات فقط** (استبدال السياق داخل نطاق الاختبار) حتى ينتهي
   نقل الاختبارات التكاملية إلى `PostgreSQL` في `P1-01` وإزالته نهائيًا في `P1-08`.

## 3. ما يتغير في التشغيل

| قبل | بعد |
|---|---|
| `Database:UsePostgres=true/false` يختار المزوّد | لا راية؛ `PostgreSQL` دائمًا |
| `AllowSqliteInProduction=true` يسمح بإقلاع إنتاجي على `SQLite` | يُرفض الإقلاع بلا سلسلة `PostgreSQL` صالحة |
| القيمة الافتراضية `Data Source=docgen.db` | لا قيمة افتراضية؛ الإعداد إلزامي |
| تسجيل `UseNpgsql` أو `UseSqlite` حسب الراية | `UseNpgsql` فقط |

رسائل الرفض تسمّي المفتاح المطلوب (`DATABASE_URL` أو `ConnectionStrings__DefaultConnection`) ولا
تطبع سلسلة الاتصال كاملة (تُطبع بصمة وصفية فقط).

## 4. الأثر على الاختبارات

- الاختبارات التي تُقلع `Program` كانت تعتمد على `SQLite` عبر `AllowSqliteInProduction`. صار
  المضيف الاختباري يمرّر سلسلة `PostgreSQL` شكلية (بلا اتصال) لتجاوز حارس الإقلاع، ثم يستبدل
  تسجيل `DocGeneratorDbContext` بسياق `SQLite` داخل `ConfigureTestServices`. هذا **حيز اختبار
  فقط**، وليس مسارًا إنتاجيًا.
- اختبارات الحارس تثبت أن الإقلاع يفشل بلا سلسلة `PostgreSQL`، وأن سلسلة `SQLite` القديمة
  (`Data Source=...`) تفشل أيضًا.
- يبقى `DatabaseInitializer` متوافقًا مع `SQLite` في مسار الاختبار حتى `P0-02` (حيث تُحذف هجرات
  `SQLite` وتُستبدل بـ`EnsureCreated` مؤقتًا)، ثم يُزال التعامل الخاص في `P1-08`.

## 5. خطة الهجرة

1. `P0-01` (هذا الـADR): إزالة المسار الإنتاجي وسجلات الإعدادات، وتحديث الاختبارات وCI.
2. `P0-02`: أخذ نسخة `pg_dump` وجرد أعداد الجداول، ثم سحق هجرات `PostgreSQL` إلى
   `InitialPostgresBaseline` واحد، وحذف هجرات `SQLite` نهائيًا، وإعادة ضبط `__EFMigrationsHistory`.
3. `P1-01`: منصة اختبار `PostgreSQL` حقيقية (Testcontainers/CI + Respawn).
4. `P1-08`: إزالة حزمة `Microsoft.EntityFrameworkCore.Sqlite` وكل `UseSqlite` من الاختبارات.

## 6. التراجع

- التراجع الكامل عن هذا القرار يتطلب إعادة فرع `UseSqlite` وراية `AllowSqliteInProduction` من
  Git، وهذا مرفوض معماريًا بعد `P0-02`.
- التراجع التشغيلي الآمن هو استعادة نسخة `pg_dump` السابقة، لا العودة إلى `SQLite`.
- إن تعذّر الإقلاع بسبب إعداد ناقص، فالخطأ مقصود ومفصح: اضبط سلسلة `PostgreSQL` ثم أعد التشغيل.

## 7. المراجع

- `docs/PERFECTION_ROADMAP.md` — `P0-01`/`P0-02`/`P1-01`/`P1-08`.
- `RUN_GUIDE.md` §9 — إعداد النشر وتطبيق الهجرات.
- `backend/src/DocGenerator.Api/Program.cs` — حارس سلسلة الاتصال.
- `backend/src/DocGenerator.Infrastructure/DependencyInjection.cs` — تسجيل المزوّد.
