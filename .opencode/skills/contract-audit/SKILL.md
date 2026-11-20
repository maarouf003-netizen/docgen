---
name: Contract Audit (End-to-End)
description: Use when a change touches a request/response DTO, service mapping, entity, projection, or frontend type. Trace one field from form to database and back, checking name, type, nullability, formatting, and JSON contract. استخدمها لتدقيق عقد حقل من النهاية للنهاية.
---

# تدقيق عقد من النهاية للنهاية

إجراء مراجعة ذاتية إلزامي قبل إعلان إنجاز أي تغيير يمسّ العقد. القواعد الحاكمة في `AGENTS.md`.

## متى تُستخدم

- إضافة/تعديل/إعادة تسمية حقل في `DocumentUpsertRequest` أو `DocumentResponse` أو أي `DTO`.
- تغيير نوع (`string` ↔ `DateTime?` ↔ `decimal?`) أو قابلية فراغ.
- تغيير استعلام/إسقاط (`Select`) أو إحصاء يعيد حقلًا للواجهة.
- تغيير في `docs/openapi/docgen-v1.json` أو `frontend/src/api/generated.ts`.

## المبدأ

اختر **حقلًا واحدًا** وتتبّعه عبر الطبقات، ثم أعد المسار كاملًا من قاعدة البيانات إلى الشاشة.
لا تعلن النجاح قبل أن يتطابق كل صف في جدول التدقيق.

```
نموذج → normalizeArabicDigits → نوع TS → طلب JSON → DTO طلب → Service/Validator
→ Entity → EF Config → استعلام/إسقاط → DTO استجابة → JSON → عرض/إحصاء
```

## الخطوات

1. **حدّد الحقل والمسار** (مثال: `RegistrationDate` في «إضافة ملف»).

2. **اجمع كل المواضع** بدل البحث العشوائي:

   ```powershell
   git grep -n "RegistrationDate" -- backend frontend docs/openapi
   git grep -n "registrationDate" -- frontend
   ```

3. **املأ جدول التدقيق من الكود** لا من الذاكرة. المراجع المعتمدة:

   | الطبقة | المرجع |
   |---|---|
   | طلب | `backend/src/DocGenerator.Application/DTOs/DocumentRequests.cs` (`DocumentUpsertRequest`) |
   | استجابة | `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs` (`DocumentResponse`, `IDocumentSheetFields`) |
   | منطق/تحقق | `backend/src/DocGenerator.Application/Services/DocumentService.cs` |
   | تحقق الحقول | `backend/src/DocGenerator.Application/Common/DocumentValidator.cs` |
   | تاريخ حر | `Common/FreeDateParser.cs` + `Common/ActionDateParser.cs` + `Common/ArabicDigitNormalizer.cs` |
   | حارس الكتابة | `backend/src/DocGenerator.Application/Services/DocumentWriteGuard.cs` |
   | تخزين/إسقاط | `backend/src/DocGenerator.Infrastructure/Persistence/DocumentRepository.cs` |
   | إعداد العمود | `backend/src/DocGenerator.Infrastructure/Persistence/Configurations/` |
   | تحويل الزمن | `backend/src/DocGenerator.Infrastructure/Persistence/UtcDateTimeConverter.cs` |
   | واجهة | `frontend/src/types/document.ts` + `frontend/src/utils/arabicDigits.ts` |
   | عميل مولّد | `frontend/src/api/generated.ts` (المصدر `docs/openapi/docgen-v1.json`) |

