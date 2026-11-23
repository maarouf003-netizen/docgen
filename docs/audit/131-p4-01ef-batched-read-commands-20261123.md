# `P4-01e/f` — مرحلتان مجمّعتان: تعليم التنبيه + تعليم الاقتراح — 2026-11-23

> **قرار التجميع:** مرحلتان مستقلتان (Alerts/Suggestions) بملفات منفصلة، فجُمِّعتا في PR
> واحد لتوفير دورة تحقق، مع مراجعة نقدية منفصلة وفحص سلوك لكل منهما.

## `P4-01e` — `MarkRead` التنبيه

| قبل | بعد |
|---|---|
| `HeadAlertService.MarkReadAsync` (فحص الدور + تحديث ذرّي في المستودع) | `MarkHeadAlertReadCommand` + معالج يملك الفحص والاستدعاء الذرّي |
| ضمن عقد `IHeadAlertService` | أُزيل من العقد (لا مستدعي غير `AlertsController`) |

- بلا معاملة (الأصل أيضًا): `IHeadAlertRepository.TryMarkReadAsync` تحديث شرطي ذرّي.
- السلوك محفوظ: `UnauthorizedAccessException` لدور غير مستلم، `false` لغير المستلم (404)،
  و`DateTime.UtcNow` نفسه.

## `P4-01f` — `MarkRead` اقتراح التطوير

| قبل | بعد |
|---|---|
| `AppSuggestionService.MarkReadAsync` تفتح المعاملة وتكتب وتُدقّق | `MarkAppSuggestionReadCommand` + معالج يملك المعاملة والتدقيق |
| ضمن عقد `IAppSuggestionService` | أُزيل من العقد (لا مستدعي غير المتحكم) |

- السلوك محفوظ: `false` للغياب، `true` بلا كتابة إن كان مقروءًا، رسالة التدقيق نفسها
  (`app-suggestion.read`).

## المراجعة النقدية (أخطاء اكتُشفت وأُصلحت)

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **إتلاف عقد `IHeadAlertService`** أثناء حذف الإعلان | استُبدل مقطع خاطئ فأُدخل توقيع `CreateAsync` داخل الواجهة | أُعيد المقطع صحيحًا (Create + RemoveAppealRecipient) قبل البناء |
| 2 | `RolePermissions` في نطاق مختلف | `CS0103` في معالج التنبيه | `using DocGenerator.Application.Common` (لا Security) |
| 3 | اختبارات تستدعي الخدمة المحذوفة | 4 ملفات (HeadAlert/Permission/SubHead/Dispatcher) | بُنيت المعالجات في الاختبارات واستُبدلت 12 استدعاءً؛ وأُزيلت طريقة التمرير من غلاف الاختبار |
| 4 | دلالة «لا تغيير» | `MarkRead` الاقتراح يعود بلا معاملة عند `IsRead` | الحفظ داخل النواة فقط بعد التغيير |
| 5 | بناء/تنسيق | `build -warnaserror` و`format` | 0/0 ونظيف |

## التحقق (دورة واحدة)

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| اختبارات مركزة (HeadAlert/Permission/SubHead/AppSuggestion) | **0 نجح / 65 متجاوز (Postgres-gated) / 0 فشل** |
| `Application` (بلا PostgreSQL) | **757 نجح / 1655 متجاوز / 0 فشل** |
| `Api` غير التسلسلي | **901 نجح / 615 متجاوز / 0 فشل** |
| `Api` التسلسلي | **0 نجح / 5 متجاوز / 0 فشل** |
| الحرّاس الستة | نظيفة |

> اختبارات المسارات المركزة مشروطة بـ`PostgresFact`، وتُؤكَّد في بوابة `backend-postgres` على CI.

## الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
