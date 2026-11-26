# `P6-01b` — أول شريحة `TanStack Query` — 2026-11-27

> إدخال `TanStack Query` رسميًا وإزالة `useEffect` اليدوي من شاشة واحدة كاملة
> (`/circuit-stats`) بسلوك مكافئ + اختبارات، مع مزوّد على مستوى `App` وعميل موحّد
> معطّل `retry` (لأن `api/client` يتولى إعادة المحاولة). لا تغيير `API`/هجرات.

## 1. الحزمة والعميل

| العنصر | القيمة |
|---|---|
| الحزمة | `@tanstack/react-query@5.108.4` (`package.json`/`package-lock.json`) |
| `src/queryClient.ts` | `createQueryClient()` + عميل التطبيق الواحد: `retry: false`، `refetchOnWindowFocus: false`، `staleTime: 30s` |
| `src/App.tsx` | `QueryClientProvider` حول `AuthProvider` (فوق كل الصفحات، وداخل حدود الخطأ) |
| `src/test/queryClient.tsx` | `createTestQueryClient()` + `renderWithQueryClient` لعميل معزول لكل اختبار |

## 2. الشريحة: `CircuitStatsPage` (171 سطرًا)

| قبل | بعد |
|---|---|
| `useEffect` لجلب الفروع + `useCallback`/`useEffect` للإحصاءات، وحالة `loading/error/rows` يدوية | استعلامان: `['branches']` (مفعّل لغير رئيس القسم/الشعبة) و`['circuit-stats', branchId]` |
| إعادة جلب عند كل تبديل فرع حتى للفرع نفسه | تبديل الفرع يقرأ المخزَّن (30s) فلا طلب مكرر عند العودة |
| خطأ الجلب حالة محلية | `statsQuery.error` مطبَّع بـ`getApiErrorMessage` |
| صفوف الفرع السابق تبقى في الحالة عند الخطأ | `placeholderData: previous` يبقيها أثناء/بعد تبديل الفرع |

**مكافئ:** منتقي الفروع مخفي لرئيس القسم/الشعبة ولا يُطلب `/branches` إطلاقًا
(`enabled`)، ورسالة نطاق القسم/الشعبة كما هي، وتصدير Excel ومنطق خطئه (`Blob`)
بلا تغيير، و«جارِ التحميل…» يظهر أثناء الجلب (`isFetching`) كما كان.

## 3. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `CircuitStatsPage.test.tsx` | **6/6** (5 قائمة + اختبار مخزَّن/عدم تكرار جديد) |
| `queryClient.test.ts` جديد | **2/2** (إعدادات العقد + عميل مستقل لكل نداء) |
| `App.test.tsx` (198 اختبارًا) | **198/198** بعد نقل المزوّد إلى `App` |
| الحزمة الكاملة (الفرع المكدَّس) | **1939/1939** في 188 ملفًا |

## 4. E2E

أُضيفت `/circuit-stats` إلى `axe` و`375px` (مدير) — صار مجموع E2E **27 اختبارًا**
(17 سطح مكتب + 10 جوال). تُشغَّل في وظيفة `e2e` الحاجبة (لا قاعدة محلية على جهاز التنفيذ).

## 5. البوابات

| البوابة | النتيجة |
|---|---|
| `oxlint src scripts e2e` | نظيف |
| `tsc -b` | نظيف |
| `build`/`budget` | نجح · إجمالي **1787.0kB** · أكبر **388.2kB** · دخول **285.4kB** (سقف 300) |
| `guard` (CSP) / `a11y-guard` / `module-size-guard` | نظيفة (0 مجمّد) |
| `generate:api` | لا انحراف |
| `npm audit` (self-test) | 0 ثغرة عند التثبيت (`found 0 vulnerabilities`) |

## 6. المتبقي في `P6-01`

- ترحيل بقية الصفحات (`useEffect` اليدوي) إلى `useQuery` تدريجيًا.
- توحيد حالات التحميل/الخطأ/الفراغ عبر مكوّنات مشتركة.
- قياس `LCP/INP` الحقيقي (يحتاج staging).

## 7. الهجرات

**لا هجرة.** لا تغيير خلفي ولا عقد `API`.
