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

تحتوي الصفحة على تبويبين:

### تبويب 1: «المجموعات المتشابهة»
- تظهر الجهات العامة المتشابهة ضمن مجموعات (كشف تلقائي عبر خوارزمية تشابه).
- ضمن كل مجموعة مربع اختيار لكل جهة + زر «توحيد التسمية».
- عند الضغط تظهر الاقتراحات مع عدّاد الاستخدام (عدد الملفات المرتبطة) لاختيار الجهة المعتمدة كتسمية موحدة.
- الهدف الافتراضي للمجموعة هو **الأعلى ارتباطًا بالملفات** ثم بالقيود، والأول أبجديًا عند التعادل —
  ومجرّد اقتراح قابل للتغيير قبل التنفيذ.
- عند التوحيد تتغير التسمية في كل مكان (الملفات التنفيذية المشطوبة والمنفذة والتريث... والاستئنافات والإحالات).

### تبويب 2: «كافة الجهات العامة»
- جدول مسطّح بكل الجهات العامة (بدون مجموعات)، بجانب كل جهة عدد الملفات المرتبطة بها ومربع اختيار.
- زر عام «توحيد التسمية».
- عند تحديد جهة واحدة: تفتح نافذة اختيار منفصلة تعرض المشابهات المقترحة (بالنسبة والتعداد) لاختيارها.
- عند تحديد عدة جهات: توحيد مباشر.
- الهدف: التقاط جهة عامة غير موجودة في المجموعات المتشابهة.
- تظهر الاقتراحات نفسها في الصفحتين (لا منع ولا ازدواج منطقي — كلاهما طريق للتوحيد).

## القرارات المعتمدة

| القرار | التوصية المعتمدة |
|--------|-------------------|
| مصدر التسمية الموحدة | من المختارين حصرًا (لا اسم جديد يدويًا) |
| معالجة الأسماء القديمة (الخاطئة) | تُحفظ كأسماء بديلة «للبحث فقط» — تُستخدم للعثور ولا تظهر كاقتراح، والموحدة وحدها تظهر وتُخزَّن |
| ازدواج الاقتراحات بين التبويبين | يُسمح (لا مشكلة منطقية) |
| الصلاحيات | المدير/المشرف فقط (`HasFullAccess`) |
| خوارزمية التشابه | هجينة: Jaccard-Bigram + Normalized Levenshtein + Token-Jaccard، بمتوسط مرجّح |
| عتبة تجميع المجموعات | 0.55 (بعد ضبط الكلمات الوظيفية) |
| معالجة الكلمات الوظيفية | نعم — «المدير العام»/«مدير عام»/«فرع» تُعد ثانوية وتُخفّف لرفع تشابه الجهة نفسها |
| عتبة مشابهات جهة محددة | 0.55 |
| هجرات EF | لا يوجد (لا تغيير في المخطط) |

## الهيكل البرمجي

```
backend/src/
  DocGenerator.Application/
    Common/
      ArabicNameSimilarity.cs            ← NEW: خوارزمية التشابه الهجينة + clustering + union-find
      Interfaces/
        IPublicEntityRepository.cs       ← EDIT: CountLinkedDocumentsAsync (batch)
    DTOs/
      EntityRegistryDtos.cs              ← EDIT: +SimilarGroupClusterDto, SimilarGroupItemDto,
                                                SimilarGroupsResponse, FindSimilarToResponse,
                                                إضافة LinkedDocumentCount إلى PublicEntityGroupDto
    Services/
      IPublicEntityService.cs            ← EDIT: +GetSimilarGroupsAsync, FindSimilarToGroupAsync
      PublicEntityService.cs             ← EDIT:
        UnifyNamesAsync الجديد (مزامنة + aliases للبحث + مندوبون + وقوعات + سجل)
        CountLinkedDocumentsAsync (batch)
        GetSimilarGroupsAsync + FindSimilarToGroupAsync (clustering)

  DocGenerator.Api/
    Controllers/
      EntityRegistryController.cs        ← EDIT: +GET /groups/similar-groups, +GET /groups/similar-to

frontend/src/
  types/index.ts                         ← EDIT: +أنواع جديدة + LinkedDocumentCount
  components/entity/
    UnifyNamesModal.tsx                  ← EDIT: حذف التحذير "لن تُحفظ"، دعم الهدف من المختارين
    SimilarGroupsUnifyTab.tsx            ← NEW: تبويب المجموعات المتشابهة
    AllEntitiesUnifyTab.tsx              ← NEW: تبويب كافة الجهات + نافذة اقتراحات
  pages/
    EntityRegistryReviewManagement.tsx   ← EDIT: تفعيل تبويب "توحيد تسميات" + عرض التبويبين

backend/tests/.../ArabicNameSimilarityTests.cs   ← NEW
backend/tests/.../PublicEntityServiceTests.cs    ← EDIT
frontend/src/components/entity/__tests__/SimilarGroupsUnifyTab.test.tsx  ← NEW
frontend/src/components/entity/__tests__/AllEntitiesUnifyTab.test.tsx    ← NEW
```

