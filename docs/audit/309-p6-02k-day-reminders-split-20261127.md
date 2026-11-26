# `P6-02k` — تفكيك `DayRemindersModal` — 2026-11-27

> تفكيك `frontend/src/components/dashboard/DayRemindersModal.tsx` من **725** إلى **296**
> سطرًا: صف اليوم، نموذج التذكير الشخصي، ومنطق النموذج/التصفية — بلا تغيير
> سلوكي/عقود/هجرات.

## 1. النتيجة

| الملف | قبل | بعد |
|---|---|---:|
| `components/dashboard/DayRemindersModal.tsx` | **725** | **296** |
| `components/dashboard/dayReminders/DayReminderRow.tsx` | — | 203 |
| `components/dashboard/dayReminders/PersonalReminderForm.tsx` | — | 199 |
| `components/dashboard/dayReminders/dayRemindersModel.ts` | — | 62 |
| `components/dashboard/dayReminders/dayRemindersModel.test.ts` | — | 101 |

كلها < 600، وحُذف `DayRemindersModal` من قائمة الحارس: **3 ⇒ 2** ملفان مجمّدان.

## 2. التوزيع

| الوحدة | المسؤولية |
|---|---|
| `dayRemindersModel` | `DeletingTarget`، خيارات النهاية و`endDateFrom`، تصفية البحث (`filterOccurrences`)، مفاتيح التعطيل |
| `DayReminderRow` | صف التذكير الشخصي (تم/تعديل/حذف) وصف الملف/الاستئناف (الرابط، الشارة، تغيير اليوم، إلغاء التذكير) |
| `PersonalReminderForm` | نموذج الإنشاء/التعديل بنفس حقوله وحمولتيه (`POST`/`PUT`) |
| `DayRemindersModal` | الحصر الحواري، البحث، تأكيد الحذف، إعادة الجدولة، تمييز الإنجاز، وتركيب النموذج |

## 3. ما نُقل حرفيًا

- حصر التركيز و`Escape` وإعادة التركيز لموضعه، ولوحة `tabIndex={-1}` و`overscroll-contain`.
- البحث: تصفية العنوان/الملاحظات/اسم الملف/عنوان الاستئناف/نص الإجراء المجرَّد، بلا بحث عند الفراغ.
- مسارات إعادة الجدولة (`/documents|/appeals/:id/actions/:actionId/reschedule`) وتطبيع الأرقام العربية.
- حذف التذكير بأنواعه الثلاثة، وتعطيل الزر بمفتاح العملية، و«تم/إعادة التكرار» مع `version`.
- مفتاح `PersonalForm` (إنشاء/تعديل-آخر) لمنع تسرب المسودة، وتصفير النهاية `__clear__`.
- **التوافق:** `PersonalForm` ما زال يُصدَّر من `DayRemindersModal` (إعادة تصدير) كي لا ينكسر `CalendarPage`.

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `DayRemindersModal.test.tsx` (بلا تعديل) | **19/19** |
| `CalendarPage.test.tsx` (بلا تعديل) | **7/7** |
| `dayRemindersModel.test.ts` جديد | **6/6** (نهاية التكرار، البحث، التجريد، المفاتيح) |

## 5. البوابات (المقاسة)

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts` | نظيف |
| `tsc -b` | نظيف |
| `vitest run` | **1915/1915** في 184 ملفًا |
| `build`/`budget` | نجح · إجمالي **1748.8kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `guard` (CSP) / `generate:api` | نظيف / لا انحراف |
| `module-size-guard` | نظيف مع **2** ملفان مجمّدان |

## 6. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
