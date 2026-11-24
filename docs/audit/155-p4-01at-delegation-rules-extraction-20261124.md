# `P4-01at` — استخراج مساعدات الإنابة المشتركة (تمهيدي) — 2026-11-24

> **قرار النطاق (خبير):** مرحلة **نقل صرف بلا تغيير سلوك** قبل نقل `CreateAsync`/`UpdateAsync`:
> استخراج المساعدات غير النقية إلى `DocumentDelegationRules` وتوسيع رؤية المساعدات النقية،
> فيصبح PR نقل الملكية التالي صغيرًا وقابلًا للمراجعة، وشبكة الاختبارات القائمة هي الدليل.

## ما تغيّر

| قبل | بعد |
|---|---|
| `ValidateAndBuildAsync` + `ResolvePendingAlertTargetAsync` + `DelegationFields` خاصة بالخدمة | في `DocumentDelegationRules` بمعاملات صريحة (`branches`/`circuits`/`users`) |
| `PruneStaleReservationsAsync` تعتمد `_reservations` | `DocumentDelegationRules.PruneStaleReservationsAsync(..., reservations, ...)` |
| مساعدات نقية `private static` | `internal static` (نفس الملفات) — `ValidateSourceForDelegation`/`ApplyDelegationAssets`/`Normalize`/`ValidateAssetsNotBlockedAsync`/`ValidateAssetsHaveSeizureDate`/`ComputeBlockedIds`/`ConsumeSnapshots`/`BlockedAssetsMessage`/`SourceLabel`/`AlertCourtAfterDaira`/`ToDto` |
| مستدعو الخدمة الثلاثة | يفوّضون إلى `DocumentDelegationRules` (موضعا Create/Update + موضع Lifecycle) |

**لم يتغير:** أي عقد واجهة، أي رسالة، أي ترتيب حفظ/تدقيق/تنبيه، أي اختبار، أي هجرة.

## المراجعة النقدية

| # | الوجد | الدليل | الإصلاح | الحالة |
|---|---|---|---|---|
| 1 | هل النقل صرف فعلًا؟ | مقارنة كل النصوص الحرفية للكتلة المنقولة مع الوالد `fc337016` | **15/15 حرفيًا، 0 مفقود** | ✅ |
| 2 | هل انحرف ترتيب؟ | مراجعة يدوية للكتل المنقولة سطرًا بسطر | لا تغيير منطقي (نفس الشروط/الرسائل/العوائد) | ✅ |
| 3 | تكرار `Normalize` | نسخة محلية في Rules | أُبقيت نسخة الخدمة `internal` فقط ولم تُكرر | ✅ |
| 4 | مستدعو `ResolvePendingAlertTargetAsync` خارج CRUD | `Lifecycle.cs:102` | حُدّث إلى Rules بمعاملات صريحة | ✅ |
| 5 | الخطأ السابق المتكرر (تعديل عقد بلا داعٍ) | `git diff` للعقد | **صفر تغيير** في أي `I*Service.cs` | ✅ |
| 6 | التنسيق | `dotnet format` كشف قوسًا مدموجًا | صُحّح قبل الالتزام | ✅ |
| 7 | تغطية السلوك | `backend-postgres` يشغّل كل اختبارات الإنابة | ستُشغَّل في البوابة | ✅ |

## التحقق

| البوابة | النتيجة |
|---|---|
| `build -warnaserror` / `format` | 0/0 ونظيف |
| `Architecture` | 29/29 |
| `Application` | 758 / 1659 / 0 |
| `Api` غير التسلسلي / التسلسلي | 901 / 615 / 0 · 0 / 5 / 0 |
| الحرّاس الستة | نظيفة |
| `DI` | **بلا تغيير** (لا معالجات جديدة) |

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.

> لم تُحرَّر اللقطات `128`–`154`.
