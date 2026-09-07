# توحيد تسميات الجهات العامة (المدير/المشرف)

## المبدأ العام

توحيد تسميات الجهات العامة (بدون الفروع) للمدير والمشرف. قد يعتمد رؤساء الأقسام في فروع الإدارة المختلفة
تسميات مختلفة لنفس الجهة العامة، وتقوم هذه الميزة بتوحيد هذه التسميات إلى تسمية واحدة معتمدة تظهر في الاقتراحات.

أمثلة على التسميات غير الدقيقة التي تُوحَّد:
- "السورية للبناء للتشييد" بدون المدير العام
- "المدير العام للمصرف التجاري السوري" (إضافة لوظيفته لا داعي لها)
- "مدير عام" بدل "المدير العام"
- أخطاء كتابية

الميزة خاصة بالجهات الأم فقط، ولا علاقة لها بالفروع نهائيًا.

## الصفحة: «توحيد تسميات الجهات العامة»

تعرض الصفحة بطاقتين مباشرة دون تبويبات فرعية — تبويب «المجموعات المتشابهة» أُلغي نهائيًا
(واجهةً وخلفيةً: نقطة `GET /groups/similar-groups` ودالة العنقدة `ClusterGroups` وأنواعها
واختباراتها حُذفت، فلا كود ميت):

### بطاقة «كافة الجهات العامة»
- قائمة مسطّحة بكل الجهات العامة بعدّادات القيود والملفات، مع بحث خادمي وتقميم (100).
- زر «تحديث» في الترويسة يُعيد جلب القائمة بالبحث الحالي دون تحديث صفحة المتصفح.
- اختيار جهة واحدة يعرض مشابهاتها في البطاقة المجاورة.
- الهدف: التقاط جهة عامة لتوحيد مشابهاتها عليها (تسميتها الصحيحة تبقى).

### بطاقة «مشابهات الجهة المحددة»
- تعرض الجهات المتقاربة في الاسم مع النسبة والعدادات (`GET /groups/{id}/similar-to` بعتبة 0.55).
- زر «توحيد تسمية المتشابهات مع الجهة المحددة ذات التسمية الصحيحة» يفتح نافذة التوحيد
  بالجهة المحددة هدفًا والمشابهات ممتصة.
- بعد نجاح أي توحيد تُعاد جلب قائمة الجهات تلقائيًا (إضافة لزر التحديث اليدوي).

## القرارات المعتمدة

| القرار | التوصية المعتمدة |
|--------|-------------------|
| مصدر التسمية الموحدة | من المختارين حصرًا (لا اسم جديد يدويًا) |
| معالجة الأسماء القديمة (الخاطئة) | تُحفظ كأسماء بديلة «للبحث فقط» — تُستخدم للعثور ولا تظهر كاقتراح، والموحدة وحدها تظهر وتُخزَّن |
| الصلاحيات | المدير/المشرف فقط (`HasFullAccess`) |
| خوارزمية التشابه | هجينة: Jaccard-Bigram + Normalized Levenshtein + Token-Jaccard، بمتوسط مرجّح |
| معالجة الكلمات الوظيفية | نعم — «المدير العام»/«مدير عام»/«فرع» تُعد ثانوية وتُخفّف لرفع تشابه الجهة نفسها |
| عتبة مشابهات جهة محددة | 0.55 |
| هجرات EF | لا يوجد (لا تغيير في المخطط) |

## الهيكل البرمجي

