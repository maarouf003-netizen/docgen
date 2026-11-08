# تقرير إنجاز — سجل أرقام الأساس v2 (2026-11-09)

> **الفرع:** `feature/base-number-registry` (لا دفع مباشر إلى `master`).
> **المرجع:** `docs/base-number-registry-plan-v2.md` + `docs/audit/base-number-registry-review-final-2026-11-08.md`.

## 1) ما نُفّذ

### السياسة والكاتب الوحيد
- `DocumentBaseNumberPolicy`: تطبيع النوع/الرقم، تحقق السنة 1900..الحالية، الأهلية مقيّدة بدائرة
  الملف الحالية، الهوية الفعّالة (السنة ثم `CreatedAt` ثم `Id`)، وبناء السجل الكامل.
- `IDocumentNumberRegistry` / `DocumentNumberRegistry`: الكاتب الوحيد لصفوف `DocumentBaseNumbers`
  وحقول رقم الملف. كل مسار داخل معاملة + `AcquireScopeLockAsync(circuit,year)` (PostgreSQL
  `pg_advisory_xact_lock`، SQLite بلا قفل) + إعادة قراءة داخل القفل + تحويل تفرد إلى `409`.
- `INumberWarningTokenService`: رمز تحذير موقّع HMAC-SHA256 قصير العمر (10 دقائق)، مرتبط
  بالمستخدم + نطاق الرقم + صفوف التعارض؛ لا `confirmReuse` منطقي.

### الكيان والهجرة
- توسيع `DocumentBaseNumber`: `ExecutionCircuitId`, `CircuitName`, `FileType`, `TypeKey`,
  `NumberKey`, `Source`, `UpdatedById`, `VoidedAt`, `VoidedById`, `VoidReason` + نوافيج.
- فهرس فريد جزئي `UX_DocumentBaseNumbers_Active` على
  `(ExecutionCircuitId, TypeKey, Year, NumberKey) WHERE VoidedAt IS NULL`.
- الهجرات (واحدة لكل سياق):
  - `20261108202145_BaseNumberRegistry` (SQLite)
  - `20261108202211_BaseNumberRegistry_Pg` (PostgreSQL)
- التعبئة داخل الهجرة: تعبئة الصفوف القائمة (`Source=rotation`)،
  `VoidedAt=document-deleted` للمحذوف، إنشاء صف قيد لكل ملف مرقّم (وللمحذوف صف ملغى توثيقي)،
  وبوابات توقف صريحة: ملف/صف بلا دائرة، صف أقدم من تبديل دائرة من التعديل، سنة غير صالحة،
  أو تكرار بعد التطبيع. لا نسبة صامتة.
- اختبار الهجرة على نسخة قاعدة التطوير: 64 صف قيد، 39 ملغى `document-deleted`، صفر صفوف يتيمة.

### المسارات
- الإنشاء/التعديل: `SyncRegistrationAsync` بعد حفظ المستند (إنشاء/استبدال سنة/إفراغ).
- التدوير: `SaveRotationsAsync` (الإدخال الفارغ = لا تغيير، سقف 500، طبقتا التكرار، تدقيق التجاوز).
- التصحيح: `PUT /documents/{id}/base-numbers/{year}` تعديل موضعي + تدقيق قبل/بعد + تجميد الملغى.
- التراجع: `DELETE /documents/{id}/base-numbers/{year}` السنة الحالية فقط، `Source=rotation`،
  `VoidedAt=undo-rotation`، والصف يبقى معروضًا.
- التجديد: `ApplyRenewalAsync` عبر السجل (`Source=renewal`) مع شرط السنة والتفرد.
- الإحالة: V-07 — صفوف الدائرة القديمة تبقى ولا تُحذف/تُلغى؛ إعادة القيد في الدائرة الجديدة.
- الحذف/الاستعادة: إلغاء كل الصفوف `document-deleted` مع بقائها معروضة؛ الاستعادة = `NeedsRegistration`
  + إفراغ `FileNumber/FileYear` وطلب رقم جديد.
