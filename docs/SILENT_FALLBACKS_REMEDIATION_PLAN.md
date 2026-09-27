# خطة معالجة الملاذات الصامتة وحدّ التسجيل (تطبيق حكومي — صفر تحمّل للخطأ)

> المرجع التشخيصي: مراجعة «الملاذات الصامتة وحدّ التسجيل في طبقة `Application` — بما فيها سياسة
> أخطاء الواجهة». القاعدة الملزمة: `AGENTS.md` (الأصول البرمجية الصارمة، لا حلول مختصرة، لا كسر
> عقود، تحقق كامل، اختبارات الحالات الحدّية). قاعدة التواريخ الحرة (`Date Fields Rule`) تبقى سارية
> بلا أي تغيير. لا هجرات قاعدة بيانات في هذه الخطة (توسيع `DTOs` عرضية + وقائع تدقيق في الجدول
> القائم) — فإن استلزم التنفيذ عمودًا جديدًا وُثّقت الهجرتان (`SQLite` + `Postgres`) وطُبّقتا حسب
> `RUN_GUIDE.md §9` قبل إعلان الإنجاز.

## 0. المبادئ الحاكمة (ملزمة لكل طور)

1. **لا صمت**: كل تدهور إما يُرفض صراحةً (`400 {message}` عربي) أو يُوسم ويُدوَّن (شارة عرض + واقعة
   `‎_audit.LogAsync‎` / سجل `Api`) — لا مخرج ثالث.
2. **لا `ILogger` جديد في `Application`**: الإشارة تعبر الحدّ إلى `Api`
   (`backend/src/DocGenerator.Api/Middleware/GlobalExceptionHandler.cs:48-54` والمتحكمات) حيث
   تُسجَّل بالسياق الكامل (`TraceId/UserId`). الحدود المعمارية لا تُكسر.
3. **لا اختراع تسميات**: سياسة `unknown` موحدة — إظهار الخام + شارة، لا تسمية صالحة كاذبة.
4. **لا إنجليزية للمستخدم**: حقلا `errors` و`title` الإطاريان (`ValidationProblemDetails`) لا يُقرآن
   أبدًا في `frontend/src/api/client.ts`. البديل رسالة عربية ثابتة مكتوبة في الواجهة.
5. **العقد `‎{message}‎` مُصدَر لا شفهي**: منتجوه (`GlobalExceptionHandler.cs:67` + `BadRequest {message}`
   في المتحكمات + `ClientErrorsController.cs:59-60`) ومستهلكه (`client.ts:13-14`) موثّقون ومُختبَرون
   من الطرفين.

## 1. الطور 1 — الواجهة الحيّة (أعلى أولوية: عيب يصيب المستخدم اليوم على بيانات نظيفة)

- الملف الوحيد: `frontend/src/api/client.ts:7-18`. التوقيع `‎(error: unknown) => string‎` ثابت —
  لا كسر تعاقدي لجميع المستهلكين (العشرات عبر الشاشات والمودالات و`useCancellableRequest.ts:57`).
- السلوك الجديد داخل `getApiErrorMessage` فقط:
  - `message` عربي صريح يُعرض كما هو (مسار `DocumentValidator` / `FreeDateParser` المحفوظ).
  - غيابه مع `status 400` ← رسالة عربية ثابتة (طلب غير صالح — تحقّق من الحقول وإعادة المحاولة).
  - `401/403` الثابتان يُحفَظان؛ `500` بلا `message` ← نص الخادم العام؛ غير-`Axios` ← العام.
  - `errors`/`title` لا يُقرآن أبدًا (منع التسريب الإنجليزي).
- الاختبارات (`frontend/src/api/client.test.ts:4-28` تُوسَّع): `message` / `400` بلا `message` /
  `errors`-فقط / `401` / `500` بلا `message` / غير-`Axios`.
- القبول: كل رسالة إطارية سابقة (`حدث خطأ غير متوقع` لحالة `400` ربط) أصبحت عربية مرشِدة؛ لا أي
  نص إنجليزي يصل الشاشة في أي حالة.
- التحقق: `npx oxlint src` + `npx tsc -b` + `npx vitest run` + `npm run build` + فحص جوال 375px.

## 2. الطور 2 — توثيق عقد `‎{message}‎` (توثيق واختبار، لا كود إنتاجي جديد)

