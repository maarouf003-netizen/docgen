# مصفوفة التفويض (المرحلة 2 — `endpoint-by-endpoint`)

**التاريخ:** 2026-10-16 — الفرع `feature/final-review-v2`.
**المنهج:** بوابة الصنف + تضييق كل إجراء + فحص النطاق على مستوى الكائن (`IDOR`).
**النتيجة:** 21/21 متحكمًا مغطاة دوريًا؛ النطاق الكائني مثبت في المسارات الحرجة؛ **صفر ثغرات مفتوحة**.

## 1) بوابة الدور (سمة `[Authorize]`)

| المتحكم | بوابة الصنف | تضييق الإجراءات | الحكم |
|---|---|---|---|
| `Appeals` (~35 مسارًا) | 5 أدوار (بلا مندوب) | `lawyer` فقط عند `554` | ✅ |
| `Documents` | 5 أدوار | — | ✅ + نطاق كائني (20 نقطة `CanAccess/OrFollow + Forbid`، `DocumentsController.cs:117-141,300-822`) |
| `Delegations` (12) | `[Authorize]` عامة | 12/12 بالأدوار الخمسة | ✅ + نطاق خدمي (أدناه) |
| `Statistics` (9) | `[Authorize]` عامة | 9/9 (إدارة/رؤساء/محامٍ حسب الحساسية) | ✅ + `own branch` (`:39`) |
| `ExecutionCircuits` (11) | `[Authorize]` عامة | 11/11 (`HeadOrSubHead` + أدوار) | ✅ + `own branch` (`:52,169,184`) |
| `PendingRegistrations` (2، نفس الملف، مسار `api/documents`) | `[Authorize]` عامة | 2/2 (`lawyer`) | ✅ مع ملاحظة صيانة (§3) |
| `Branches` (5) | 5 أدوار | `CanManageBranches → Forbid` على `POST/PUT/DELETE` (`:38,62,82`) | ✅ |
| `Sections` (5) | 5 أدوار | `CanManage → Forbid` على 4 إجراءات معدِّلة (`:47,75,96,113`) | ✅ |
| `UserManagement` | `head,subhead,manager,admin` | الرؤساء لنطاق فرعهم (`:95,126,155,177`) | ✅ |
| `AuditLogs` | `manager,admin,head,subhead` | `GET` فقط + الرئيس لفرعه (`:33`) | ✅ |
| `Correspondences` | 5 أدوار | `BranchId` من الـ `claims` (`:32`) + رفض خدمي (`UnauthorizedAccessException`) | ✅ |
| `ReviewLetters` | 5 أدوار | نفس النمط (`:33` + رفض خدمي) | ✅ |
| `Forum` | 5 أدوار | رفض خدمي (كاتب/مشرف/رئيس) | ✅ |
| `EntityRegistry` | 5 أدوار | فاعل بالفرع (`:29`) + رفض خدمي | ✅ |
| `Portal` | `entitymanager` (+ قسم `manager…` عند `:306`) | رفض خدمي (`:128`) + نطاق محافظة (`:179`) | ✅ |
| `HeadSuccession` | `manager,admin` | — | ✅ |
| `PersonalReminders` | `lawyer` | مالكية `LawyerId == userId` في كل عملية (`:54,105,168,199`) | ✅ |
| `Alerts` | `lawyer,head,subhead` | استعلامات بفرع المستخدم | ✅ |
| `AppSuggestions` | 5 أدوار | — | ✅ |
| `ClientErrors` | كل الأدوار + مندوب | إبلاغ أخطاء بالتصميم | ✅ (مقصود) |
| `Auth` | `login/logout` مجهولان؛ الباقي كل الأدوار | خنق الدخول (`LoginIpPolicy` `:47`) | ✅ |
| `Meta` | `current-year` مجهول (موثق كعام) | — | ✅ (مقصود) |

## 2) النطاق الخدمي للإنابات (أعلى مخاطر `IDOR` بعد المستندات)

| العملية | الحارس | الدليل |
|---|---|---|
| إنشاء/تعديل/حذف | `EnsurePendingByOwner` (معلّقة + مالك المصدر) | `:200,321,1232-1238` |
| اعتماد/رفض | `EnsureApprover` (رئيس/شعبة فرع الدائرة المنابة) | `:419,837,613-628` |
| توجيه/تراجع توجيه | رئيس فرع الدائرة (مستخدم مُحمَّل من القاعدة) | `:670-674,752-757` |
| تسجيل/إتمام | مالكية الملف المناب `CreatedById == userId` (من `JWT` الموقّع) | `:907-908,1030-1031` |

## 3) ملاحظتان (لا ثغرات)

- **مخاطر صيانة:** `PendingRegistrationsController` بمسار `api/documents` وبوابة صنف عامة — أي إجراء مستقبلي بلا تضييق يصل للمندوب (يحميه اليوم `EntityManagerPortalGuard`). توصية: تضييق الصنف إلى `lawyer` عند أول تعديل قادم.
- **خلل تجميلي مرصود عرضًا:** رسائل عربية مشفرة خطأً (`mojibake`) في `PersonalReminderService.cs` (`:71,75`…) — خارج نطاق الخطة، يُسجَّل للدفعة القادمة.