4. **افحص النقاط السبع** بالترتيب:

   1. **الاسم**: `PascalCase` في `C#`، `camelCase` في `JSON` (افتراضي `System.Text.Json`)، مطابق في
      `TS`. لا اسم بديل في طبقة واحدة.
   2. **النوع**: `string?` طلب ↔ `DateTime?`/`decimal?` تخزين ↔ `string?` استجابة. لا `int` لحقل
      يمكن أن يغيب.
   3. **الفراغ**: لكل حقل `null` مسار كامل: يُقبل، يُخزَّن، يُعرض فراغًا، ولا يكسر `SUM`/`AVG`
      (النتيجة `null` لا صفر) ولا `projection`.
   4. **التواريخ**: الطلب نص حر يُطبَّع من الواجهة بـ `normalizeArabicDigits(...).trim()` ثم يُحلَّل في
      الخلفية عبر `DocumentValidator.ParseDateTime` ← `FreeDateParser.Parse` ← `ActionDateParser.TryParse`؛
      والاستجابة `yyyy-MM-dd` عبر `FreeDateParser.ToResponse`. **المرجع التنفيذي هو
      `ActionDateParser.Formats`** (خمس صيغ حاليًا، بلا بديل مرن — قرار `RF-014`). إن خالف ذلك
      `AGENTS.md` فأبلغ كسؤال ولا تعدّل القاعدة من تلقاء نفسك.
   5. **الزمن**: الكتابة `DateTime.UtcNow` والقراءة عبر `UtcDateTimeConverter`؛ العرض والحسابات
      الجدارية عبر `ServerClock` (توقيت دمشق) لا `DateTime.Now`.
   6. **التعدادات**: نفس الأعضاء نصًا في `C#` و`TS`، ورفض القيمة غير المعروفة بـ `4xx` برسالة
      عربية لا سقوط صامت.
   7. **الأرقام**: `decimal` في الخلفية ودقته في `Configurations/`، والعرض عبر `Intl.NumberFormat`
      لا صيغة يدوية.

5. **حدّث عقد `OpenAPI` ثم العميل المولّد** (بوابتا `CI`):

   ```powershell
   # 1) أعد توليد اللقطة المطبَّعة من سطح API الفعلي
   $env:UPDATE_OPENAPI_SNAPSHOT = '1'
   dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj `
     --filter "FullyQualifiedName~OpenApiContractTests" --nologo -v q
   Remove-Item Env:UPDATE_OPENAPI_SNAPSHOT

   # 2) أعد توليد عميل TS وقارنه
   cd frontend
   npm run generate:api
   git diff --exit-code -- src/api/generated.ts
   cd ..

   # 3) أكّد أن اللقطة مطابقة (بلا متغيّر التحديث)
   dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj `
     --filter "FullyQualifiedName~OpenApiContractTests" --nologo -v q
   ```

   - إن لم تكن حزمة `openapi-typescript` مثبتة محليًا:
     `npx --yes openapi-typescript@7.13.0 ../docs/openapi/docgen-v1.json -o src/api/generated.ts`
     (نفس إصدار `package-lock`).
   - قد يُظهر `git status` الملف «معدّلًا» بسبب `CRLF` فقط؛ احكم بـ `git diff` الفعلي: إن كان
     فارغًا فالمحتوى مطابق.
   - أي فرق حقيقي = العقد غير محدّث أو تغيير غير مقصود.

6. **الاختبارات**: أضف/حدّث اختبار خلفية (قيمة، فارغ، `null`، حدّ أقصى/أدنى) واختبار واجهة يعرض
   الحقل. الحقل بلا اختبار = غير مُدقَّق. شغّل المتأثر فقط أثناء العمل ثم البوابات كاملة قبل الدفع.

7. **أعد الخطوتين 2 و5** بعد التعديل، ثم نفّذ مهارة `release-gate`.

## أخطاء شائعة

- `DateTime?` في الخلفية و`string` غير اختياري في `TS` ⇒ تعطّل عند `null`.
- تمرير التاريخ من الواجهة بلا `normalizeArabicDigits` ⇒ رفض الخلفية للأرقام العربية.
- `Select` جديد ينسى حقلًا ⇒ يظهر فارغًا بصمت في القائمة دون خطأ.
- تعديل `DTO` دون `docs/openapi/docgen-v1.json` وإعادة التوليد.
- اختبار يغطي الحالة الممتلئة فقط دون الفراغ/`null`/الحد الأقصى.
- افتراض أن `AGENTS.md` هو الكود: عند التعارض، الكود + `docs/audit/units/RF-014.md` هما الحقيقة.

## قالب التقرير

| الطبقة | الملف | النوع | فراغ؟ | الحالة |
|---|---|---|---|---|
| ... | ... | ... | ... | مطابق/انحراف |

ثم: الانحرافات، ما أُصلح، ما بقي سؤالًا للمالك، ونتيجة `generate:api` diff.

## قائمة الإنجاز

- [ ] كل مواضع الحقل مجمعة ومطابقة.
- [ ] جدول التدقيق مكتمل بلا «غير معروف».
- [ ] `OpenAPI` والعميل المولّد بلا فرق.
- [ ] اختبارات الحالة الفارغة/`null` مضافة.
- [ ] البوابات خضراء أو الانحراف مذكور صراحة.
