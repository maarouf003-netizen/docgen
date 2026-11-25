# `P4-01cv` — لقطة إغلاق `P4-01b`: ملكية Command/Query مثبّتة — 2026-11-25

> **النطاق:** المرحلة الثالثة والعشرون والأخيرة في سلسلة `P4-01`: تثبيت القاعدة في
> [`ADR-0010`](../adr/0010-command-query-ownership.md)، وإغلاق `P4-01b` بأدلة `DoD`،
> وتصحيح ادّعاءات «اكتمال القراءة» غير الدقيقة، وتنظيف ما كشفه المسح. لا تغيير سلوكي
> ولا هجرة. الفرع `refactor/p4-01cu-cv-reminders-forum-closure` من `master` (`e338e928`).

## 1. القرار المثبَّت

[`ADR-0010-command-query-ownership.md`](../adr/0010-command-query-ownership.md) يعتمد:

1. **معالج واحد لكل use-case**: الكتابة عبر `CommandHandler` يملك المعاملة/الآثار
   (أو `ITransactionRunner` عند الحاجة إلى `catch` حولها)، والقراءة الصرفة عبر
   `QueryHandler` بلا كتابة/معاملة/تدقيق.
2. **متحكمات رقيقة**: توجيه + صلاحيات/نطاق + تحويل إلى `Query`/`Command` + ترجمة أخطاء
   فقط؛ لا `ITransactionRunner`/`SaveChanges`/معاملة.
3. **قائمة ما يبقى على الخدمات عمدًا** بأعداد فعلية (حرّاس/مزامنة/مرايا/`Outbox`/أرقام/
   مرجعي/تدقيق بوابة)، و**دين قراءة معلن** منفصل لا يُخفى داخل «مغلق».

## 2. أدلة الإغلاق (مقيسة على `e338e928` + هذه الدفعة)

| الدليل | الأمر/الفحص | النتيجة |
|---|---|---|
| ملكية الكتابة | جرد كل `public interface I*Service` وربط أعضائه بمستدعيه | لا use-case كتابة على خدمة: كل كتابة endpoint = `CommandHandler` واحد؛ الباقي حرّاس/مزامنة/مرايا/`Outbox`/أرقام/تدقيق بوابة (ADR §2.3) |
| تسجيلات = أصناف | عدّ `AddScoped<I(Query|Command)Handler<…` مقابل `sealed class *Handler` | **218 / 218 / 0** (**100** استعلامًا + **118** أمرًا)؛ 218 نوعًا مميزًا، 0 مكرر |
| لا خدمة قراءة ميتة | بحث `IForumService|IAppSuggestionService|IPersonalReminderService` في `backend/` | **0** مطابقة؛ الثلاث حُذفت مع تسجيلاتها |
| متحكمات بلا معاملات | `Select-String` على `backend/src/DocGenerator.Api/Controllers` لـ`ITransactionRunner|RunAsync|BeginTransaction|SaveChanges|CommitAsync` | **0** مطابقة (22 متحكمًا) |
| لا partials فارغة | فحص 57 ملف `partial class` بعد تجريد التعليقات | **0** جسم فارغ |
| لا حقول ميتة | عدّ كل `private readonly _field` في `Services/*.cs` مقابل تكراره في عائلة partials | **0** حقل بتكرار واحد |
| العقود الخارجية | `generate:api` + `git diff --ignore-cr-at-eol --exit-code -- src/api/generated.ts` | **0** فرق |
| النموذج | `dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext --no-build` بسلسلة وهمية | «No changes» — **لا هجرة** |

## 3. أسطح القراءة المتبقية (دين معلن — لا يمسّ `DoD` الكتابة)

| العقد | العدد | الأعضاء | المتابعة |
|---|---:|---|---|
| `IHeadAlertService` | 6 | `ListForActorAsync`/`ListSentAsync`/`ListByDelegationAsync`/`GetDetailsAsync`/`GetRecipientsAsync`/`CountUnreadAsync` | `P4-01cw` |
| `IStrikeOffReminderService` | 3 | `GetAsync`/`GetListAsync`/`GetCountAsync` | `P4-01cw` |
| `IAuditLogService` | 2 | `SearchAsync`/`GetDocumentChangesAsync` | `P4-01cw` |
| `IExcelExportService` | 4 | مصنّفات `xlsx` نقية من صفوف جاهزة | ليس قراءة قاعدة — يبقى مساعد تنسيق |

**تصحيح موثّق:** عبارة «لا يبقى سطح قراءة صرف معلن» في خريطة `P4-01ct` كانت غير دقيقة؛
الأسطح أعلاه قراءات صرفة باقية على خدماتها. أُبقيت صراحةً بدل إخفائها، ولا تمسّ `DoD`
`P4-01` (ملكية الكتابة + المتحكمات الرقيقة) المعلن في ADR §2.4.

## 4. تعديلات الوثائق الحية

- `docs/PERFECTION_ROADMAP.md`: حالة `P4-01` صارت **`P4-01b` مغلق — ملكية `use-case`
  مكتملة**، وأُضيفت فقرتا `P4-01cu`/`P4-01cv`، واستُبدل سطر «متبقٍ» بإغلاق `DoD` +
  إحالة `ADR-0010` + دين القراءة المعلن.
- `docs/README.md`: تحديث «آخر تحديث»، وإضافة `ADR-0010` إلى الفهرس، وإضافة اللقطات
  `242…245`.
- لا تعديل على أي لقطة مجمّدة (`docs/audit/239…241` بقيت كما هي).

## 5. الحكم

`P4-01b` **مغلق رسميًا**: نموذج الملكية موثّق ومُنفّذ ومحروس (218/218/0، 0 معاملة في
المتحكمات، 0 واجهة قراءة منقولة باقية)، وكل كتابة endpoint يملكها أمر واحد. الدين
المتبقي قراءة فقط ومسجّل ببند مستقل (`P4-01cw`)، فلا يُنسب إلى `P4-01b` ولا يُخفى.