```
backend/src/
  DocGenerator.Application/
    Common/
      ArabicNameSimilarity.cs            ← EDIT: خوارزمية التشابه الهجينة لاقتراح مشابهات جهة
                                                 محددة (دالة العنقدة ClusterGroups وثابت عتبتها محذوفان)
      Interfaces/
        IPublicEntityRepository.cs       ← EDIT: CountLinkedDocumentsAsync (batch)
    DTOs/
      EntityRegistryDtos.cs              ← EDIT: +SimilarToItemDto, SimilarToResponse (وأنواع العناقيد
                                                SimilarGroup* محذوفة)،
                                                إضافة LinkedDocumentCount إلى PublicEntityGroupDto
    Services/
      IPublicEntityService.cs            ← EDIT: +FindSimilarToGroupAsync (ودالة GetSimilarGroupsAsync محذوفة)
      PublicEntityService.cs             ← EDIT:
        UnifyNamesAsync الجديد (مزامنة + aliases للبحث + مندوبون + وقوعات + سجل)
        CountLinkedDocumentsAsync (batch)
        FindSimilarToGroupAsync (مشابهات جهة محددة)

  DocGenerator.Api/
    Controllers/
      EntityRegistryController.cs        ← EDIT: +GET /groups/similar-to (ونقطة similar-groups محذوفة)

frontend/src/
  types/index.ts                         ← EDIT: +أنواع جديدة + LinkedDocumentCount
  components/entity/
    UnifyNamesModal.tsx                  ← EDIT: حذف التحذير "لن تُحفظ"، دعم الهدف من المختارين،
                                           حذف قسم المرسوم من مسار التوحيد فقط (الخلفية تقبل null)
    UnifyNamesTab.tsx                    ← RESTRUCTURED (سابقًا SimilarGroupsUnifyTab): بطاقتا كافة
                                           الجهات والمشابهات مباشرة بلا تبويبين فرعيين + زر تحديث
                                           + إعادة جلب تلقائية بعد التوحيد (لوحة العناقيد محذوفة)
  pages/
    EntityRegistryReviewManagement.tsx   ← EDIT: تفعيل تبويب "توحيد تسميات" (بطاقتان مباشرتان)

backend/tests/.../ArabicNameSimilarityTests.cs   ← NEW (اختبارات العنقدة محذوفة مع الدالة)
backend/tests/.../PublicEntityServiceTests.cs    ← EDIT (اختبارا similar-groups محذوفان)
frontend/src/components/entity/UnifyNamesTab.test.tsx  ← RESTRUCTURED (سابقًا SimilarGroupsUnifyTab.test.tsx)
```

## خوارزمية التشابه الهجينة

1. **Jaccard على Bigrams**: تقسيم الاسم المُطبَّع إلى أزواج أحرف متتالية (2 حرف) وحساب تشابه المجموعات.
2. **Normalized Levenshtein**: مسافة التعديل مقسومة على أطول اسم.
3. **Token Jaccard**: تقسيم الاسم إلى كلمات وحساب تشابه المجموعات.
4. **معالجة الكلمات الوظيفية**: عبارات مثل «المدير العام»/«مدير عام»/«الإدارة العامة»/«فرع» تُزال
   مؤقتًا من نص المقارنة، ونأخذ الأقصى من تشابه الجوهر وتشابه النص الكامل — فيرتفع تشابه الجهة
   نفسها مهما اختلفت صياغتها الوظيفية دون خلط جهات مختلفة فعليًا.

الأوزان: `bigram=0.4`، `levenshtein=0.3`، `token=0.3` (تُستخدم حصرًا لاقتراح مشابهات جهة محددة؛
تجميع Union-Find أُلغي مع تبويب المجموعات المتشابهة).

ملاحظة: عُدّلت العتبة من 0.65 إلى 0.55 لأن الأمثلة الواقعية (مثل «المصرف التجاري السوري» مع إضافة
«المدير العام») احتُسبت ~0.58 — أي دون العتبة الأصلية، فكانت ستُفوَّت من المجموعات المتشابهة.

## سلوك UnifyNamesAsync الجديد

بعد تحقق التعرّضات (كما في PreviewUnifyAsync):
1. نقل القيود النشطة إلى مجموعة الهدف (تغيير GroupId) — **إلا القيود المطابقة سابق الوجود (المحافظة/الفرع
   حرفيًا Ordinal) المُعدّ منها طيًّا**، فتُبطل وتُرحّل روابطها (`ApplicantPublicEntities`/`ExecutedPublicEntities`/
   `ExecutionApplicants` + `ApplicantRegistryId`) وأسماءها البديلة إلى القيد الناجي (سيمانتك دمج الفروع).
2. تعطيل المجموعات الممتصة.
3. إضافة أسماء المجموعات الممتصة كأسماء بديلة «للبحث فقط» على القيد الناجي/المنقول.
4. مزامنة النصوص في الملفات عبر `SyncTextsAfterRenameAsync` (يغيّر صور الأسماء القديمة في كل الملفات).
5. مزامنة الاستئنافات عبر `SyncAppealsAfterEntityChangeAsync`.
6. ترحيل المندوبين عبر `MigrateDelegatesAsync` — المطوي يرحل للقيد الناجي (عبر خريطة
   `entryTargetByAbsorbed`)، والمنقول يبقى على قيده بلا مساس.
7. إنشاء DocumentOccurrence (نوع `entity-change`) لكل ملف متأثر.
8. إنشاء PublicEntityChangeEvent (ActionKindCatalog.Unify) مع حمولة تذكر `entriesFolded` و`folds`.