- تسجيل الإنابة: `ExecutionCircuitId` في `RegisterDelegationRequest` (إلزامي للخارجية) + منتقي دائرة.

### الهوية/التصدير
- `EffectiveFileIdentity.Latest/Number/Year` تقرأ `Document.ExecutionCircuitId` وتتجاهل الملغى.
- `LatestFrom` يقبل `int? circuitId` (موضعا `DocumentSearchService` و`PortalService`).
- `DocumentExportRow.ExecutionCircuitId` + إسقاط التصدير.
- `DocumentSearchTextBuilder` لا يضم أرقام الدوائر الأخرى/الملغاة.
- مراجعة اتساق R9 عند الإقلاع: كل ملف مرقّم له صف قيد غير ملغى في دائرته/سنته، ولا صفوف بلا دائرة.

### الواجهة
- `BaseNumbersModal`: سجل كامل (بما فيه الملغى)، الدائرة/المصدر/الحالة، تصحيح موضعي، زر تراجع
  بتأكيد، وعرض بطاقات على الجوال + جدول على المكتبي.
- `NumberReuseWarningModal`: نافذة تحذير 409 مع «إدخال على أي حال»/«إلغاء».
- ربط التحذير في: `DocumentForm`، `Rotation`، `RegisterDelegationModal`،
  `PendingRegistrationsPage`، `StatusChangeModal`، `ExecutedStatusModal`، `RenewalModal`.

## 2) البوابات (آخر تشغيل)

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | ✅ 0 تحذير/0 خطأ |
| Application tests | ✅ 2310/2310 |
| Api tests `Category!=Sequential` | ✅ 899/899 |
| Api tests `Category=Sequential` | ✅ 5/5 |
| Postgres migration script (offline) | ✅ بلا `datetime2`، يتضمن البوابات و`ON DELETE SET NULL` |
| `has-pending-model-changes` (السياقان) | ✅ لا تغييرات معلّقة |
| `npx tsc -b` | ✅ |
| `npx oxlint src` | ✅ |
| `npx vitest run` | ✅ 1774/1774 |
| `npm run build` | ✅ |
| `npm run guard` (CSP) | ✅ 91 ملفًا |
| `node scripts/subhead-guards.mjs` | ✅ |

## 3) تنبيه النشر (إلزامي)

**طبّق قبل النشر:**
```bash
dotnet ef database update --context DocGeneratorDbContext
dotnet ef database update --context DocGeneratorPostgresDbContext
```
وإلا سيرفض التطبيق الإقلاع (`Database:AutoMigrate=false`).

- أضيفت الهجرتان إلى `RUN_GUIDE.md` §9 في نفس التغيير.
- القاعدة الحية (`Supabase`) لم تكن قابلة للوصول من بيئة التنفيذ (IPv6-only/بركة غير صالحة)؛
  التدقيق الحي والنسخة الاحتياطية **معلّقان** ويجب تنفيذهما قبل التطبيق — انظر
  `docs/audit/base-number-registry-audit-2026-11-09.md`.

## 4) انحرافات موثّقة

1. **`ExecutionCircuitId`/`CircuitName` قابلان للفراغ على مستوى القاعدة** (رغم تعبئتهما):
   لحذف دائرة إدارية عبر `ON DELETE SET NULL` مع بقاء الصف التاريخي (`CircuitName` لقطة).
   الكاتب يفرض دائرة لكل كتابة جديدة، وR9 يرفض الإقلاع عند أي ملف مرقّم بلا صف قيد.
2. **التحذير في `complete-registrations`**: الرمز مرتبط بالملف الأول المصادم؛ عند وجود تحذير
   لملف ثانٍ يعيد الخادم رمزًا جديدًا وتُعاد النافذة (لا إدخال صامت).
3. **التدقيق الحي للقاعدة الحية** لم يُنفّذ لتعذّر الاتصال؛ الهجرة ترفض أي تكرار/صف بلا دائرة
   برسالة صريحة، ويجب تنفيذ §8.1 قبل النشر.
