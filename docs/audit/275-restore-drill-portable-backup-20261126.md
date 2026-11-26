# تمرين استعادة + نسخ احتياطي محمول — 2026-11-26

> **النطاق:** تمرين استعادة فعلي لنسخة `Supabase` على `PostgreSQL 17` عادي، ثم إصلاح
> أدوات النسخ/الاستعادة لتكون **محمولة** (مخطط التطبيق `public` فقط) بدل النسخة الكاملة
> التي تحمل مخططات `Supabase` الداخلية.
>
> **الأسرار:** لم تُطبع أي كلمة مرور/مفتاح؛ القيم قُرئت من ملف محلي مشفّر ومُرّرت كمتغيرات
> بيئة للعمليات فقط.

## 1. النتيجة الأولى (قبل الإصلاح)

| المحاولة | النتيجة |
|---|---|
| `pg-restore.ps1` الرسمي على نسخة كاملة (`--single-transaction`) | **فشل:** `extension "supabase_vault" is not available` |
| استعادة يدوية بـ`--schema=public` بعد إنشاء `pg_trgm` | **نجحت** |
| التحقق اليدوي | 7 هجرات، 51 جدولًا، 4 مشغّلات، فهرس `IX_Documents_SearchText_Trgm`، البيانات: 0 مستندات/0 تنبيهات/مستخدم واحد |

**السبب:** `pg_dump` كان ينسخ قاعدة `postgres` كاملة على `Supabase`، فتضمّنت مخططات
`auth`/`storage`/`realtime`/`graphql`/`vault`/`pgbouncer` وامتدادات (`supabase_vault`,
`pgcrypto`, `uuid-ossp`, `pg_stat_statements`) وأدوارًا خاصة بالمنصة. هذا:
1. يجعل النسخة **غير قابلة للاستعادة** على `PostgreSQL` عادي (الخادم الحكومي المتوقع).
2. يحمل بيانات منصة لا يحتاجها التطبيق (سطح تعرّض أوسع).
3. يخالف نطاق الدليل المعلن: «بيانات التطبيق + `__EFMigrationsHistory`».

## 2. الإصلاح

| الملف | التغيير |
|---|---|
| `scripts/backup/pg-backup.sh` / `.ps1` | الافتراضي `--schema=public` (نطاق التطبيق)؛ `--all-schemas`/`-AllSchemas` للنسخة الكاملة القديمة. البيانات الوصفية تسجّل `scope` و`schema`. |
| `scripts/backup/pg-restore.sh` / `.ps1` | الافتراضي `--schema=public`؛ `--all-schemas` للاستعادة على نفس المنصة. قبل الاستعادة الكاملة يُنشئ `pg_trgm` في `public` ( يحتاجه فهرس `P3-02`). |
| `scripts/backup/backup-scripts-guard.mjs` | رموز إلزامية للنطاق والامتداد + ترتيب: التحقق/التأكيد ← `pg_trgm` ← `pg_restore`. |
| `docs/runbooks/backup-restore.md` | نطاق النسخة، الاستعادة المحمولة، ومنع `--all-schemas` على خادم عادي. |
| `RUN_GUIDE.md` §9 | سطر النطاق المحمول وإحالة هذا التمرين. |

## 3. التمرين بعد الإصلاح (برهان)

1. **نسخة جديدة** من قاعدة العرض:
   - `docgen-20261126T104358Z.dump` — **279386** بايت (بدل 675774 للكاملة).
   - `SHA-256`: مسجّل في `.sha256`؛ `scope=app`, `schema=public`, هجرات 7.
   - فحص `pg_restore --list`: **لا** `supabase_vault`/`auth`/`storage`/`vault`؛ مخطط `public` فقط.
2. **قاعدة تمرين جديدة:** `initdb` محلي (`PostgreSQL 17.11`، منفذ `55434`)، قاعدة فارغة.
3. **الاستعادة بأداة المشروع** (بلا أي تدخل يدوي):
   ```text
   Restore scope: app schema 'public' only (portable to vanilla PostgreSQL).
   Checksum OK (...)
   Archive migrations: count=7 head=20261121034750_P3_02_PortalSearchIndex
   Ensuring required extension pg_trgm exists in schema 'public'...
   Restoring (single transaction, no owner/privilege replay)...
   Smoke OK: tables=51 history=7 head=20261121034750_P3_02_PortalSearchIndex documents=0
   Restore completed successfully.
   ```
4. **تحقق مستقل:** 7 صفوف هجرة، 51 جدولًا، 4 مشغّلات، `pg_trgm` في `public`،
   `IX_Documents_SearchText_Trgm` موجود، والبيانات: 0 مستندات/0 تنبيهات/مستخدم واحد.
5. **تنظيف:** أُوقف العنقود المؤقت وحُذف بالكامل؛ لم تُلمس قاعدة العرض.

## 4. ما لم يُثبت بعد

- تمرين استعادة على **بيانات إنتاج حقيقية** (قاعدة العرض فيها مستخدم واحد ولا مستندات).
- استعادة النسخة **الكاملة** (`--all-schemas`) على `Supabase` آخر (تحتاج نفس الامتدادات/الأدوار).
- تشغيل مسار shell الكامل للاستعادة (تم `--dry-run` للنسخ و`--self-test` للاستعادة؛
  المسار PowerShell نُفّذ كاملًا). المنطق متطابق، لكن البرهان النهائي على shell يبقى مطلوبًا
  في `CI`/الخادم.
- نسخ خارج الموقع و`PITR` وتمرين موقّع بـ`RTO` مقاس (ما زالت مسؤولية المالك/البنية التحتية).

## 5. الحكم

النسخة الاحتياطية صارت **محمولة**: يمكن استعادتها على `PostgreSQL` عادي، وتستبعد
مخططات/امتدادات `Supabase` الداخلية. هذا هو المسار الصحيح للانتقال إلى الخادم الحكومي
عندما يُحدَّد. لا تغيير كود تطبيق ولا هجرات في هذه اللقطة.
