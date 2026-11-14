# ADR-0005 — نطاق التزامن المتفائل وآليته (`P0-06`)

- **الحالة:** معتمد.
- **التاريخ:** 2026-11-14.
- **المرجع:** `docs/PERFECTION_ROADMAP.md` §4 `P0-06`، و`docs/adr/0002-transactions-retry.md`.

## 1. السياق

كان `Version` موجودًا على `Document` وحده (RF-010)، وبقية الكيانات القابلة للتحرير
تُحفظ بلا عدّاد: أي كتابتان متزامنتان من مستخدمين مختلفين تُنتجان **آخر كتابة تفوز**
(lost update) بصمت — بلا `409` ولا أثر تدقيقي للخسارة.

## 2. القرار

- **عقد مشترك:** `IVersionedEntity { long Version { get; set; } }` في `Domain`، وكل كيان
  يدخل النطاق يطبّقه ويُعرَّف عموده `IsConcurrencyToken` في `IEntityTypeConfiguration`.
- **زيادة مركزية:** `DocGeneratorDbContext.SaveChanges(bool)` و`SaveChangesAsync(bool, CancellationToken)`
  يزيدان نسخة كل كيان `Modified` من `IVersionedEntity` **مرة واحدة** إن لم يغيّرها الكاتب
  صراحةً (`CurrentValue == OriginalValue`)، فلا يعتمد ضمان الحماية على تذكّر كل مسار كتابة.
  مسارات `ExecuteUpdateAsync` (تتجاوز التتبّع) تزيد النسخة في استعلامها صراحةً.
- **فحص مبكر اختياري:** `OptimisticConcurrencyGuard.EnsureCurrent(entity, expectedVersion, subject)`
  يرفض النسخة القديمة بـ`DocumentConflictException` (→ `409`) ورسالة عربية ودية.
  غياب `Version` في الطلب يعني **قبولًا بلا فحص مبكر** (توافق خلفي كامل)، وتبقى شبكة
  الأمان البنيوية (قيد `WHERE Version = @original` في `UPDATE`) ناشطة دائمًا.
- **الاستجابة:** كل `DTO` لكيان داخل النطاق يحمل `Version` ليعيده العميل في التعديل.

## 3. النطاق

**داخل النطاق (16 كيانًا):** `Document`, `DocumentAppeal`, `DocumentDelegation`,
`ExecutionCircuit` (قائمة)، و`User`, `PublicEntity`, `PublicEntityGroup`, `Correspondence`,
`Section`, `Branch`, `ReviewLetter`, `ReviewLetterMessage`, `PersonalReminder`,
`ForumMessage`, `ExecutionAction`, `DocumentIntervention` (جديدة).

**خارج النطاق بقرار مقصود:**

- **سجلات إلحاقية/قراءة فقط:** `HeadAlert`, `HeadAlertRecipient`, `HeadAlertOutboxMessage`,
  `AuditLog`, `DocumentOccurrence`, `DocumentFieldChange`, `PublicEntityChangeEvent`,
  `HeadSuccession`, `ReviewLetterMessageRead`, `ForumMessageRead`, `IdempotencyKey`.
  لا تُحرَّر بعد الإنشاء، فلا سباق lost-update عليها.
- **كيانات الراية/الحالة الضيقة:** `DocumentAssignment`, `DocumentBaseNumber`,
  `AppealBaseNumber` — تُكتب بقرار خادمي واحد من مسار محكوم، وتكفيها قيود القاعدة الفريدة.
- **جداول الأسماء البديلة/الرسائل/الإيصالات** (`PublicEntityAlias`, `CorrespondenceMessage`,
  `CorrespondenceReceipt`, `ReviewLetterMessage` عمليًا إلحاقي): تضيف صفًا ولا تعدّل صفًا.
- **اقتراحات التعديل** (`ParentEditSuggestion`, `AppSuggestion`): دورة حياة مراجعة أحادية
  الاتجاه (بانتظار → معتمد/مرفوض) محمية بفحص الحالة داخل معاملة.
- **اللقطات/الأصول** (`Asset`, `DelegationAsset*`, `AssetOwner`): تُكتب من الملف الأب داخل
  معاملة واحدة، وحمايته بـ`Version` تكفي؛ وإفرادها بعدّادات يضاعف كلفة الحفظ بلا فائدة.

## 4. الاستثناءات والتكلفة

- **الكتابة المزدوجة داخل طلب واحد:** خدمة تُنفّذ `SaveChanges` مرتين على الكيان نفسه
  قد ترفع النسخة مرتين (الزيادة المركزية لكل حفظ). هذا **آمن وظيفيًا** (العدّاد رمز
  تغيير لا رقم إصدار دلالي)، لكنه يخالف اختبارات «+1 لكل طفرة» في مسار الاعتماد؛
  عولج الموضع الوحيد المتأثر بإزالة `Update` زائد بعد حفظ سابق (`AssignAsync`).
- **`DocumentWriteGuard` لم يُمسّ:** يزيد النسخة ويثبّت `OriginalValue` صراحةً قبل الحفظ
  (سلوك RF-010 القائم واختباراته)، والزيادة المركزية تحترم زيادته فلا ازدواج.
- **`SQLite` في الاختبارات:** `long Version` محمول على `SQLite` و`PostgreSQL` معًا، بخلاف
  `xmin` الذي يخصّ `PostgreSQL` وحده — فيبقى مسار الاختبار التشغيلي موحّدًا حتى `P1-08`.

## 5. العواقب

- أي كيان قابل للتحرير جديد يجب أن يطبّق `IVersionedEntity` ويُعرَّف قيده في
  `IEntityTypeConfiguration` — ويُغطّى باختبار `OptimisticConcurrencyTests`.
- الهجرة `20261114160957_P0_06_OptimisticConcurrency` تضيف 12 عمودًا
  (`bigint NOT NULL DEFAULT 0`) وهي **معلّقة** حتى تطبيقها في النشر:
  `dotnet ef database update --context DocGeneratorPostgresDbContext`.
