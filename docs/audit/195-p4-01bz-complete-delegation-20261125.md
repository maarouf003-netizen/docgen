# `P4-01ca` — نقل إتمام الإنابة إلى Command — 2026-11-25

> **النطاق:** المرحلة الثانية من دفعة دورة حياة الإنابة: ملكية
> `DocumentDelegationService.CompleteAsync` داخل `CompleteDocumentDelegationCommand`،
> مع حذف `DocumentDelegationService.Lifecycle.cs` بعد إفراغه.

## ما نُفِّذ

- `CompleteDocumentDelegationCommand` + handler: شروط الإتمام (مسجّلة، ملكية المناب،
  «متداول فقط» للمناب والمنيب)، تاريخ الإعادة وقرار الإحالة القطعية، بدل المبيع لكل أصل
  والتغطية، تحويل الحالة، تفعيل المنيب «منفذ جبريا (جزئيا)» مع تطهير التريث/السير،
  وقعة `Forcible`، تنبيه الإتمام/الأرشفة، وتدقيق يقارن البدلات قبل/بعد — معاملة واحدة.
- ترقيات لإعادة الاستخدام: `ClearTarithAndSayerFields`/`SerializeDetails`/`FormatSalePrices`
  (Helpers) و`TargetFileLabel`/`TargetCourt` (Mapping) صارت `internal`.
- `IDocumentDelegationService` فقد `CompleteAsync` (9 أعضاء متبقية كلها قراءات/حرّاس/مزامنة).
- الاختبارات: مصنع `Complete` + جسر `CompleteAsync`، وتحويل 12 موضع نداء.

## إثبات الحفاظ على السلوك

- **جسم الدالة:** 130/130 سطرًا، `diffs = 0` بعد تطبيع بادئتي القواعد/الخدمة.
- **النصوص الحرفية:** 43/43؛ الفرق الوحيد تأهيل `FormatSalePrices` داخل نص التدقيق
  (`DocumentDelegationService.FormatSalePrices`) — نفس المحتوى.
- **المعاملة:** كل التحقق قبل `_tx.RunAsync`، والوقعة والتدقيق داخلها كما في الأصل.
- **التنبيه/الوقعة:** نفس الأنواع و`SupersedePlan` (`delegation_completed`) ونفس تفاصيل
  `SerializeDetails` وقيود التفعيل مرة واحدة (`markApplied`).

## مراجعة نقدية أولى

| # | السؤال | النتيجة |
|---|---|---|
| 1 | هل نُسخ منطق التفعيل التلقائي؟ | لا — نفس الأسطر حرفيًا |
| 2 | هل بقيت نسخة من `SerializeDetails`/`FormatSalePrices`؟ | لا — ترقية للاستخدام المشترك |
| 3 | هل تحرّك ترتيب المعاملة أو الوقعة؟ | لا |
| 4 | هل بقي `Lifecycle.cs`؟ | حُذف كاملًا |
| 5 | هل صار حقل خدمة ميتًا؟ | نعم (6 حقول) — أُزيلت في المراجعة الهجومية `196` |
| 6 | هل المعالج مسجَّل؟ | نعم |

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة · audit · migration-guard لا تغييرات · `DI` 112/112/0.

> اختبارات `PostgreSQL` تُخطّى محليًا؛ `backend-postgres` في `CI` هو المرجع القاطع.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
