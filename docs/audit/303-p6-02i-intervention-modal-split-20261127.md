# `P6-02i` — تفكيك `InterventionModal` — 2026-11-27

> تفكيك `frontend/src/components/form/InterventionModal.tsx` من **650** إلى **333**
> سطرًا: منطق النموذج النقي في وحدة مستقلة، وأقسام الطرف في مكوّن عرض، بلا تغيير
> سلوكي/عقود/هجرات.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---|---:|
| `components/form/InterventionModal.tsx` | **650** | **333** |
| `components/form/intervention/InterventionPartyFields.tsx` | — | 281 |
| `components/form/intervention/interventionForm.ts` | — | 198 |
| `components/form/intervention/interventionStyles.ts` | — | 4 |
| `components/form/intervention/interventionForm.test.ts` | — | 232 |

كلها < 600، وحُذف `InterventionModal` من قائمة الحارس: **5 ⇒ 4** ملفات مجمّدة.

## 2. التوزيع

| الوحدة | المسؤولية |
|---|---|
| `interventionForm` | انتقالات الحالة (`withInterventionNature`/الممثل/الورثة/اختيار السجل)، `validateIntervention`، `buildInterventionPayload`، `interventionAddressSuppressed` |
| `InterventionPartyFields` | أقسام الشخص الطبيعي (بالممثل والورثة) والاعتباري والجهة العامة، و`NamedInput` |
| `interventionStyles` | `labelCls`/`inputCls` المشتركة |
| `InterventionModal` | الحالة، العنوان، بيانات طلب التدخل، الخطأ، أزرار الحفظ/الإعادة/الإلغاء، منتقي السجل |

## 3. ما نُقل حرفيًا

- ترتيب التحقق ورسائله الحرفية (الجهة العامة من السجل ← اسم الطرف ← تاريخ الورود ← اسم الممثل).
- تطبيع الأرقام العربية في `receiptNumber`/`receiptDate`/`acceptanceDate`، وقصّ الفراغات،
  وتجاهل `version` في الإنشاء (`version: initial?.version`).
- كبت عنوان المتدخل عند وجود ممثل شرعي أو وريث مسمّى، وتصفيره في الحمولة، والورثة `[]`
  عند الممثل.
- حفظ الصفة يبقي بيانات الطلب المشتركة ويصفّر حقول الطرف، و«إعادة تعيين» تعيد الحالة الابتدائية.
- الحمولة لا تُرسل حقول صفة أخرى (`registryId` لا يظهر في الطبيعي/الاعتباري).

## 4. تحسين وصول غير كاسر

اسم الجهة العامة كان `<label>` بلا `htmlFor` وحقلًا بلا `id`/`name`؛ أُضيف
`id="intervention-public-entity"`/`name="entityName"` وربط التسمية، وبقيت بقية
المعرّفات كما هي (عقد اختبارات النافذة لم يتغيّر).

## 5. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `InterventionModal.test.tsx` (بلا تعديل) | **7/7** |
| `DocumentView.interventions.test.tsx` + `DocumentInterventionsList.test.tsx` | **22/22** |
| `interventionForm.test.ts` جديد | **12/12** (حمولة/تحقق/انتقالات/كبت العنوان) |

## 6. البوابات (المقاسة)

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts` | نظيف |
| `tsc -b` | نظيف |
| `vitest run` | **1924/1924** في 184 ملفًا |
| `build`/`budget` | نجح · إجمالي **1748.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `generate:api` | نظيف / لا انحراف |
| `module-size-guard` | نظيف مع **4** ملفات مجمّدة |

## 7. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
