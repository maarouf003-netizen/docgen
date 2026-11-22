# `P3-06a` — التخزين المؤقت للبيانات المرجعية — 2026-11-22

> **النطاق:** تخزين **أسماء/معرّفات** الفروع والشعب والدوائر في `IMemoryCache`
> مع إبطال محلي عند الكتابة وTTL قصير. **العدّادات الحيّة (مستخدمون/مستندات/دوائر)
> لا تُخزَّن**؛ تُحسب من القاعدة في كل قراءة. لا تغيير في API ولا هجرة.

## 1. التصميم

- `IReferenceDataCache` (Application): `GetOrCreateAsync<T>(group, key, factory, ct)`
  و`Invalidate(group)`.
- `ReferenceDataCache` (Infrastructure): `IMemoryCache` + رقم إصدار لكل مجموعة؛
  الإبطال يرفع الرقم فتصبح المفاتيح القديمة غير مرئية فورًا، وTTL **3 دقائق**
  كشبكة أمان.
- `IRepository<T>.ListNoTrackingAsync`: قراءة **غير متتبَّعة** — لا تُخزَّن كيانات
  مربوطة بـ`DbContext` انتهى نطاقه.
- تُخزَّن فقط القوائم الخام:
  - `ExecutionCircuitService`: الدوائر/الشعب/الفروع (الملكية، الأسماء، التفعيل).
  - `SectionService`: الشعب/الدوائر (الأسماء والعدّاد يُحسب حيًّا).
  - `BranchManagementService`: الفروع (الأسماء/الأكواد/المحافظة).
- الإبطال: بعد كل `SaveChangesAsync` في مسارات الكتابة الثلاثة (إنشاء/تسمية/تفعيل/
  حذف/نقل الدوائر، وإنشاء/تعديل/حذف الفروع).

## 2. المراجعة النقدية (وجد/إصلاح)

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **خطر جوهري:** `Repository.ListAsync` يعيد كيانات **متتبَّعة** مربوطة بـ`DbContext` الطلب؛ تخزينها يعيد كيانًا تابعًا لنطاق منتهٍ | قراءة `Repository<T>` | أُضيف `ListNoTrackingAsync` ويُستخدم في مصانع الكاش فقط؛ مسار بلا كاش يبقى `ListAsync` |
| 2 | إبطال ناقص يعني بيانات قديمة حتى TTL | تعدد مسارات الكتابة | إبطال بعد **كل** `SaveChangesAsync` في الخدمات الثلاث (13+5+3 موضعًا) |
| 3 | تخزين DTO يحمل عدّادات حيّة | `ListMineAsync`/`ListSectionsAsync` | نُخزّن القوائم الخام فقط، والعدّادات تُحسب حيًّا |
| 4 | تعدد النسخ: إبطال نسخة لا يصل الأخرى | لا `Redis`/ناقل | TTL 3 دقائق يحدّ البقايا؛ موثّق كقيد |
| 5 | `IRepository<T>` الجديد يكسر مستودعات اختبارية مصغّرة | أخطاء بناء | أُضيف التنفيذ إلى `UnconfiguredCircuitRepository`/`UnconfiguredSectionRepository` والمستودعات الاختبارية |
| 6 | تسابق أول قراءة بعد إبطال | لا single-flight | مصنع القراءة رخيص؛ التسابق مقبول ومقصود |

## 3. الاختبارات

- `ReferenceDataCacheTests` (2): إعادة الاستخدام + الإبطال، وعزل المجموعات.
- `SectionServiceTests.ListSections_CacheInvalidatedAfterCreateAndRename`.
- `ExecutionCircuitServiceTests.ListMine_CacheInvalidatedAfterCircuitCreate`.
- `BranchManagementServiceTests.ListBranches_CacheInvalidatedAfterCreate`.

## 4. التحقق المحلي

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| الاختبارات المستهدفة (4 أصناف) | **110/110** |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |

## 5. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
