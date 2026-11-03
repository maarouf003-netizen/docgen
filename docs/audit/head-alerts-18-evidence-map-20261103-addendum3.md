# ملحق 3 — خريطة أدلة §18: تصحيح الأعداد (2026‑11‑03)

> ملحق ثالث مؤرَّخ للوثيقة `head-alerts-18-evidence-map-20261103.md`. **الأصل وملحقا 1 و2
> لا يُحرَّران** (سياسة `docs/audit`)؛ التصحيح يُسجَّل هنا فقط.
>
> **الغرض:** إثبات ثلاثة أرقام وردت في الخريطة/الخطة بمصادرها، بعد إعادة عدّها على
> الشجرة الحالية. هذه **ليست شهادة إنجاز**: حالة البنود تبقى كما هي في الخطة.

---

## 1) `EnqueueSystemAsync` = 23 موضع استدعاء (لا 26)

**الأمر المستخدم** (من جذر المستودع، `PowerShell`):

```powershell
Get-ChildItem backend/src -Recurse -Include *.cs |
  Select-String -Pattern 'EnqueueSystemAsync' -AllMatches
```

**النتيجة:** 25 مطابقة نصية. منها **إعلانان** لا استدعاءان:

| # | الملف | السطر | النوع |
|---|---|---|---|
| 1 | `backend/src/DocGenerator.Application/Common/Interfaces/IHeadAlertOutbox.cs` | 21 | إعلان الواجهة |
| 2 | `backend/src/DocGenerator.Application/Services/HeadAlertOutbox.cs` | 32 | تنفيذ الواجهة |

⇒ **23 موضع استدعاء فعلي** موزّعة على: `DocumentDelegationService` (8) ·
`DocumentAppealService` (5) · `PublicEntityService` (3) · `PublicEntityService.Moves` (2) ·
`PublicEntityService.Branches` (1) · `ExecutionCircuitService` (2) ·
`ReviewLetterService` (1) · `DocumentDelegationService.Mirror` (1).

**الخلاصة:** رقم الخطة `§18:3061` («26 موقع محتوى = 24 إنشاءً + موضعا تحديث») **غير مطابق**
للشجرة الحالية. الرقم الصحيح **23**، وقد أُضيف تصحيح مؤرَّخ أسفل البند نفسه في الخطة.

## 2) مواضع الأرشفة = 8 + 2 (لا 11)

**الأمر المستخدم:**

```powershell
Get-ChildItem backend/src -Recurse -Include *.cs |
  Select-String -Pattern 'ArchiveByDelegationAsync|ArchiveByAppealAsync' -AllMatches
```

**النتيجة:** 15 مطابقة نصية، تُوزَّع كما يلي:

| النوع | العدد | المواضع |
|---|---|---|
| استدعاءات مباشرة من خدمات العمل | **8** | `DocumentDelegationService` (4) · `DocumentStatusService` (2) · `DocumentAppealService` (2) |
| استدعاءات داخلية للمسار الأساسي | **2** | `HeadAlertService.ApplySupersedePlanAsync` ⇒ `ArchiveByDelegationCoreAsync` و`ArchiveByAppealCoreAsync` |
| إعلانات الواجهة/التنفيذ | 4 | `HeadAlertService.cs` (141، 148، 665، 679) |
| إشارات توثيقية | 3 | تعليق `HeadAlertService.cs:146` + تعليقان في `HeadAlertKindCatalog.cs` (148، 178) |

⇒ **10 مواضع أرشفة فعلية (8 + 2)**، لا 11 كما ورد في الخريطة (صف `§18:3052`).
الفرق ليس إصلاحًا مطلوبًا: كل الاستدعاءات تستخدم `archiveKinds` صريحة
(`DelegationKinds`/`AppealKinds`)، والعدد التاريخي 11 كان يَعُدّ إعلانًا/توثيقًا مع الاستدعاءات.

## 3) «31 catch» رقم تاريخي لا أثر له في الشجرة الحالية

الرقم 31 في الخطة (`§0.1`، `§5.3`، `§14.1`، `§18:3015`/`3227`) يصف **عدد معالجات
`head_alert_failed` التي أُزيلت في `PR‑2b`**، لا عددًا قائمًا.

**أمر التحقق:**

```powershell
Get-ChildItem backend/src -Recurse -Include *.cs |
  Select-String -Pattern 'head_alert_failed' -AllMatches
```

**النتيجة:** **صفر مطابقة** في `backend/src` (وهو ما يثبته البند `§18:3078` أصلًا).
وللعلم، إجمالي كلمة `catch` في الشجرة الحالية **261** مطابقة — أي أن «31» ليس عددًا
حاليًا لأي مجموعة `catch`، بل سجل تاريخي لإزالة نمط ابتلاع فشل التنبيه.

---

## حدود هذا الملحق

- لا يعدّل الأصل ولا الملحقين؛ ولا يغيّر حالة أي بند.
- لا يتضمن أي ادعاء عن سلوك لم يُقرأ في الكود؛ كل رقم أعلاه مأخوذ من أمر بحث مذكور.
- تصحيح `§18:3061` (26 ⇒ 23) مسجَّل كملاحظة مؤرَّخة في الخطة نفسها، ولم تُحذف الصياغة الأصلية.
