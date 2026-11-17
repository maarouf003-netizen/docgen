# Runbook — نشر نسخة جديدة

> المرجع الملزم: `RUN_GUIDE.md` §9 (الترتيب، الهجرات، الفحص الدخاني). هذا ملخّص تنفيذي.

## الأعراض/السياق

وصول نسخة جديدة إلى `master` أو نشر يدوي مطلوب.

## قبل النافذة

1. **البوابات الست خضراء** على الـ`PR` المدموج (`backend`, `frontend`, `guards`, `audit`,
   `backend-postgres`, `migration-guard`).
2. **نسخة احتياطية + استعادة تجريبية** (لا نسخة بلا اختبار استعادة).
3. **الهجرات المعلّقة** من `RUN_GUIDE.md` §9 معروفة، و`p0-07b` له preflight على القاعدة الفعلية.
4. **متغيّرات `P2-03`** مضبوطة في المنصّة (`AllowedHosts`, HSTS, HTTPS redirect, CSP enforce,
   Swagger off, KnownProxies أو TLS app) — وإلا الخدمة **لن تقلع**.
5. سجّل الهجرات المطبّقة حيًّا:
   `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;`

## أثناء النافذة

1. تطبيق الهجرات **قبل** تشغيل النسخة:
   `dotnet ef database update --context DocGeneratorPostgresDbContext`
   (بعد preflight عند لزومه؛ لا `--context DocGeneratorDbContext`).
2. تحقّق من `__EFMigrationsHistory` (الصفوف الجديدة ظهرت).
3. شغّل/انشر النسخة الجديدة.

## التحقق

```bash
curl -fsS https://<host>/healthz        # 200
curl -fsS https://<host>/readyz         # 200 (503 = قاعدة/outbox)
curl -sI https://<host>/ | grep -i '^strict-transport-security\|^content-security-policy'
```

- دخول ناجح، بحث، فتح تفاصيل، توليد `Word`/`Excel`، وسجل تدقيق يعمل.
- لا أخطاء 5xx جديدة في السجل خلال أول 10 دقائق.

## التراجع

- فشل بعد النشر ⇒ [`rollback.md`](rollback.md).
- **لا `Down` على القاعدة الحية**؛ التراجع بالنسخة + استعادة النسخة الاحتياطية إن لزم.

## التصعيد

مالك النظام + سجل الحادثة (postmortem بلا لوم) إن تجاوز التأثير 10 دقائق.
