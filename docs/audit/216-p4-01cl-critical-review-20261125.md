# `P4-01cl` — مراجعة نقدية أولى (سطرًا بسطر) — 2026-11-25

> **نطاق الوثيقة:** أسئلة المراجعة النقدية الإلزامية قبل الدمج على تحويل قراءات كتب
> المطالعة الست إلى Query Handlers (`docs/audit/215-p4-01cl-review-letter-reads-20261125.md`).
> كل نتيجة مسنودة إلى فحص آلي أو مقارنة نصية موثّقة، لا إلى انطباع.

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل أُضيفت كتابة أو معاملة إلى أي استعلام؟ | فحص نصي للملفات الستة عن `SaveChangesAsync`/`AddAsync`/`Add(`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/`RunAsync` | لا — 0 مطابقة |
| 2 | هل أُضيف وسم تدقيق (`IAuditedRequest`) أو تدقيق صريح؟ | فحص `IAuditedRequest`/`IAuditLogger`/`LogAsync` | لا — 0 مطابقة |
| 3 | هل تغيّر ترتيب الحرّاس أو شروط النطاق في المتحكم؟ | `git diff` للمتحكم | لا — لم يتغيّر أي سطر حراسة؛ التغيير في مصدر القيمة فقط |
| 4 | هل تغيّر سطح `IReviewLetterService` بأكثر من الحذف المقصود؟ | عدّ الأعضاء قبل/بعد | 6 → 0؛ الواجهة صارت فارغة فحُذفت، ولم تُضف/تُعدّل أي عضو آخر |
| 5 | هل بقي تعريف مزدوج لأي دالة قراءة على الخدمة؟ | بحث عن `SearchAsync`/`GetByIdAsync`/`CountPendingForHeadAsync`/`CountUnseenRepliesForLawyerAsync`/`ListByDocumentAsync`/`GetAdministrativeBranchesAsync` كأعضاء خدمة في `src` | 0 — التعريفات الوحيدة في المعالجات (والخدمة لا تحتويها) |
| 6 | هل تكرار مساعدات التحويل/الوصول بين الخدمة والمعالجات؟ | بحث عن تعريف `ToDto`/`ToListItem`/`FileContextOf`/`CanViewAsync`/`InFileScopeAsync`/`OwnerSectionOfAsync`/`FileOwnerSectionAsync`/`FollowsDocumentAsync` | تعريف واحد لكل منها: `CanViewAsync` و`InFileScopeAsync` في معالجَيهما فقط (أُزيلتا من الخدمة)، والبقية `internal static` على الخدمة يستدعيها الطرفان |
| 7 | هل صار أي حقل أو معامل منشئ ميتًا في الخدمة؟ | عدّ استخدام كل حقل بعد النقل | 12/12 مستخدمة؛ وحُذفت `_uow`/`_tx`/`_audit` (كانت ميتة أصلًا: تعريف وإسناد فقط) فصار المنشئ 12 معاملًا |
| 8 | هل بقي مستدعٍ للأسطح الستة على الخدمة في `src`؟ | بحث شامل | 0 — المتحكم يحقن ستة معالجات |
| 9 | هل كل معالج مسجَّل في `DI`؟ | عدّ آلي: 143 تسجيلًا مقابل 143 صنف معالج | 143/143/0 بلا تكرار ولا صنف غير مسجَّل |
| 10 | هل تغيّر مسار/صلاحية/شكل استجابة أي نقطة؟ | مقارنة المتحكم + `generate:api` | لا — نفس المسارات والوسوم و`[Authorize]` والفحوص ورموز الحالة، والعميل المولَّد بلا فرق |
| 11 | هل تغيّر نطاق قراءة كتاب واحد أو كتب الملف؟ | مقارنة `CanViewAsync`/`InFileScopeAsync` والاستعلامات | لا — نفس الشروط والترتيب، ونفس `KeyNotFoundException`/`UnauthorizedAccessException` |
| 12 | هل تغيّر استخدام الساعة/المنطقة الزمنية؟ | مقارنة `FileContextOf` و`ToDto`/`ToListItem` | لا — `ServerClock.CurrentYear(clock, timeZone)` بنفس المواضع، والقيم تُمرَّر من نفس السجلات المحقونة |
| 13 | هل صار partial فارغًا أو بقي ملف ميت؟ | عدّ أعضاء كل partial + `git rm` للملف المُفرَّغ | لا — `Queries.cs` حُذف بعد إفراغه؛ `Access`/`Compose`/`Formatting`/الرئيسي غير فارغة |
| 14 | هل أُضيفت هجرة أو تغيّر النموذج؟ | `ef migrations has-pending-model-changes` | لا — نظيف |
| 15 | هل تغيّر أي توقّع اختباري؟ | تحويل مواضع النداء عبر جسر بنفس التوقيعات | لا — 40 موضعًا بلا تعديل توقّع واحد |

## مطابقة الأسطر (ملخص)

| الدالة | الوالد | المعالج | `diffs` بعد التطبيع |
|---|---|---|---|
| `SearchAsync` | 33 | 38 | 0 |
| `GetByIdAsync` | 5 | 6 | 0 |
| `CountPendingForHeadAsync` | 1 | 1 | 0 |
| `CountUnseenRepliesForLawyerAsync` | 1 | 1 | 0 |
| `GetAdministrativeBranchesAsync` | 1 | 1 | 0 |
| `ListByDocumentAsync` | 14 | 17 | 0 |
| `CanViewAsync` | 19 | 22 | 0 |
| `InFileScopeAsync` | 10 | 10 | 0 |
| **الإجمالي** | **84** | **96** | **0** |

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإعادة تسمية
> `_letters`/`_documents`/`_circuits`/`_appeals`/`_delegations` ببارامترات، وإسقاط
> تأهيل `ReviewLetterService.`، ثم مقارنة تدفّق الرموز. فروق الأسطر كلها تنسيقية أو
> ناتجة عن تمرير الاعتماديات صراحةً (موثّقة أعلاه).

## النصوص الحرفية اتجاهان

المقطع المنقول يحوي **7** نصوص حرفية مميزة (رسائل الاستثناءات الثلاث في البحث ورسائل
الكتاب/الملف/الصلاحية)، والنتيجة `missing = 0` و`added = 0`.

## سطح الخدمة

`IReviewLetterService`: **6 → 0 → محذوفة**. تسجيلات `DI` المرتبطة بها: 1 → 0.
`ReviewLetterService` بقي تنفيذًا لعقدَي مساعدات الكتابة فقط
(`IReviewLetterReplyWriteRules` و`IReviewLetterCreateWriteRules`)، وحقوله 15 → 12.
