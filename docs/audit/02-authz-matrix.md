# مصفوفة التفويض — `02-authz-matrix.md`

> من الأدوار الموجودة **فعلًا في الكود** مقابل أدوار السياق. كل خلية بدليل `path:line` أو `UNKNOWN`.
> `A` = `ALLOW`، `D` = `DENY`، `C` = `CONDITIONAL` (يُذكر الشرط)، `U` = `UNKNOWN`.
> العمود الأخير: هل الإنفاذ خادميًا (`yes`) أم واجهة فقط (`frontend only`) — كل المصفوفة `yes` إلا ما يُذكر.
> المصدر المركزي: `backend/src/DocGenerator.Api/Authorization/RolePermissions.cs:1-142`.
> الأدوار النصية في التوكن: `lawyer/head/manager/admin/entitymanager`
> (`Infrastructure/Security/TokenService.cs:34` — `ToString().ToLowerInvariant()`، VERIFIED).

## 1. الأدوار: الكود مقابل السياق

| الدور في السياق | الدور في الكود (`Domain/Enums/Enums.cs:3-15`) | الفرق |
|---|---|---|
| محامي (`Lawyer`) | `Lawyer=1` | لا فرق |
| رئيس القسم (`Head`) | `Head=2` | لا فرق |
| المدير (`Manager`) | `Manager=3` | **فرق**: السياق قال «صلاحيات واسعة» لكن الكود يمنعه من رؤية المحذوفات (`RolePermissions.cs:24-25`) — انظر `BQ-001` |
| المشرف (`Supervisor`) | `Admin=4` (الاسم الكودي `Admin`) | **فرق تسمية**: السياق يسميه «مشرف» والكود يسميه `Admin`؛ وهو الوحيد الذي يدير المستخدمين/الفروع (`RolePermissions.cs:46-50`) — انظر `BQ-001` |
| مندوب الجهة (`entitymanager`) | `EntityManager=5` | لا فرق؛ معزول بنيويًا بالحارس (`Middleware/EntityManagerPortalGuard.cs:20-40`) |

## 2. المصفوفة (قراءة R / إنشاء C / تحديث U / حذف Del / إسناد As / إغلاق Cl / إعادة فتح Re / تصدير Ex)