## خوارزمية التشابه الهجينة

1. **Jaccard على Bigrams**: تقسيم الاسم المُطبَّع إلى أزواج أحرف متتالية (2 حرف) وحساب تشابه المجموعات.
2. **Normalized Levenshtein**: مسافة التعديل مقسومة على أطول اسم.
3. **Token Jaccard**: تقسيم الاسم إلى كلمات وحساب تشابه المجموعات.
4. **معالجة الكلمات الوظيفية**: عبارات مثل «المدير العام»/«مدير عام»/«الإدارة العامة»/«فرع» تُزال
   مؤقتًا من نص المقارنة، ونأخذ الأقصى من تشابه الجوهر وتشابه النص الكامل — فيرتفع تشابه الجهة
   نفسها مهما اختلفت صياغتها الوظيفية دون خلط جهات مختلفة فعليًا.

الأوزان: `bigram=0.4`، `levenshtein=0.3`، `token=0.3`.

التجميع: خوارزمية Union-Find تربط كل زوج يتجاوز درجة التشابه العتبة (0.55).

ملاحظة: عُدّلت العتبة من 0.65 إلى 0.55 لأن الأمثلة الواقعية (مثل «المصرف التجاري السوري» مع إضافة
«المدير العام») احتُسبت ~0.58 — أي دون العتبة الأصلية، فكانت ستُفوَّت من المجموعات المتشابهة.

## سلوك UnifyNamesAsync الجديد

بعد تحقق التعرّضات (كما في PreviewUnifyAsync):
1. نقل القيود النشطة إلى مجموعة الهدف (تغيير GroupId).
2. تعطيل المجموعات الممتصة.
3. إضافة أسماء المجموعات الممتصة كأسماء بديلة «للبحث فقط» على مجموعة الهدف.
4. مزامنة النصوص في الملفات عبر `SyncTextsAfterRenameAsync` (يغيّر صور الأسماء القديمة في كل الملفات).
5. مزامنة الاستئنافات عبر `SyncAppealsAfterEntityChangeAsync`.
6. ترحيل المندوبين عبر `MigrateDelegatesAsync`.
7. إنشاء DocumentOccurrence (نوع `entity-change`) لكل ملف متأثر.
8. إنشاء PublicEntityChangeEvent (ActionKindCatalog.Unify).

## مراحل التنفيذ والتحقق

| المرحلة | المحتوى | التحقق |
|---------|---------|--------|
| 1 | خوارزمية التشابه (`ArabicNameSimilarity.cs`) + اختبارات | `dotnet test` |
| 2 | DTOs جديدة + `LinkedDocumentCount` في `PublicEntityGroupDto` | `dotnet build` |
| 3 | batch عدّاد الملفات في الـ repository/service | `dotnet test` |
| 4 | `UnifyNamesAsync` الجديد (مزامنة + aliases للبحث + وقوعات + سجل + مندوبون) | `dotnet test` |
| 5 | endpoints `similar-groups` + `similar-to` | `dotnet test` |
| 6 | Frontend: `SimilarGroupsUnifyTab` + `AllEntitiesUnifyTab` + تعديل المودال | `npx vitest run` |
| 7 | تفعيل تبويب توحيد تسميات | `npx vitest run` |
| 8 | التحقق الشامل النهائي | كل أدوات الفحص |

## تنبيه النشر

لا توجد هجرات EF جديدة — لا حاجة لتطبيق قاعدة بيانات.
