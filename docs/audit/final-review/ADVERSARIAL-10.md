# السيناريوهات العدائية العشرة (المرحلة 9 — أحكام من الكود)

**التاريخ:** 2026-10-16 — الفرع `feature/final-review-v2`.
**التقسيم مقابل المرحلة 2:** هناك منهجية شاملة (كل مسار)؛ هنا سيناريوهات مركبة عابرة للأدوار.

| # | السيناريو | الحكم | الدليل |
|---|---|---|---|
| 1 | محامٍ يبدّل `id` لملف زميله | **يصمد** (`403`) | `CanAccess/OrFollow + Forbid` في 20 موضعًا (`DocumentsController.cs:300-822`) + `DocumentScopeIsolationTests` |
| 2 | مندوب يطلب ملفات جهة أخرى | **يصمد** (`403` + تسجيل) | `EntityManagerPortalGuard` (بوابته فقط) + `PortalService.cs:128` + `EntityDelegateService.cs:179` + `PortalScopingTests` |
| 3 | رئيس قسم يعتمد إنابة خارج فرعه | **يصمد** (رفض) | `EnsureApprover`: الدور + تطابق الفرع + نطاق الشعبة (`DocumentDelegationService.cs:613-628`) |
| 4 | نقل ملكية ملف خارج النطاق | **يصمد** (رفض) | `TransferBranchGuardTests` + تمرير `BranchId` من الـ `claims` في كل نقل |
| 5 | إعادة `POST` مكررة (نقرة مزدوجة/إعادة إرسال) | **يصمد** (إعادة تشغيل آمنة) | `RF011IdempotencyTests` + `IdempotentReplayException` + `RF001DuplicateRequestCharacterizationTests` |
| 6 | توكن قديم بعد تغيير دور المستخدم | **يصمد** (إبطال) | `StaleRoleRevocationTests` + `TokenRevocationIntegrationTests` |
| 7 | `section_id` مزور في الطلب | **يصمد** (رفض) | الشعبة تُقرأ من المستخدم المحمَّل قاعديًا لا من الطلب (`:624-627` + `ForumService.cs:323` نمطًا) |
| 8 | تصدير ضخم متزامن (إغراق) | **يصمد** (خنق + سقف) | `ExpensivePolicy` + `ExportConcurrencyGuard` + `MaxRows` + `ExportConcurrencyGuardTests` |
| 9 | تاريخ مستحيل `30/02/2026` | **يصمد** (رفض برسالة الحقل) | `ActionDateParser` الصارم (5 صيغ) → `null` → `"{اسم الحقل} غير صالح — استخدم مثال: 1/8/2026"` + `RF014DateAcceptanceTests` |
| 10 | انقطاع أثناء `TransferAllAsync` | **يصمد** (ذرّية + عدم تكرار) | `TransactionRunner` + `TransferConcurrencyTests` + `AppealTransferAllConcurrencyTests` + مفاتيح عدم التكرار |

**الخلاصة:** 10/10 تصمد — لا سيناريو مخترِق مثبت من الكود.