- مقطع عقد واحد يذكر المنتجين والمستهلك وشكل `JSON` والحالات المستثناة (`400` الإطاري).
- الخلفي مثبت قائمًا (`GlobalExceptionHandlerTests.cs` + `ExceptionHandlerIntegrationTests.cs`)؛
  الأمامي يُثبَّت باختبارات الطور 1. أي متحكم جديد يعيد `ValidationProblem` أو `‎{msg}‎`/`‎{error}‎`
  يُكتشف بالاختبار لا بالشاشة.

## 3. الطور 3 — الفلاتر والحسابات الصامتة (أ5/أ6/أ7)

- أ5 — `backend/src/DocGenerator.Application/Services/PublicEntityService.cs:835-843`
  (`ParseChangeEventPeriod`): `From/To` غير الفارغة وغير الصالحة تُرفض بـ `ArgumentException`
  عربي (نمط `FreeDateParser.cs:23`) فيُردّ `400` ويُعرض عبر الطور 1 — بدل التوسيع الصامت إلى بلا فلتر.
- أ5-متمم — `PublicEntityService.cs:888-909` (`ResolveActorFilter`/`MatchesActorName`): فلتر فاعل
  يُطبَّع إلى فراغ ← صفر نتائج (لا `return true` للكل)؛ رقم بلا مستخدم ← صفر نتائج.
- أ6 — `backend/src/DocGenerator.Application/Common/ActionReminderCalculator.cs:13-27`: تاريخ تالف
  ← التذكير يُوسم مشتبهًا (لا fallback صامت إلى `createdAt`)؛ مدة مجهولة ← تُرفض عند الإدخال أو
  تُوسم (لا `0` صامتًا في `DurationDays:26`).
- أ6-متمم — `DocumentService.Apply.cs:880` + `ActionDateParser.cs:8`: `DateParsed=null` مع وسم
  المصدر (إحصاء من `CreatedAt` معلن لا خفي).
- أ7 — `backend/src/DocGenerator.Api/ClaimsPrincipalExtensions.cs:12`: `sub` غير رقمي ← رمي
  `UnauthorizedAccessException` (نمط `GetRoleEnum.cs:24-29`) بدل `0`؛ `GetRole (:16)` /
  `GetBranchId (:31-35)` تُحفَظ.
- اختبارات حدّية لكل فرع (تالف/فارغ/صفر/سالب/مجهول) في `backend/tests/DocGenerator.Application.Tests`.

## 4. الطور 4 — سجل التدقيق (أ1: أعلى حساسية إثباتية)

- `backend/src/DocGenerator.Application/Services/EntityChangeLogSummary.cs:24-51`: تمييز
  فارغ/تالف/مجهول بإشارة `degraded` تصل `ToChangeEventDto` (`PublicEntityService.cs:931-946`)
  فتُعرض «ملخص منقوص — راجع الخام» بدل تسمية عادية، وتُدوَّن واقعة جودة-بيانات عبر
  `‎_audit.LogAsync‎` القائم (نمط `:925-926`).
- توسيع `PublicEntityServiceTests.cs:1845-1848` (تالف ← موسوم، لا تسمية نظيفة).

## 5. الطور 5 — لقطات الاستئناف (أ2/أ3)

- `backend/src/DocGenerator.Application/Common/AppealSnapshotSerializer.cs:32-44` →
  `DocumentAppealService.cs:1043-1044`: التالف يُعرض بشارة تلف لا فراغ نظيف.
- `AppealSnapshotSerializer.cs:64-71` + `PublicEntityService.cs:3468-3484`: تخطي المزامنة للتالف
  يُدوَّن `appeal_entity_sync_skipped` بجانب `appeal_entity_sync (:3478-3481)` — لا `continue` صامت.
- توسيع `AppealSnapshotSerializerTests.cs:120-135` بالإشارة والواقعة. السلوك الحمائي المحفوظ:
  اللقطة التالفة لا تُكسر المعاملة (`json!` الأصلي) والتطبيع `null/"null" → "[]"` يبقى.

## 6. الطور 6 — الكتالوجات (أ4: سياسة موحدة بلا استثناء)

- النطاق الكامل: `AppealOutcome.cs:24`، `AppealDirection.cs:25`، `AppealStatus.cs:29`،
  `ExecutedStatus.cs:40`، `GeneralEntitySide.cs:26`، `OccurrenceType.cs:68`،
  `AssetKind.cs:55/66`، `PartyNature.cs:37`، `ExecutionStatus.cs:99/124/181/194` — المجهول ←
  الخام + شارة. المحفوظان: `ActionKindCatalog.cs:36` (صادق أصلًا) و`AllowedStatusChanges.cs:159`
  (إغلاق آمن).