| المورد | `manager` | `admin` | `head` | `lawyer` | `entitymanager` | إنفاذ خادمي؟ |
|---|---|---|---|---|---|---|
| ملفات: قراءة | C كل الفروع بلا كتابة (`RolePermissions.cs:38,104`) | C كالمدير + يرى المحذوفات (`:24`) | C فرعه فقط (`DocumentsController.cs:107-108` نطاق `visibleBranch`) | C ملكه + ما يُتابعه (`DocumentsController.cs:80-92`) | D خارج البوابة (`EntityManagerPortalGuard.cs:34`)؛ C داخل بوابته (`PortalController.cs:20`) | yes |
| ملفات: إنشاء/تعديل | D (`CanEditDocuments` محامٍ فقط `:12`) | D (`:12`) | D (`:12`) | A ملكه (`:12`) | D | yes |
| ملفات: حالة/إغلاق/شطب/تجديد | D (`CanChangeDocumentStatus` محامٍ فقط `:15`) | D (`:15`) | D (`:15`) | A ملكه (`:15`) | D | yes |
| ملفات: حذف/استعادة منطقية | D (`CanDeleteDocuments` محامٍ فقط `:18`) | D (`:18`) | D (`:18`) | A ملكه (`:18`) | D | yes |
| ملفات: رؤية المحذوفات | D مستبعد صراحةً (`:24-25`) | A (`:24`) | C فرعه (`:24`) | C ملكه (`:24`) | D | yes |
| ملفات: نقل بين المحامين | D (`CanTransferDocuments` رئيس فقط `:41`) | D (`:41`) | A فرعه (`:41` + `DocumentsController.cs:543-549`) | D (`:41`) | D | yes |
| ملفات: عدادات مشاهدة/طباعة | A (`CanViewCounters :33-35`) | A (`:33-35`) | A (`:33-35`) | D (تُصفَّر له `DocumentsController.cs:61-78`) | D | yes |
| ملفات: تصدير `Excel` داخلي | A (قراءة شاملة) | A | C فرعه | C ملكه/نطاقه | D (بوابته لها تصديرها الخاص `PortalController.cs:132`) | yes — لكن **بلا تدقيق** (`SEC-002`) |
| ملفات: توليد `Word` | C قراءة (يولّد لما يراه؟) U التفصيل | C U | C فرعه | A ملكه | D | yes — لكن **بلا تدقيق** (`SEC-001`) |
| استئنافات: تسطير/حسم/شطب | D (`CanManageAppeals` محامٍ فقط `:71`) | D (`:71`) | D (`:71`) | A ملكه (`:71`) | D | yes |
| استئنافات: إسناد/نقل | D (`CanAssignAppeals` رئيس فقط `:76`) | D (`:76`) | A فرعه (`:76`) | D (`:76`) | D | yes |
| إنابات: تسطير | D (`CanManageDelegations` محامٍ فقط `:59`) | D (`:59`) | D (`:59`) | A قبل الاعتماد (`:59`) | D | yes |
| إنابات: اعتماد/إسناد | D (`CanApproveDelegations` رئيس فقط `:65`) | D (`:65`) | A فرعه (`:65`) | D (`:65`) | D | yes |
| مراسلات رئيسية | A (`:92,111`) | A (`:92`) | A فرعه (`:92`) | A (`:92`) | D مرفوض صراحةً (`CorrespondencesController.cs:70-71`) رغم شمول الدور في `:92` | yes |
| مراسلات البوابة | D | D | D | D | A كتابة بوابة فقط (`PortalController.cs:231-298`) | yes |
| كتب مطالعة: إنشاء/لاحقات | D (`CanCreateReviewLetters` محامٍ فقط `:83`) | D (`:83`) | D (`:83`) | A (`:83`) | D | yes |
| كتب مطالعة: رد | D (`CanReplyReviewLetters` رئيس فقط `:86`) | D (`:86`) | C فرعه (`:86`) | D (`:86`) | D | yes |
| سجل الجهات: إدارة | A كل السجل + دمج (`:111,121-122`) | A كالمدير (`:111,121-122`) | C مقصور على محافظة فرعه (`:107-109`؛ التنفيذ في الخدمة — NOT VERIFIED مكانيًا، انظر `SEC-017`) | D (`EntityRegistryController.cs:40` يرفض) | D | yes |
| مستخدمون: إدارة كاملة | D (`CanManageUsers` مشرف فقط `:47`) | A (`:47`) | D (`:47`) | D | D | yes |
| مستخدمون: محامو الفرع | D | A (`:44`) | C فرعه — يُجبر على فرعه (`UserManagementController.cs:86,102,132,154`) | D | D | yes |
| مندوبون: إدارة حسابات | A (`CanManageDelegates :117-119`) | A (`:117-119`) | C نطاقه (`:117-119`) | D | D | yes |
| فروع: إدارة | D (`CanManageBranches` مشرف فقط `:50`) | A (`:50`) | D (`:50`) | D (`:50`) | D | yes |
| سجل التدقيق: قراءة | A (`AuditLogsController.cs:11` ضمن `Roles`) | A (`:11`) | A (`:11`) بلا نطاق فرع — **تسرب** (`SEC-013`) | D (مستبعد من `Roles`) | D (الحارس + غياب) | yes |
| إحصاءات | A (`StatisticsController.cs:47,85`) | A (`:47,85`) | C جزئي (`:47,95`) | C جزئي + تذكيراته (`:47,78,146`) | D | yes |
| بوابة المندوب | D (`PortalController.cs:20` حصري) | D (`:20`) | D (`:20`) | D (`:20`) | A قراءة + إكسل + مراسلاته (`:117,132,162-298`) | yes + حارس عمق (`EntityManagerPortalGuard.cs:20-40`) |
| تذكيرات شخصية | D (محامٍ فقط `:126`) | D (`:126`) | D (`:126`) | A ملكه (`:126`) | D | yes |
| اقتراحات: قراءة/تعليم | D (`CanViewAppSuggestions` مشرف فقط `:129`) | A (`:129`) | D (`:129`) | D | D | yes |
| اقتراحات: إرسال | D (`CanSuggestApp` محامٍ/رئيس `:132`) | D (`:132`) | A (`:132`) | A (`:132`) | D | yes |
| تنبيهات الرؤساء | D (`CanCreateAlerts` رئيس فقط `:53`) | D (`:53`) | A فرعه (`:53`) | D (يستلم فقط) | D | yes |
| تدوير سنوي | D (`CanRotate` محامٍ فقط `:31`) | D (`:31`) | D (`:31`) | A ملكه (`:31`) | D | yes |
| سجل دوائر التنفيذ: إدارة (إدخال/تسمية/تعطيل/حذف-إفراغ/إحالة) | D (`CanManageExecutionCircuits` رئيس فقط `RolePermissions.cs:141`) | D | A فرعه (النطاق إجباري خلفيًا من `User.GetBranchId()` + شرط محافظة الفرع) — وتنبيه: `POST …/{id}/refer-files` نقل ملكية جماعي (`CreatedById` لكل ملف) بتفويض الإدارة لا `CanTransferDocuments` (صف «نقل بين المحامين») | D (اختيار فقط + معالج إعادة قيد ملفاته) | D | yes |
| سجل دوائر التنفيذ: إحصاءات | A كل الفروع (منتقي فرع) | A كالمدير | C فرعه فقط | D | D | yes |
| سجل دوائر التنفيذ: قراءة للاختيار (`GET …/for-lawyer` فرعه؛ `GET …/for-delegation` محافظته بكل الفروع) | D | D | A (`ExecutionCircuitsController.cs:142-164`) | A (`:142-164`) | D | yes |
| سجل دوائر التنفيذ: معالج إعادة القيد (`GET …/my-pending-registrations` + `POST …/complete-registrations`) | D | D | D | A ملكه حصرًا (يُتحقق `CreatedById` داخل المعاملة) | D | yes |