### سياسة المفتاح وحتمية الترتيب
- مطابقة خام `Ordinal` على `(Governorate, BranchName)` بلا تطبيع — فرق فراغ زائد يعني «نقلًا» لا «طيًّا».
- تُبنى «بركة ناجين» تبدأ بنسخ القيود النشطة للهدف ويُلحق بها كل قيد يُنقل. لذا عند ممتصتين بنفس المفتاح
  والهدف خالٍ منه: تُنقل الأولى (تُضاف للبركة) وتُطوى الثانية عليها — حتمية مرتبطة بترتيب الطلب.

## معاينة التوحيد (PreviewUnifyAsync)

- ترجع `UnifyNamesPreviewResponse(TargetName, AbsorbedGroups, TotalEntriesToMove, TotalEntriesFolded,
  FoldsToApply, Warnings)`.
- `EntryFoldPreviewDto(AbsorbedGroupId, AbsorbedGroupName, Governorate, BranchName, LinkedDocumentCount)`
  بقيدٍ سيُطوى؛ العداد عبر `CountLinkedDocumentsByEntryIdsAsync` (batch واحدة).
- المعاينة **تحاكي طريقة التنفيذ بالضبط** (دورة بركة الناجين نفسها) فتتفق نتائجها مع الاعتماد:
  العدّاد يزاد داخل حلقة القيد للمنقول فقط، والطيّ لا يُعدّ نقلًا. لا سطر «تعارض» بعد الآن — التعارض
  أصبح طيًّا؛ المحاكاة نُقلت كمصدر حقيقة (المراجعة F2/G2).

## الواجهة (UnifyNamesModal)

- سطر الملخص: `{TargetName} — {TotalEntriesToMove} قيدًا سيُنقل` + `{TotalEntriesFolded} قيدًا مطابقًا سابق
  الوجود سيُطوى` عند وجود طي.
- قسم «قيود ستُطوى» يعرض لكل طيٍّ: `Governorate / BranchName` (من `AbsorbedGroupName`) + عدد الملفات.
- لكل مجموعة ممتصة تُعرض `EntryCount` (إجمالي القيود النشطة شاملة المطوي) مع تنويه «(منها N مطابق سيُطوى)»
  عند وجود طي في المجموعة، ليميّز العدد الإجمالي عن صافي القيود المنقولة في سطر الملخص (`TotalEntriesToMove`).
- رسالة النجاح بعد التأكيد تذكر `entriesMoved` وقيد الطي عند وجوده (`UnifyResponse.EntriesFolded`).
- نافذة التوحيد لا تجمع مرسومًا (حُذف قسم «المرسوم (اختياري)» منها) — حقول `UnifyRequest` الاختيارية
  باقية في الخلفية وتُستقبل `null`؛ مرسوم «تعديل جهة عامة» (تعديل تسمية/دمج/حلول) لم يُمسّ.

## مراحل التنفيذ والتحقق

| المرحلة | المحتوى | التحقق |
|---------|---------|--------|
| 1 | خوارزمية التشابه (`ArabicNameSimilarity.cs`) + اختبارات | `dotnet test` |
| 2 | DTOs جديدة + `LinkedDocumentCount` في `PublicEntityGroupDto` | `dotnet build` |
| 3 | batch عدّاد الملفات في الـ repository/service | `dotnet test` |
| 4 | `UnifyNamesAsync` الجديد (مزامنة + aliases للبحث + وقوعات + سجل + مندوبون) | `dotnet test` |
| 5 | endpoint `similar-to` (مشابهات جهة محددة) | `dotnet test` |
| 6 | Frontend: بطاقتا `UnifyNamesTab` (كافة الجهات + المشابهات) + المودال (بلا مرسوم) | `npx vitest run` |
| 7 | تفعيل تبويب توحيد تسميات | `npx vitest run` |
| 8 | إلغاء تبويب/لوحة «المجموعات المتشابهة» نهائيًا بلا كود ميت (نقطة `similar-groups` + `ClusterGroups` + أنواعها + اختباراتها) + بطاقتان مباشرتان + زر تحديث يدوي/تلقائي + إعادة تسمية زر التوحيد | `dotnet test` + `npx vitest run` |
| 9 | تنقية النصوص الإثباتية عند غياب المرسوم (`EntityChangeMessages.DecreeSuffix` بلا «بموجب» عائمة، ويحرس النقص الجزئي) + `aria-current` لحالة التحديد + بانر نجاح التوحيد + تنسيق أرقام إشعار القطع | `dotnet test` + `npx vitest run` |

## تنبيه النشر

لا توجد هجرات EF جديدة — لا حاجة لتطبيق قاعدة بيانات.
