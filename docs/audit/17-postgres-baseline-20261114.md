# P0-02 — سحق الهجرات إلى Baseline PostgreSQL واحد

- **التاريخ:** 2026-11-14.
- **المرجع:** `docs/PERFECTION_ROADMAP.md` §4 `P0-02`، و`docs/adr/0001-postgres-only.md`.
- **النطاق:** PostgreSQL فقط (ADR-0001)؛ لا هجرات SQLite في التشغيل.

## 1. النسخة الاحتياطية (قبل أي تغيير)

أُخذت نسخة منطقية من القاعدة التجريبية عبر `pg_dump` 17.12 (الخادم 17.11) بصيغة
`custom`، وحُفظت **خارج المستودع** في `C:\Users\pc\Desktop\docgen-backups`:

| العنصر | القيمة |
|---|---|
| ملف النسخة | `docgen_postgres_20261114_122106.dump` |
| الحجم | 593,594 بايت |
| SHA-256 | `4053E7F36A5076BFC92A61FDC6468C7C8CF98D0ED0932AA37AC55A336549A3DC` |
| عناصر الأرشيف (`pg_restore --list`) | 979 |
| هل استُعيدت؟ | نعم — على PostgreSQL محلي معزول (51 جدولًا) |
| جرد أعداد الجداول | `docgen_table_counts_20261114_122005.tsv` — 51 جدولًا، 1,619 صفًا |
| سجل الهجرات القديم | `docgen_migration_history_20261114_122048.txt` — 74 هجرة |

> محتوى `001.txt` لم يُطبع، ولم تُنسخ أي سلسلة اتصال إلى المستودع أو إلى هذا الملف.

## 2. الـBaseline

- **الهجرة:** `20261114101737_InitialPostgresBaseline`
  (`Persistence/MigrationsPostgres/`).
- **المصدر:** النموذج الحالي بعد توحيد صياغة `HeadAlert` على PostgreSQL وإضافة
  `CK_Documents_FileNumberRequiresCircuit` إلى `DocumentConfiguration`.
- **SQL:** 125,611 بايت، بلا `datetime2`، ويشمل:
  - 51 جدولًا + الفهارس والقيود والمفاتيح الأجنبية.
  - `UX_HeadAlerts_DedupeKey_Active` بصياغة `"IsArchived" = false`.
  - `CK_HeadAlerts_Kind_NotEmpty` و`CK_Documents_FileNumberRequiresCircuit`.
  - مشغّلات إلحاق-فقط لـ`AuditLogs` و`DocumentFieldChanges` (RF-019) عبر
    `fn_prevent_audit_mutation()`.
- **الهجرات القديمة:** حُذفت هجرات `PostgreSQL` و`SQLite` من الشجرة؛ سجلّها محفوظ في
  Git والنسخة الاحتياطية.

## 3. التحقق

1. **اختبار محلي (PostgreSQL 16):** `PostgresBaselineTests` — تطبيق Baseline على قاعدة
   فارغة، سجل هجرة واحد، القيود والمشغّلات موجودة، و`HasPendingModelChanges=false`.
2. **مقارنة مع المخطط الحي (PostgreSQL 17):** طُبّق Baseline في schema مؤقت
   `baseline_check` على القاعدة التجريبية، ثم قُورن بـ`public`:
   - الأعمدة: **717/717** متطابقة.
   - القيود: **169/169** (فرق لفظي فقط: `trim` مقابل `btrim` في قيد واحد، وأسماء
     المفاتيح الأساسية القديمة `PK_RealEstates`/`PK_RealEstateOwners` صُحّحت إلى
     `PK_Assets`/`PK_AssetOwners`).
   - الفهارس: **231/231**.
   - المشغّلات: **4/4**.
3. **حذف schema المقارنة** بعد التحقق؛ لم تُمسّ بيانات `public`.
4. **إعادة ضبط القاعدة التجريبية (2026-11-14):** أُسقطت جداول `public` القديمة
   (بعد النسخة الاحتياطية)، ثم طُبّق الـBaseline مباشرة. التحقق الحي:
   `tables=51`, `history_rows=1`, `migration_id=20261114101737_InitialPostgresBaseline`,
   `triggers=4`, `constraints=2`. أُقلع التطبيق محليًا ضدها: `/healthz` = 200
   و`/api/meta/current-year` = 200. ثم حُذف مستخدم البذر المؤقت، فبقي `public`
   بلا مستخدمين؛ عند تشغيل خدمة `Render` يستخدم `Bootstrap__AdminPassword`
   المضبوط هناك لزرع المدير.

## 4. إعادة ضبط القاعدة التجريبية وتطبيق Baseline

الإجراء المعتمد (بعد النسخة الاحتياطية والتحقق):

1. إسقاط جداول `public` (بما فيها `__EFMigrationsHistory`) مع الإبقاء على schema
   وصلاحياته، أو استخدام قاعدة فارغة جديدة.
2. تطبيق Baseline:
   `dotnet ef database update --context DocGeneratorPostgresDbContext`
   (مع `POSTGRES_CONNECTION` في نافذة النشر).
3. التحقق:
   - `SELECT count(*) FROM "__EFMigrationsHistory"` = **1**، والهجرة
     `20261114101737_InitialPostgresBaseline`.
   - `SELECT count(*) FROM information_schema.tables WHERE table_schema='public'
     AND table_type='BASE TABLE'` = **51**.
   - وجود المشغّلات الأربعة و`CK_Documents_FileNumberRequiresCircuit`.
4. إقلاع التطبيق بـ`Database__AutoMigrate=false` وفحص `/healthz` ثم دخان
   (دخول/بحث/فتح تفاصيل).

> **تحذير:** لا تُشغّل `dotnet ef database update` للسياق القديم
> `DocGeneratorDbContext`؛ لم يبقَ له هجرات، والمرجع الوحيد هو
> `DocGeneratorPostgresDbContext`.

## 5. التراجع

- التراجع عن الـBaseline = استعادة النسخة الاحتياطية أعلاه (`pg_restore`) ثم إعادة نشر
  النسخة السابقة من الكود.
- لا يُعتمد `Down` على قاعدة حيّة؛ النسخة هي مسار التراجع.
