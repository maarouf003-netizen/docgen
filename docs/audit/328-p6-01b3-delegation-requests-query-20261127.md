# `P6-01b/3` — الشريحة الثالثة `TanStack Query`: `DelegationRequests` — 2026-11-27

> ترحيل `frontend/src/pages/DelegationRequests.tsx` إلى `useQuery` + `useMutation`
> + `invalidateQueries`: أول شريحة تثبت نمط **قراءة + كتابة** لا القراءة وحدها،
> بسلوك مكافئ واختبارات للفشل والتخزين. بلا تغيير `API`/هجرات.

## 1. النتيجة

| العنصر | قبل | بعد |
|---|---|---|
| قائمة الطلبات | `useCancellableRequest([rejectedOnly])` | `useQuery(['delegation-requests', rejectedOnly])` + `keepPreviousData` |
| شعب الفرع | `useEffect` يدوي + `setSections` | `useQuery(['sections', branchId])` مع `enabled: isHead && branchId != null` |
| الأفعال (توجيه/تراجع/رفض) | `api.post` داخل `try/catch` + `refetch()` | `useMutation` مع `onMutate` (تعطيل الصف) و`onSettled` و`invalidateQueries` |
| اعتماد الإنابة | `refetch()` يدوي | `invalidateQueries(['delegation-requests'])` |
| الخطأ | من الخطّاف (مطبَّع) | `getApiErrorMessage(query.error)` |
| الاختبارات | 9 | **11** (+ إعادة جلب بعد الفعل، + عدم تكرار مفتاح الفلتر) |

الملف **301 سطر** (كان 273)؛ والحارس نظيف مع **0** ملف مجمّد.

## 2. ما نُقل حرفيًا

- مسار `GET /delegations/pending` مع `rejectedOnly` وشرط `Array.isArray`.
- مسارات الأفعال: `/delegations/:id/redirect|recall-redirect|reject` وأجسامها
  (`{ sectionId }` / `{}` / `{ reason }`) ورسائل النجاح/الخطأ كما هي.
- شروط العرض: `isHead && isExternal && !redirectedToSectionId && assignedLawyerId == null`
  لزر التوجيه، و`redirectedToSectionId != null` للتراجع، وشروط زر الرفض/الاعتماد.
- نافذتا التوجيه/الرفض وحقولهما وتعطيل الأزرار أثناء الانشغال (`busyId`).
- «جارِ التحميل...» أثناء الجلب (`isFetching` — مطابق `isLoading` السابق الذي كان
  يظهر أيضًا عند تبديل الفلتر).

## 3. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `DelegationRequests.test.tsx` | **11/11** (9 قائمة + إعادة جلب بعد الفعل + عدم تكرار مفتاح مخزَّن) |
| `AuditLogs.test.tsx` (إصلاح ملحق `327`) | **7/7** |
| الحزمة الكاملة | **1945/1945** في 188 ملفًا |

## 4. E2E

أُضيفت `/delegations/requests` (رئيس قسم) إلى `axe` و`375px` — صار مجموع E2E
**31 اختبارًا** (19 سطح مكتب + 12 جوال). تُشغَّل في وظيفة `e2e` الحاجبة.

## 5. البوابات

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts e2e` / `tsc -b` | نظيفان |
| `vitest run` | **1945/1945** في 188 ملفًا |
| `build`/`budget` | نجح · إجمالي **1790.2kB** · أكبر **388.2kB** · دخول **285.5kB** |
| `guard` (CSP) / `a11y-guard` / `module-size-guard` | نظيفة (0 مجمّد) |
| `generate:api` | لا انحراف |

## 6. المتبقي في `P6-01`

- بقية الصفحات اليدوية (شريحة كل تغيير).
- توحيد مكوّنات حالات التحميل/الخطأ/الفراغ.
- `LCP/INP` (معلّق على بيئة القياس).

## 7. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
