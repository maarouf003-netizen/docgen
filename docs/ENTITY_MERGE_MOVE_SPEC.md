# خطة تنفيذ المرحلة 5: تغيير التبعية + الدمج + أحداث الجهات والتنبيهات

> **الحالة:** مواصفة معتمدة من صاحب المشروع (جلسة 2026-08-26) — تُنفَّذ كما هي.
> **قاعدة العمل:** أي محادثة تنفيذ تبدأ بقراءة هذا الملف و`docs/ENTITY_PORTAL_PLAN.md` معًا.

---

## 0) قرارات مالك المشروع المعتمدة (لا تُعدَّل)
1. **CoverageLabel** لحل الفروع متعددة المحافظات، مع قاعدة التوجيه **(أ):**
   «الفروع الإقليمية يدير/يراجع رئيس مقرها الإداري الأساسي» (`Governorate` = المقر).
2. **التبعية بالنقل على مستوى الفرع** (`MoveEntry`) هي البداية الأولية؛ تغيير تبعية جهة كاملة =
   تنفيذ متتابع لكل قيودها ثم إيقاف الهوية القديمة. وضعيتان لنفس المسند:
   نقل إلى الهوية / **طيّ في قيد قائم** (ترحيل روابط ثم إيقاف القديم + اسمه اسمًا بديلًا).
3. **تنبيه رئيس الهوية الجديدة عند النقل**: نعم، يُرسل دائمًا.
4. **بيانات المرسوم**: إلزامية عندما ينفّذ الإدارة/Mشرف فعلًا رسميًا (دمج/إلغاء/تسمية بموجب قرار)،
   اختيارية لتصحيحات رئيس القسم الداخلية.
5. **إشعار رئيس الهوية الجديدة عند النقل** = نعم (تنبيه واحد خفيف).
6. نصوص أطراف الملفات القائمة **لا تُعاد كتابتها** عند النقل/تغيير التبعية (ثابتة قانونًا)؛
   «توحيد النصوص» خيار مستقبلي فقط في الدمج الكبير، يُطلق معطلًا أول إصدار.

---

## 1) CoverageLabel (م-1)
- عمود `PublicEntity.CoverageLabel` (string? max150).
- العرض: `CoverageLabel ?? GovernorateFormatted` في البطاقات/التوليد؛
  الحوكمة والفلترة والتجميع والفهرس الفريد تبقى على `Governorate` (المقر) حصرًا.
- تحقق: يُرفض إن طابق اسم محافظة واحدة من الكتالوج؛ حد 150؛ trim.
- FE: زر ☑ «يخدم أكثر من محافظة» يُظهر الحقل؛ عرضه بـbreak-words.
- مرجع العرض المشترك: استبدال نقاط العرض في EntityRegistryManagement/Review/Picker/PortalCards.

## 2) جدول أحداث الجهات المهيكلة (م-3) — العمود الفقري
```csharp
// DocGenerator.Domain/Entities/PublicEntityChangeEvent.cs
Id, EntryId?(SetNull), GroupId(SetNull), ActionKind(string 30),   // rename|move|merge|abolish|create|review|import
DecreeKind?(30), DecreeNumber?(50), DecreeDate?(datetime2),
PayloadJson(text required),                                        // قبل/بعد + خريطة الفروع
ActorUserId(Restrict), CreatedAtUtc
```
- فهارس: EntryId, GroupId, CreatedAtUtc, ActionKind.
- Postgres: `ReviewedAtUtc` موجود مسبقًا + `DecreeDate/CreatedAtUtc → timestamp with time zone`.
- يُكتب داخل نفس معاملة العملية دائمًا. هو مصدر: شاشة المراقبة + وقوعات الملف الآلية + نصوص التنبيهات.

## 3) MoveEntry (م-2) — الوضعيتان
API:
```
POST /api/entity-registry/{id}/move        { targetGroupId? | targetEntryId?, decreeKind?, decreeNumber?, decreeDate?, note? }
POST /api/entity-registry/move-all         { sourceGroupId, targetGroupId, ... }   // تبعية كاملة
```
معاملة التنفيذ (بالترتيب):
1. تحقق: هدف موجود ونشط؛ القيد `NeedsReview=false`؛ لا تعارض (gov+branch لدى الهدف →
   رسالة تقترح وضعية الطيّ)؛ حدود الرئيس عبر EnsureHeadScopeAsync(entry.Governorate).
2. `entry.GroupId = target` (وضعية أ). في وضعية (ب): ترحيل روابط RegistryId إلى targetEntry،
   إيقاف القيد المنقول، إضافة اسمه الكامل اسمًا بديلًا للهدف (تمييز بالتطبيع).
3. إعادة بناء `Document.ApplicantRegistryId` لكل ملف متأثر من صفوفه (آلية الحفظ القياسية).
4. كتابة ChangeEvent (ActionKind=move، Payload يشمل fromGroup/toGroup + خريطة القيود المنقولة).
5. وقوعات آلية على كل ملف متأثر: OccurrenceType جديد `entity-change` (نص جاهز بالتبعية
   والمرسوم إن وجد + مفتاح ChangeEvent لضمان عدم التكرار).
6. تنبيه رئيس الهوية الجديدة: «أُلحق بقيدكم فرع من هيئة أخرى…».
7. تدقيق `move_entity_registry` بالمستخدم والأعداد.
الأثر المتوقع (اختباره): المندوبون يتحولون آليًا بالارتباط؛ الإحصاءات تنتقل تلقائيًا؛
نصوص ملفات قائمة لا تتغير؛ التوليد الجديد يستخدم النص المخزن حتى يعدّله المحامي.

