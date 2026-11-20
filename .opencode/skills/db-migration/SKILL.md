---
name: DB Migration (PostgreSQL)
description: Use when adding, changing, or verifying an EF Core migration in this repo. PostgreSQL only (DocGeneratorPostgresDbContext), migration guards, RUN_GUIDE §9 + AGENTS watchlist, and the deploy warning.
---

# هجرة قاعدة بيانات (PostgreSQL فقط)

## متى تُستخدم

- أي تغيير في `DocGeneratorDbContext` أو الكيانات أو `IEntityTypeConfiguration` يغيّر **المخطط**:
  عمود جديد، تغيير نوع، قيد `CHECK`/`FK`/`UNIQUE`، فهرس.
- التحقق من هجرات معلّقة قبل النشر أو قبل فتح `PR`.
- لا تُستخدم لتغيير **قيمة** عمود `DateTime?` قائم — لا هجرة مطلوبة.

## حقائق ثابتة في هذا المستودع

- قاعدة التشغيل `PostgreSQL` فقط (`ADR-0001`)؛ لا هجرات `SQLite` بعد `P0-02`.
- مجلد الهجرات الوحيد:
  `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres`.
- سياق التشغيل والهجرات: `DocGeneratorPostgresDbContext`. لا يوجد سياق تشغيلي غيره.
- في الإنتاج `Database:AutoMigrate=false`: التطبيق **يرفض الإقلاع** إذا وُجدت هجرة معلّقة، فلا
  تعتبر المهمة منجزة قبل تنبيه المستخدم بأمر التطبيق.
- عدد الهجرات الحالي والترتيب التراكمي في `RUN_GUIDE.md` §9، والقائمة المختصرة في `AGENTS.md`
  (§ «تذكير الهجرات المعلّقة»). الحارس: `node scripts/migration-watchlist-guard.mjs`.

## الخطوات

1. **تحقق من خط الأساس أولًا** — لا تبدأ إن كان الحارس أحمر:

   ```powershell
   node scripts/migration-watchlist-guard.mjs
   ```

2. **غيّر النموذج** في `backend/src/DocGenerator.Infrastructure/Persistence` (الكيان أو
   `OnModelCreating`) لا في الهجرات القديمة.

3. **أنشئ الهجرة بالسياق الصحيح فقط**:

   ```powershell
   dotnet ef migrations add <Name> `
     --context DocGeneratorPostgresDbContext `
     --project backend/src/DocGenerator.Infrastructure `
     --startup-project backend/src/DocGenerator.Api
   ```

   - لا تستخدم `--context DocGeneratorDbContext`.
   - لا تُعدّل هجرة طُبّقت على أي بيئة؛ التصحيح بهجرة جديدة.

4. **تحقق من مطابقة النموذج للقطة (G1)** — يتطلب سلسلة وهمية فقط:

   ```powershell
   $env:POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=docgen;Username=x;Password=x'
   dotnet ef migrations has-pending-model-changes `
     --context DocGeneratorPostgresDbContext `
     --project backend/src/DocGenerator.Infrastructure `
     --startup-project backend/src/DocGenerator.Api
   ```

5. **حارس السلامة** — يرفض `Drop`/`Alter`/`Rename`/`DeleteData` في `Up` بلا خطة expand/contract
   وبلا إدخال موثّق في `scripts/migration-safety-allowlist.json` (لا تُدخل استثناءً بلا موافقة المالك):

   ```powershell
   node scripts/migration-safety-guard.mjs
   ```

6. **Preflight قبل هجرات القيود على البيانات**: شغّل `scripts/p0-07b-preflight.sql` (وأي سكربت
   preflight مخصص) على نسخة من قاعدة الإنتاج أولًا؛ أي قيمة سالبة تُفشل هجرة القيود.

7. **حدّث الوثيقتين في التغيير نفسه** (الحارس يفشل بدونهما):
   - `RUN_GUIDE.md` §9 — سطر جديد بتاريخ الإنشاء واسم الهجرة وحالتها (معلّقة/مطبّقة).
   - `AGENTS.md` § «تذكير الهجرات المعلّقة» — بند الهجرة الجديدة.
   - أضف سطرًا إلى قائمة الهجرات التراكمية فقط؛ لا تحذف بندًا معلّقًا قبل تطبيقه فعليًا.

8. **تحقق كامل**:

   ```powershell
   dotnet build backend/DocGenerator.sln --nologo -warnaserror
   dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --nologo -v q
   node scripts/migration-watchlist-guard.mjs
   node scripts/migration-safety-guard.mjs
   ```

   ثم أعد الخطوة 4 (لازم `No changes`).

## أمر النشر (يُذكر حرفيًا في التقرير)

```powershell
dotnet ef database update --context DocGeneratorPostgresDbContext
```

- الترتيب الإلزامي: نسخة احتياطية مجرَّبة → تطبيق الهجرات → التحقق من `__EFMigrationsHistory` →
  تشغيل النسخة الجديدة.
- لا تُشغّل `--context DocGeneratorDbContext` (لا هجرات له).
- إن كانت الهجرة جديدة وغير مطبّقة: اكتب تنبيهًا صريحًا أن المهمة لا تُعدّ منجزة حتى تُطبَّق في
  نافذة النشر.

## أخطاء شائعة

- نسيان `--startup-project` ⇒ فشل إنشاء التصميم (`POSTGRES_CONNECTION` مطلوب من
  `PostgresDbContextFactory`).
- كتابة `DateTime.Now`/`DateTime.Today` في الكيان أو seed — القاعدة `DateTime.UtcNow` حصرًا.
- تعديل `InitialPostgresBaseline` أو `ModelSnapshot` يدويًا بدل إنشاء هجرة.
- إضافة فهرس/قيد ثم نسيان خطوة `RUN_GUIDE` ⇒ حارس القائمة الأحمر على `CI`.

## مخرجات التقرير

- اسم الهجرة (أو الهجرات) وعددها، وتاريخها.
- نتيجة الحارسين و G1.
- هل تحتاج preflight؟ نتيجته.
- بند `RUN_GUIDE` §9 و`AGENTS.md` المضافان.
- تنبيه النشر بالأمر أعلاه.
