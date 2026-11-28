# `W8a` — قرارات المالك السريعة — 2026-11-28

> **لقطة مؤرخة** — تُجمَّد بعد الدمج؛ أي تصحيح بملحق.
> **الأساس:** `master e5d29d7a` (بعد `W7`)، والفرع `review/w8-owner-decisions`.
> **المرجع:** قرارات المالك في `docs/REVIEW_DEBT.md` §3.4، ومهارة `independent-review`.

## 1. النطاق

| البند | التغيير |
|---|---|
| حدود القراءة | سياسة `reads` (120/دقيقة، مفتاح `ReadsPerMinute`) لـ`GET /api/portal/stats` و`GET /api/documents/my-pending-registrations`؛ كتابة «إكمال التسجيل» تبقى `expensive`. |
| منع التصدير الشامل | `GET /api/documents/export` يرفض `400` إن لم يُمرَّر أي فلتر (بحث/حالة/أي فلتر)، لكل الأدوار. |
| وضوح `status` | `status=منفذ` في `GET /api/documents` و`/api/documents/list` يُرفض `400` برسالة توجّه إلى صفحة الملفات المنفذة؛ لا دمج مع «منفذ عليه». |
| توقيت دمشق | حدود الفترة تُحوَّل إلى `UTC` لمقارنة `CreatedAt`، والتصنيف الشهري/فترة الاستئناف تستخدم اليوم المحلي (دمشق) بدل `CreatedAt.Date` (UTC). |

**بلا هجرة، وبلا تغيير `DTO`/`OpenAPI`** (سلوك/تحقق فقط).

## 2. الاختبارات

- `RateLimitOptionsTests`: `ReadsPerMinute` ضمن تحقق المدى.
- `RateLimitCoverageTests`:
  - `PortalAndPendingReads_CarryReadsPolicy_AndPendingWriteStaysExpensive` (انعكاسي).
  - `AppSettings_ReadLimits…` يشمل المفتاح الجديد (يفشل عند غيابه).
- `DocumentsExportIntegrationTests`:
  - `Export_WithoutAnyFilter_IsRejectedWithValidationMessage`.
  - تحديث كل مسارات التصدير الناجحة/429 لتُمرِّر فلترًا (`status=متداول`).
- `DocumentsIntegrationTests`: `GeneralList_ExecutedStatusFilter_RejectedWithGuidance` للمسارين.
- `StatisticsTests`: `ManagerStats_Monthly_UsesDamascusLocalDateForCreatedAtFallback`
  (‏22:00 UTC في 30 أيلول = 01:00 في 1 تشرين الأول) + التصنيف الشهري.
- `QueryBudgetTests`/`StatsScopeTests`/`RF017SilentWriteAuditTests`: تمرير فلتر للتصدير
  (`تحت رفع` للمسودات، `متداول` للمسجّل، `court` لاختبار حجب «بلا بحث»).

## 3. المراجعة المستقلة (الجولة الأولى) والتصحيحات

- **`B1`:** حارس `status=منفذ` عارض اختبارين قائمين (`Search_ValidStatuses_ReturnOk`
  و`MainList_HidesReferredToStart_UnlessTextSearch`)؛ أُزيلت القيمة من قائمة الصالح
  وأُضيف توقّع `400` في اختبار الحجب، مع اختبار الرفض المخصص.
- **`B2`:** فلتر `status=متداول` في اختبارات التصدير لا يطابق **المسودات** التي ينشئها
  `CreateDocumentAsync` (‏`IsDraft=true`) ⇒ صفر صفوف واختبارات حمراء/سقوف زائفة؛
  صُحّح إلى `status=تحت رفع` للمسودات، و`متداول` للمسجّل، و`court=دمشق` لاختبار
  «الحجب بلا بحث» ليبقى نيّة الاختبار.
- **متابعات مغلقة أيضًا:** تشديد `ReadsPerMinute ≥ 60` وأعلى من `Expensive` في اختبار
  `appsettings`؛ استخدام `ExecutionStatusCatalog.ExecutedFilter` بدل ثابت محلي؛ رفض
  `status=منفذ` في التصدير أيضًا؛ توحيد تحويل `Kind` عبر `AsUtc`؛ واختبار حدّ استئناف
  `Pending` عند منتصف الليل.

## 4. الطفرات (محلية، تُعاد فورًا)

| # | الطفرة | الاختبار | النتيجة |
|---|---|---|---|
| `M1` | إسقاط شرط «فلتر واحد» في التصدير | اختبار الرفض | **غير مثبت محليًا** (PostgresFact) — يُغطّى في CI |
| `M2` | إسقاط حارس `status=منفذ` | `GeneralList_…` | **غير مثبت محليًا** (PostgresFact) — يُغطّى في CI |
| `M3` | إرجاع `CreatedAt.Date` بدل اليوم المحلي | `ManagerStats_Monthly_…` | **غير مثبت محليًا** (PostgresFact) — يُغطّى في CI |
| `M4` | إعادة شاشتي القراءة إلى `expensive` | `PortalAndPendingReads…` | **فشل** (مثبت محليًا) |

> الاختبارات المعتمدة على `PostgreSQL` (`PostgresFact`) لا تعمل محليًا؛ تُنفَّذ في
> `backend-postgres` على `CI`.

## 5. البوابات

| البوابة | النتيجة |
|---|---|
| backend | ✅ `build -warnaserror` 0 تحذير، `format --verify-no-changes`، RateLimit **15/15** |
| frontend | لا تغيير واجهة في هذه الموجة (تُشغَّل البوابات كاملة قبل الدمج) |
| migration-guard | بلا هجرة؛ `has-pending-model-changes` + لا `datetime2` |
| `backend-postgres` | في `CI` (اختبارات التصدير/الحالة/الإحصاءات الجديدة) |

## 6. المتبقي

- `W8b`: قياس + فهرس `(…CreatedAt, Id)` + تجميع `SQL` للإحصاءات (يحتاج هجرة/قياسًا).
- `W8c`: `POST /generate` + مهمة أرشفة `AuditLogs` المشفّرة (24 شهرًا/5 سنوات).
- قرارا معدل القراءة لبقية الشاشات إن ظهر خنق مماثل.
