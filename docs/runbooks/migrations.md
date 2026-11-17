# Runbook — هجرات قاعدة البيانات

> المرجع الكامل: `RUN_GUIDE.md` §9 (القائمة التراكمية + أمان الهجرات). لا تشغّل
> `--context DocGeneratorDbContext` أبدًا (لا هجرات له بعد `P0-02`).

## الأعراض

- الخدمة **ترفض الإقلاع** برسالة هجرات معلّقة (`Database:AutoMigrate=false` في الإنتاج).
- أو `migration-guard`/`migration-watchlist` فشلا على `PR`.

## التشخيص

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;
```

قارن الناتج بهجرات الحزمة في `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres`
وبالقائمة في `RUN_GUIDE.md` §9. أي فرق = لا تنشر قبل التحقيق.

## الإجراء (قبل النشر)

1. **نسخة احتياطية** + استعادة تجريبية.
2. إن كانت الهجرة **تعتمد على البيانات** (مثل `P0_07b_AmountConstraints`):
   شغّل `scripts/p0-07b-preflight.sql` على **قاعدة الإنتاج الفعلية** واصلح أي قيمة سالبة قبل التطبيق.
3. راجع سكربت `migration-guard` (`migrations-sql`) المرفوع كأثر في الـ`PR`.
4. طبّق:
   ```bash
   dotnet ef database update --context DocGeneratorPostgresDbContext
   ```
5. بعد هجرات التدقيق: صلاحيات القراءة فقط (بدور المالك/DBA):
   `psql "$DATABASE_URL" -v app_role=<runtime_role> -f scripts/audit-readonly-privileges.sql`
6. شغّل النسخة الجديدة **بعد** التحقق من `__EFMigrationsHistory`.

## التحقق

- الصفوف الجديدة ظهرت في `__EFMigrationsHistory`.
- `has_table_privilege` على جداول التدقيق يعيد `false` لدور التطبيق، وعدد المشغّلات 4.
- `/readyz` = 200.

## التراجع

- **ممنوع `Down` على القاعدة الحية.** إن فشلت هجرة: أوقف النشر، استعد النسخة الاحتياطية،
  وأصلح الهجرة في `PR` جديد.
- لا تحذف هجرة بعد تطبيقها في أي بيئة.

## التصعيد

مالك النظام + مراجعة `migration-safety-allowlist.json` (أي عملية هدم تتطلب استثناءً مبررًا).
