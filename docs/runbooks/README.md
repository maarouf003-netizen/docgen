# Runbooks — إجراءات التشغيل

> كل runbook: **الأعراض → التشخيص → الإجراء → التحقق → التراجع/التصعيد**.
> الأوامر التشغيلية الكاملة في [`../../RUN_GUIDE.md`](../../RUN_GUIDE.md) §9.
> لا تُلصق أسرارًا (مفاتيح/كلمات مرور/سلاسل اتصال) في تذاكر أو سجلات.

| الحالة | الملف |
|---|---|
| نشر نسخة جديدة | [`deploy.md`](deploy.md) |
| تراجع عن نسخة سيئة | [`rollback.md`](rollback.md) |
| هجرات قاعدة البيانات | [`migrations.md`](migrations.md) |
| قاعدة البيانات غير متاحة | [`database-down.md`](database-down.md) |
| تعطّل الدخول/المصادقة | [`auth-outage.md`](auth-outage.md) |
| تراكم/توقف صندوق الصادر | [`outbox-stuck.md`](outbox-stuck.md) |
| تدوير الأسرار | [`secret-rotation.md`](secret-rotation.md) |
| اختبار الحمل وSLO قبل الإصدار | [`load-testing.md`](load-testing.md) |
| الرصد و`SLO` والتنبيهات (`P5-02`) | [`observability.md`](observability.md) |
| النسخ الاحتياطي والاستعادة و`DR` (`P5-06`) | [`backup-restore.md`](backup-restore.md) |
| اختبارات E2E/الوصول/الجوال (`P6-04`) | [`e2e.md`](e2e.md) |
| بعد حادثة (تحليل بلا لوم) | [`postmortem-template.md`](postmortem-template.md) |

**فحوص سريعة موحّدة:**

```bash
curl -fsS https://<host>/healthz      # حياة العملية فقط (بلا قاعدة)
curl -fsS https://<host>/readyz       # القاعدة + صندوق الصادر (503 = غير جاهز)
curl -fsS https://<host>/healthz/outbox
```

سجلات الخادم: `logs/logs-YYYYMMDD.txt` تحت جذر الخلفية (احتفاظ 60 يومًا، سقف 50MB للملف).
