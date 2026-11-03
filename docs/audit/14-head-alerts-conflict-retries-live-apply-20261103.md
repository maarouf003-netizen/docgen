# 14 — تطبيق هجرة `HeadAlertOutbox_ConflictRetries_Pg` حيًا (2026‑11‑03)

> لقطة تنفيذ مؤرَّخة (سياسة `docs/audit`: لا تُحرَّر بعد التجميد). الغرض: إثبات تطبيق
> هجرة `ConflictRetries` على قاعدة `PostgreSQL` الحيّة قبل نشر الكود، مع الأرقام والتحقق.

## 1) السياق

- الكود مدموج على `master` (`4205fc4`) ويتضمن عمود `HeadAlertOutboxMessages.ConflictRetries`.
- الإنتاج `PostgreSQL` (`Supabase`) بـ`Database:AutoMigrate=false`؛ `DatabaseInitializer.EnsureSchemaIsCurrentAsync`
  يرفض الإقلاع عند وجود هجرة معلّقة. لذلك **الترتيب إلزامي: هجرة ← تحقق ← نشر كود**.
- الهجرة: `Persistence\MigrationsPostgres\20261103172448_HeadAlertOutbox_ConflictRetries_Pg.cs`
  (`integer NOT NULL DEFAULT 0` + قيد `CK_HeadAlertOutbox_ConflictRetries` + تعبئة
  `ConflictRetries = 1` حيث `LastError = 'conflict_retry'`).

## 2) التطبيق

```powershell
# سلسلة الاتصال تُقرأ من ملف خارج المستودع ولا تُطبع في أي مخرج.
$env:POSTGRES_CONNECTION = <session-pooler Supabase>   # مُطبَّعة من postgresql:// إلى Host=…;Port=…;…
dotnet ef database update 20261103172448_HeadAlertOutbox_ConflictRetries_Pg `
  --context DocGeneratorPostgresDbContext `
  --project src/DocGenerator.Infrastructure --startup-project src/DocGenerator.Api
```

**المخرجات:**

```
20261103172448_HeadAlertOutbox_ConflictRetries_Pg (Pending)   # قبل
Applying migration '20261103172448_HeadAlertOutbox_ConflictRetries_Pg'.
Done.                                                          # بعد
```

## 3) التحقق بعد التطبيق

| الفحص | النتيجة |
|---|---|
| `migrations list` — الهجرة | `20261103172448_HeadAlertOutbox_ConflictRetries_Pg` بلا `(Pending)` |
| الهجرات المطبَّقة على `PostgreSQL` | **64** |
| الهجرات المعلّقة على `PostgreSQL` | **0** |
| `SQLite` | يبقى **5 معلّقة** (`Expand`/`Outbox_Create`/`Backfill`/`Contract`/`ConflictRetries`) — ليس قاعدة الإنتاج |

> لم يُتحقَّق من العمود باستعلام `SQL` مباشر في هذه الجلسة (لا عميل `psql` مثبّت)؛
> الدليل هو نجاح `database update` (معاملة واحدة) + `migrations list` + مراجعة `Up`،
> إضافةً إلى تطبيق الهجرة نفسها بنجاح على `SQLite` محليًا سابقًا.

## 4) التراجع

- **تراجع الكود وحده آمن:** النسخة السابقة تتجاهل العمود.
- **تراجع القاعدة** (`database update 20261026152238_HeadAlertTaxonomy_Contract_Pg`) يُسقط
  العمود والقيد ويُفقد عدّاد التعارض فقط — لا بيانات تنبيهات.

## 5) المتبقي

- نشر كود `master` على `Render` (Manual Deploy أو Retry) ثم فحص `/healthz` و`/api/alerts`.
- `SQLite`: لا تُطبَّق وحدها — بعد `G2` أو بسكربت فرقي من `Contract`، مع إيقاف المشغّل.