## 4) الدمج N←1 (4-B) — يبني على MoveEntry
- صلاحية جديدة: `RolePermissions.CanMergeEntities(role)` => Manager || Admin (حصرًا).
- حواجز: رفض إذا أي طرف `NeedsReview=true`؛ منع الذات؛ تنفيذ ثنائي متسلسل؛ تأكيد بكتابة اسم الناجي.
- API:
```
POST /api/entity-registry/merge-preview { survivorGroupId, absorbedGroupIds[] }
POST /api/entity-registry/merge-commit  { same + unifyTexts=false default }
```
- خطوات commit داخل معاملة: لكل مُهمَل: ترحيل روابط حسب خريطة الفروع (قديم←ناجٍ مطابق gov/branch
  وإلا القيد الافتراضي للناجي)، إعادة بناء المسرّعات، نقل الأسماء البديلة، إعادة توجيه مندوبَي
  الهوية/القيود المُهمَلة إلى الناجي، إيقاف المُهمل، اسمه الكامل اسمًا بديلًا، وقوعات آلية للملفات،
  حدث دمج أب ببيانات المرسوم وPayloadJson كامل بخريطة الدمج (أساس أي تراجع مستقبلي).
- `unifyTexts=true` (معطل افتراضيًا): يعيد استخدام آلية SyncTextsAfterRenameAsync للتوحيد مع تدقيق قبل/بعد.

## 5) الإشعارات (مصفوفة معتمدة)
| الحالة | الجمهور | القناة/النص |
|---|---|---|
| محامٍ أدخل جهة جديدة | رؤساء محافظتها (نشطون) | قائم (منفذ في المرحلة السابقة) |
| اعتماد مراجعة كما هي | لا أحد | صمت مقصود |
| تعديل تسمية أثناء المراجعة | المُدخِل المحامي | قائم («من … إلى …») |
| نقل قيد | رئيس الهوية الجديدة | «أُلحق بقيدكم فرع من هيئة أخرى…» |
| دمج/إلغاء/تسمية رسمية بالإدارة | كل المحامين + رؤساء الأقسام | HeadAlert واحد بمستلمين جماعيين: «…بموجب القرار رقم N بتاريخ D، يرجى أخذ العلم» |
| عدلت الإدارة ضمن محافظة رئيس | ذلك الرئيس | «عدّلت الإدارة … ضمن محافظتك» |

## 6) قسم الوقوعات «التغييرات التي وقعت على الجهة العامة»
- نوع جديد `entity-change` في OccurrenceType (كتالوج + FE تجميع بصري بعنوان فرعي خاص).
- يظهر في تفاصيل الملف وطباعة الوقوعات؛ يحمل نصًا جاهزًا + مرجع المرسوم + مفتاح الحدث.

## 7) شاشة مراقبة الإدارة «سجل تغييرات الجهات»
- مصدرها PublicEntityChangeEvent فقط (بلا parsing نصوص AuditLogs).
- فلاتر: محافظة/نوع الحدث/المستخدم/فترة + تصدير Excel (نمط blob القائم).
- سطور تفصيلية: من/ماذا/متى/قبل←بعد/المرسوم — تلبي حرفيًا: «يُظهران رئيس قسم اللاذقية قام بهذا…».

## 8) ترتيب التنفيذ داخل هذه المرحلة
1. CoverageLabel (+عرض+تحقق+اختبار).
2. جدول ChangeEvent + هجرتان (AddEntityEvents/AddEntityEventsPg) + DecreeDate pg override.
3. MoveEntry (وضعيتا) + move-all + وقوعات آلية + تنبيه الرئيس الجديد + اختبارات (~8).
4. الدمج preview/commit + CanMergeEntities + اختبارات (~8).
5. شاشة المراقبة + تصديرها + اختبارات (~4).
6. قسم الوقوعات في FE + اختبار.
7. تحقق كامل (dotnet test، oxlint/tsc/vitest/build) + CHANGELOG [1.11.0] + RUN_GUIDE §9 + كومِت/push.

## 9) أنماط مرجعية إلزامية (من الكود القائم)
- مسند الرؤية الموحد: `PortalRepository.ScopePredicate/StatsBase`.
- مزامنة النصوص والتدقيق: `PublicEntityService.SyncTextsAfterRenameAsync` + `LogDocumentChangeAsync`.
- الوقوعات: `DocumentOccurrenceConfiguration` + `OccurrencesEditor` FE.
- تنبيهات: `HeadAlert/HeadAlertRecipient` + نمط الإنشاء في `HeadAlertService.CreateAsync`.
- معاينة/تنفيذ بنمط import-preview/commit + تأكيد كتابة اسم الناجي.
- اختبارات DB: `TestDb.Create()` + `FakeAuditLogger` + بذر مستخدم مرجعي Id=1 قبل القيود (FK).

## 10) تنبيهات النشر
- هجرتان جديدتان متوقعتان لهذه المرحلة: `AddEntityEvents` / `AddEntityEventsPg`
  (+ CoverageLabel ضمن الأولى أو هجرة مستقلة `AddCoverageLabel` بحسب ما يولّده ef).
- تطبيق `dotnet ef database update` للسياقين في نافذة النشر وإدراجهما في §9.