## 3. ملاحظات التفويض الحرجة

1. **التفويض مشتت برمجيًا لا إعلانيًا**: معظم النقاط `[Authorize]` عارٍ (184 نقطة، انظر `01-endpoints.md`)
   والفحص داخل الأكشن عبر `RolePermissions.*` + `return Forbid()` — أي أكشن جديد يُنسى فحصه ينفتح
   افتراضيًا (فشل مفتوح). الاستثناءات الإعلانية: `AuditLogsController.cs:11`
   (`Roles="manager,admin,head"`) و`PortalController.cs:20` (`Roles="entitymanager"`) وبعض أفعال
   `StatisticsController.cs:47,63,78,85,90,95,120,146,168` و`AppealsController.cs:408-409`
   (`Roles="lawyer"` للتذكيرات) — VERIFIED — وأفعال سجل الدوائر الإحدى عشرة
   (`ExecutionCircuitsController.cs:45,61,82,102,122,143,153,167,209,237,244`) — VERIFIED.
2. **الدفاع العميق للبوابة سليم التصميم**: حتى لو نسي متحكم داخلي فحص الدور،
   `EntityManagerPortalGuard.cs:20-40` يحصر `entitymanager` في
   `/api/portal|/api/auth/me|/api/auth/logout|/api/client-errors|/api/meta` — VERIFIED.
   لكن رفضه **بلا تدقيق** (`SEC-007`).
3. **فجوات تحتاج إثباتًا حيًا** (NOT VERIFIED — في خطة الاختبار): ملكية التعديل داخل خدمات
   الاستئناف/الإنابة (`SEC-016`)، وقيد المحافظة لرئيس القسم في السجل (`SEC-017`)، ونافذة
   `branch_id` المخبوز بعد النقل (اختبار T-5 في `02-security.md`).
4. **خلايا `U` المتبقية**: تفصيل «المحامي يولّد لمن يرى قراءةً» (هل التوليد لملفات المتابعة مسموح؟)،
   وقراءة المدير/المشرف لكتب المطالعة (كتابة `D` مؤكدة، القراءة `U` — تُحسم بالاختبار T-12).
