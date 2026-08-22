# سجل إصلاحات التدقيق (Audit Fixes Log)

> مرجع دائم لما أُصلح وما هو معلّق من نتائج تدقيق المشروع الشامل.
> يُحدَّث هذا الملف مع كل إصلاح جديد. آخر تحديث: 2026-08-22.

---

## أولًا: الإصلاحات المنفذة

| # | الإصلاح | النطاق | أبرز الملفات | الحالة |
|---|---------|--------|--------------|--------|
| 1 | رسالة إشعار إتمام الإنابة تذكر الملف والأموال المنفذة | خلفية | `DocumentDelegationService.cs` (+helper `TargetFileLabel`)، اختباراته | ✔️ منشور (`916159c`) |
| 2 | تصميم «صفحة الكائن» لتفاصيل الملف: ترويسة هوية لاصقة + أعمدة متساوية + تبويبات جوال | واجهة | `SectionCard.tsx`, `FieldCell.tsx`, `DocumentView.tsx`, ثماني بطاقات عرض، `DocumentView.test.tsx` (+stubMobile) | ✔️ منشور (`acd13c9`) |
| 3 | سباقات الاستجابات المتأخرة (stale-response races): hook مركزي قابل للإلغاء بواجهة شبيهة `useQuery` كبوابة هجرة نحو TanStack Query | واجهة | `hooks/useCancellableRequest.ts` (+11 اختبارًا)، تهجير `Dashboard.tsx` كاملًا، تحديث تأكيداته | ✔️ مكتمل محليًا |
| 4 | طلب API لكل ضغطة مفتاح في سجل التدقيق: debounce مركزي + دفاع مزدوج مع الإلغاء + إعادة ضبط الصفحة عند تغيير الفلاتر + ربط `label htmlFor/id/name` وحلقات تركيز | واجهة | `hooks/useDebouncedValue.ts` (+6 اختبارات)، `AuditLogs.tsx`, `AuditLogs.test.tsx` (+2) | ✔️ مكتمل محليًا |
| 5 | ابتلاع أخطاء التحديث الخلفي في قائمة الملفات: نمط stale-while-revalidate — الصفوف القديمة تبقى + شريط `role="status"` مع «إعادة المحاولة»، وفشل الجلب الأول ⇒ كتلة `role="alert"` كاملة | واجهة | `DocumentsList.tsx`, `DocumentsList.test.tsx` (+2، وإكمال mock بـ`getApiErrorMessage`) | ✔️ مكتمل محليًا |
| 6 | مؤقتات `setTimeout` بلا تنظيف في مودالي النقل والتنبيه: hook تصريحي يلغي تلقائيًا عند unmount أو تغيّر الحالة | واجهة | `hooks/useTimeout.ts` (+5)، `TransferDocumentModal.tsx/.test.tsx` (+2)، `FileAlertModal.tsx/.test.tsx` (جديد ×4) | ✔️ مكتمل محليًا |
| 7 | توافق كلمات مرور قديمة بلا ملح: صيغة معيارية ذاتية الوصف `$docgen$v1$<iters>$salt$hash` (600k) + ترقية شفافة عند أول دخول ناجح داخل معاملة الدخول + سجل تدقيق، ودون لمس `token_version` | خلفية | `IPasswordHasher.cs`, `PasswordHasher.cs`, `AuthService.cs`, `FastTestPasswordHasher.cs`, `SecurityTests.cs` (+8)، `AuthIntegrationTests.cs` (+2) | ✔️ مكتمل محليًا |
| 8 | عزل بقايا المشروع القديم الحساسة (سطح تسريب): نقل آمن بخارج مستودعات git مع حماية `.gitignore` وتحقق سلامة | بنية المستودع | نقل 5 ملفات من جذر `working 3.8 32b` إلى `%USERPROFILE%\Documents\docgen-legacy-archive\`، إضافة `.gitignore`, تحديث هذا السجل | ✔️ مكتمل محليًا |
| 9 | عدم اتساق حماية null: تطبيع استجابة الخادم عند «حد الثقة» مرة واحدة بدل تشتيت `?.` — `normalizeDocumentResponse` يضمن القوائم الثماني بنوع `NormalizedDocumentResponse`، واشتقاق `assets` واحد في نافذة الإتمام (كشف أثناء الاختبار موضع انهيار رابع في `delegationAssetsLine` وعولج بتمرير النسخة المطبعة إلى `DelegationDetails`) + اختبا انحدار لاستجابة ناقصة | واجهة | `utils/apiNormalization.ts` (جديد)، `DocumentForm.tsx`, `CompleteDelegationModal.tsx`, اختبارا الملفين (+2) | ✔️ مكتمل محليًا |

**نتائج التحقق بعد آخر إصلاح:** `dotnet test`: Application ‏548/548، Api ‏248/248 — `oxlint/tsc/vitest/build`: أخضر بالكامل (‏557/557).

---

## ثانيًا: المعلّقات (مرتبة حسب الأولوية)

### أمان / موثوقية
1. ~~**تهجير بقية مسارات القراءة إلى `useCancellableRequest`**~~ — **✔️ منجز بالكامل**: هُجرت كل الصفحات والمكوّنات (`Dashboard`, `AuditLogs`, `DocumentsList`, `DelegationRequests`, `UsersActivity`, `TransferDocumentModal`, `Rotation`, `BranchesManagement`, `ArchivedDocumentsList`, `DocumentView` بجلبَيه مع تطبيع الاستجابة). مودالات الإنابة الثلاثة (BaseNumbers/Assign/DelegationForm) بقيت على علم `cancelled` القديم المتزامن — آمنة ومتسقة، وتوحيد اختياري عند أول لمسة لها.
2. ~~**تنظيف أرشيف جذر المستودع**: `documents.db.enc`, `db_crypto.py`, `config.ini` (هاشات بلا ملح), `test_password_123`, `react-dotnet-app.rar` (106MB)~~ — **✔️ منجز**: نُقلت الخمسة بسلامة (تحقق SHA-256 قبل/بعد) إلى `⁦%USERPROFILE%\Documents\docgen-legacy-archive\⁩` خارج أي مستودع، وأُضيف `⁦.gitignore⁩` في جذر المشروع القديم يمنع عودتها، وتأكد أن التطبيق الفعّال لا يشير إليها إطلاقًا (المرجع الوحيد في `FIXES_LOG.md` ذاته).
3. **قرار منتج معلّق**: سياسة موعد نهائي لحسابات خاملة ما تزال على صيغة كلمة مرور تاريخية (قفل أو إجبار إعادة تعيين بعد X شهر خمول). آلية الترقية الشفافة (#7) تغطي كل من يسجل دخولًا فقط.

### أداء / بنية
4. **تفكيك `DocumentService.cs`** (2682 سطرًا) — **تقدم جزئي منجز**: المرحلة 1 تقسيم ميكانيكي إلى 5 partials ‏(Core/Search/Status/Actions/Apply — commit `d5ac941`)، والمرحلة 2 استخراج النقيين `DocumentValidator` و`AssetMapper` مع 15 اختبار وحدة جديدة (commit `54b1425`). **المتبقي (مرحلة 3)**: استخراج متعاونين حالة‌يين خلف واجهات (`ExecutionActionService`, `StatusTransitionService`) واحدًا واحدًا عند أول لمسة تطويرية لمنطقه — **معلَّم في الكود نفسه** بملاحظة أعلى `DocumentService.Status.cs` و`DocumentService.Actions.cs`.
5. ~~**سقف أو بث لتصدير Excel**~~ — **✔️ منجز (الخيار A)**: سقف قابل للضبط `Export:MaxRows` افتراضيًا 10,000، مع عدّ مسبق (`CountExportAsync`) قبل جلب أي صف ورفض برسالة عربية واضحة؛ البث الحقيقي (OpenXmlWriter + AsAsyncEnumerable) مسجل كبديل مستقبلي حين يصبح السقف قيدًا تجاريًا (commit `4da64a4`).
6. **[منجز — الخيار 1: الخلفية مصدر الحقيقة]** توحيد خرائط حالات الملف: `DocumentStatusResolver` في Common يشتق `DisplayStatus` على `DocumentResponse` (عبر عقد `IDocumentExecutionState` الذي يحققه الكيان والـDTO معًا)، و`ExcelExportService.StatusText` مفوَّض إليه — حُذف التكرار من مصدره. الواجهة تستهلك `displayStatus` أولًا مع مسار احتياطي مؤقت للبيانات العتيقة/الـmocks (يُحذف مع #12). اختبارات مصفوفة على الـresolver (+4). commits: `cce4b76`, `432c2e9`.

### جودة واجهة
7. **a11y تسجيل الدخول**: `autocomplete="username"/"current-password"` وإزالة `autoFocus` غير المبرر.
8. **درج الجوال**: إغلاق بمفتاح Escape + focus trap.
9. ~~**جدول `UsersActivity`**: غلاف `overflow-x-auto` على المقاسات الصغيرة~~ — **✔️ منجز**: النمط الكامل المعياري (غلاف تمرير + `min-w-[36rem]` للجدول + `tabular-nums whitespace-nowrap` للعدّادات) مع أول ملف اختبار للصفحة (3 حالات: صفوف/فراغ/خطأ). الترقية إلى بطاقات جوال تبقى مرشحة شرطية إن زادت الأعمدة مستقبلًا.
10. **[منجز]** مركزية helper ‏`stubMobile` في `src/test/stubMobile.ts` — نسخة كاملة بمعامل افتراضي `false` (مكتبي)، و6 ملفات اختبار هُجرت للاستيراد، مع جعل القصد صريحًا (`stubMobile(true)`) في المواضع التي كانت تعتمد نسخة «جوال ضمنيًا» — حذف 108 أسطر مكررة (commit `7bd85c0`).

### هجرة مستقبلية (باب مفتوح لا التزام)
11. عند تحقق أحد المحفزات (بيانات مشتركة بين شاشات، polling للتنبيهات، شبكات إبطال معقدة بعد الكتابة، optimistic updates، infinite scroll) ⇒ إدخال **TanStack Query** والتهجير مكوّنًا مكوّنًا — واجهة الـhooks الحالية (`{ data, isLoading, error, refetch, setData }`) مطابقة له عمدًا.
12. **اعتماد Zod للتحقق المخطط على حدود الـAPI** عند أول توسعة كبيرة أو مع هجرة TanStack Query — يُلغي التطبيع اليدوي في `apiNormalization.ts` وfetchers ويجعل العقود مُتحققًا منها وقت التشغيل.

---

## ثالثًا: ملاحظات نشر

- **لا هجرات قاعدة بيانات** في أيٍّ من الإصلاحات 1–7 (لا تغيير على مخطط SQLite/Postgres) — لا حاجة لـ`dotnet ef database update` بسببها.
- الإصلاح #7 يتطلب نشر الخلفية فقط؛ الترقية تجري تلقائيًا لكل حساب عند أول دخول ناجح بعده.
- المرجع الدائم لنشر الهجرات حين تطرأ: `react-dotnet-app/RUN_GUIDE.md` §9.