- الكتابة مُحصَّنة أصلًا (`Apply.cs:505`، `PublicEntityService.cs:643`،
  `CorrespondenceService.cs:205`، ثوابت `ActionKind`) والاستيراد اسمي لا تعدادي (`:972-1110`)؛
  القاعدة بلا `CHECK` (`Configurations.cs:1048/1055`) فالتغيير دفاع عمق آمن مع تحديث شامل
  للاختبارات المتأثرة بالتسميات.
- الكود الميت `ActionKindCatalog.IsValid (:39)` — يُوظَّف في مسار كتابة أو يُحذف. لا بقاء لميت.

## 7. الطور 7 — التثبيت (ب)/(ج): يُوثَّق لا يُمسّ

- (ب): `DecreeSuffix (:17-28)` واختباراته، `null`/فارغ ← `[]`، `_ => kind`، الرفض العربي الصريح —
  تثبيت بتغطية قائمة.
- (ج): `catch ← BadRequest/403/404`، تجميع `preview.errors (:1298-1399)` المعروض في
  `BranchManagementModal.tsx:318/800`، `Program.cs:51-53/296-302`، `errorReporting.ts:33-78`،
  افتراضا التوافق (`DocumentValidator.cs:19-21/44-46`) — تُذكر في تقرير الإنجاز كمتروك عمدًا وموثق.

## 8. التحقق الإلزامي قبل إعلان الإنجاز (كل طور يُغلق بتحققه قبل التالي)

1. خلفي: `dotnet test` أخضر كامل.
2. أمامي (عند مساس `frontend/`): `npx oxlint src` + `npx tsc -b` + `npx vitest run` + `npm run build`.
3. تتبّع مسار كامل (`DTO ← Service ← عرض`) حقلًا بحقل لأي عقد ممدَّد.
4. `grep`/`rg` لبقايا المصطلحات والتسميات القديمة (لا تظهر إلا في الهجرات التاريخية و`Designer`).
5. قائمة تحقق مقابل بنود هذه الخطة بندًا ببند؛ أي انحراف يُقاس ويُعلَّل.
6. فحص جوال بصري (375px) لأي تغيير يمس التخطيط.
7. تنبيه النشر: لا هجرات في هذه الخطة ما لم يُضف عمود جديد — عندها تُذكر الهجرتان وتُطبَّقان
   (`DocGeneratorDbContext` + `DocGeneratorPostgresDbContext`) حسب `RUN_GUIDE.md §9`.

## 9. الجولة الثانية — نتائج المراجعة النقدية سطرًا بسطر والمتبقيات (منفذة)

- **F1 (خلل منطقي مُصلَح)**: `TryComputeDueDate` كان يقصّ المدة للوسم ولا يقصّها للحساب —
  `" أسبوع "` توسم سليمة وتحسب `0` أيام معًا. التطبيع الآن واحد للمقارنة والحساب
  (`ActionReminderCalculator.cs`) مع اختبار انحدار.
- **R1 (ثغرة كتابة حية مُسدّة)**: نوع إجراء الاستئناف كان حرًا (`Bounded` طولًا فقط) بينما مسار
  الملفات يقيّد بـ `action/note` والواجهة (`AppealActionsModal.tsx:42`) لا ترسل غيرهما —
  وثبت بالاختبار تخزين `أخضر`/`xyz`. أُضيف `ValidActionTypes` + `ValidateActionType` للمصدر
  المشترك، ووُحّد مسار الملفات عليه (نفس الرسالة حرفيًا)، وطُبّق على إضافة/تعديل الاستئناف.
- **R2 (مُتحقق آمن بلا تغيير)**: `GetRole()` الفارغ مستهلكه الوحيد `GetRoleEnum` الذي يرمي —
  لا مسار صامت. وُثّق هنا بدل التعديل.
- **أمنيًا**: وقائع التدقيق الجديدة معرّفات وعدّادات فقط؛ خلية الإكسل `InlineString` (لا حقن
  صيغ)؛ الشارات نصوص `JSX` مُهرَّبة؛ لا نقاط نهاية ولا صلاحيات جديدة.
- **الحالة النهائية**: خلفية 1432 + 361 أخضر؛ واجهة `oxlint` 0 + `tsc` نظيف + 1051 أخضر + `build`
  ناجح؛ بلا هجرات.
