---
name: DB Migration (PostgreSQL)
description: Use when adding, changing, or verifying an EF Core migration (PostgreSQL only). Creates with DocGeneratorPostgresDbContext, runs migration guards, updates RUN_GUIDE §9 + AGENTS watchlist, and states the deploy command. استخدمها لأي هجرة قاعدة بيانات.
---

# هجرة قاعدة بيانات (PostgreSQL فقط)

إجراء تنفيذي. القواعد الحاكمة تبقى في `AGENTS.md`؛ هذه المهارة تترجمها إلى خطوات وأوامر.

## متى تُستخدم

- تغيير **مخطط** في `DocGeneratorDbContext` أو الكيانات أو `IEntityTypeConfiguration`
  (`Persistence/Configurations/`): عمود، نوع، طول، دقة، قيد، فهرس، علاقة.
- التحقق من هجرات معلّقة قبل النشر أو قبل `PR`.
- **لا** تُستخدم لتغيير قيمة عمود `DateTime?` قائم — لا هجرة مطلوبة.

## حقائق ثابتة

- `PostgreSQL` فقط (`ADR-0001`)؛ لا هجرات `SQLite` بعد `P0-02`.
- مجلد الهجرات الوحيد: `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres`.
- سياق الهجرات: `DocGeneratorPostgresDbContext` (لا سياق تشغيلي غيره).
- الإنتاج `Database:AutoMigrate=false`: هجرة معلّقة = رفض إقلاع مقصود.
- حارس القائمة: `node scripts/migration-watchlist-guard.mjs` — يفشل إن لم تُسجَّل كل هجرة في
  **بند** (`-`/`*`) داخل `RUN_GUIDE.md` §9 و`AGENTS.md` § «تذكير الهجرات المعلّقة».
- لا تعتمد على عدد ثابت: عدد الهجرات الحالي يظهر في مخرجات الحارس.

## الخطوات

1. **خط الأساس أخضر** — لا تبدأ على أحمر:

   ```powershell
   node scripts/migration-watchlist-guard.mjs
   ```

2. **غيّر النموذج** في `Persistence/` (الكيان أو `Configurations/`). لا تُعدّل هجرة قائمة طُبّقت؛
   التصحيح بهجرة جديدة. لا تُعدّل `DocGeneratorPostgresDbContextModelSnapshot.cs` يدويًا.

3. **أنشئ الهجرة** (الاسم يتبع نمط الكود الحالي: `<Code>_<ShortName>`، مثل `P0_08_ShortName`):

   ```powershell
   dotnet ef migrations add P0_08_ShortName `
     --context DocGeneratorPostgresDbContext `
     --project backend/src/DocGenerator.Infrastructure `
     --startup-project backend/src/DocGenerator.Api
   ```

4. **راجع `Up` بنفسك** قبل أي اختبار: هل يوجد `Drop`/`Alter`/`Rename`/`DeleteData`؟ إن نعم،
   تحتاج خطة expand/contract على مرحلتين (إضافة → تعبئة → إزالة لاحقًا) لا هجرة كاسرة واحدة.

5. **مطابقة النموذج للقطة (G1)** — لا يحتاج قاعدة فعلية، سلسلة وهمية تكفي:

   ```powershell
   $env:POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=docgen;Username=x;Password=x'
   dotnet ef migrations has-pending-model-changes `
     --context DocGeneratorPostgresDbContext `
     --project backend/src/DocGenerator.Infrastructure `
     --startup-project backend/src/DocGenerator.Api
   ```

   المطلوب: `No changes have been made to the model since the last migration.`

6. **حارس السلامة** — أي عملية هدم في `Up` تُرفض بلا استثناء موثّق في
   `scripts/migration-safety-allowlist.json` (لا استثناء بلا موافقة المالك):

   ```powershell
   node scripts/migration-safety-guard.mjs
   ```

7. **Preflight للقيود المعتمدة على البيانات**: شغّل `scripts/p0-07b-preflight.sql` (أو preflight
   المخصص) على نسخة من قاعدة الإنتاج أولًا؛ قيمة واحدة مخالفة تُفشل هجرة القيد.

8. **سجّل الهجرة في الوثيقتين، في نفس الـ commit** (الحارس يفشل بدونهما):
   - `RUN_GUIDE.md` §9، قسم «هجرات بانتظار التطبيق (قائمة تراكمية)» — بند جديد بالشكل المتبع:
     `` - [ ] **YYYY-MM-DD — `<ClassName>` (`<Code>`)** — وصف موجز + أمر التطبيق. ``
     استخدم `- [ ]` للمعلّقة (الحارس يعدّها) و`- [x]` بعد التطبيق الفعلي فقط؛ لا تشطب بندًا
     قبل تطبيقه على البيئة.
   - `AGENTS.md` § «تذكير الهجرات المعلّقة» — بند يذكر اسم الهجرة الكامل وتاريخها وحالتها
     (بند `-` عادي، وليس `- [ ]`).

9. **تحقق كامل**:

   ```powershell
   dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
   dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --nologo -v q
   node scripts/migration-watchlist-guard.mjs
   node scripts/migration-safety-guard.mjs
   ```

   ثم أعد الخطوة 5 (يجب أن تبقى `No changes`)، وقبل الدفع نفّذ مهارة `release-gate` كاملة.

## إن أخطأت في الهجرة

- **قبل التطبيق**: `dotnet ef migrations remove --context DocGeneratorPostgresDbContext --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api`، ثم أصلح النموذج وأعد الإنشاء.
- **بعد التطبيق على أي بيئة**: لا تحذف ولا تعدّل؛ أنشئ هجرة تصحيحية.

## أمر النشر (يُذكر حرفيًا في التقرير)

```powershell
dotnet ef database update --context DocGeneratorPostgresDbContext
```

- الترتيب: نسخة احتياطية مجرَّبة → تطبيق الهجرات → التحقق من `__EFMigrationsHistory` →
  تشغيل النسخة الجديدة.
- لا تُشغّل `--context DocGeneratorDbContext` (لا هجرات له).
- هجرة جديدة غير مطبّقة = المهمة **غير منجزة** حتى تُطبَّق في نافذة النشر.

## أخطاء شائعة

- نسيان `--startup-project` ⇒ فشل إنشاء التصميم (`PostgresDbContextFactory` يطلب `POSTGRES_CONNECTION`).
- `DateTime.Now`/`Today` في الكيان أو seed — القاعدة `DateTime.UtcNow` حصرًا (`UtcDateTimeConverter`).
- تحديث `RUN_GUIDE.md` دون `AGENTS.md` (أو العكس) ⇒ حارس أحمر.
- إضافة هجرة بلا بند في القائمة ثم «إصلاح» الحارس بتعديل الحارس نفسه.

## مخرجات التقرير

- أسماء الهجرات وعددها وتاريخها، ونتيجة الحارسين و G1.
- هل لزم preflight؟ نتيجته.
- البندان المضافان في `RUN_GUIDE.md` §9 و`AGENTS.md`.
- تنبيه النشر بالأمر أعلاه.
