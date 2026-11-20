---
name: Contract Audit (End-to-End)
description: Use when a change touches a request/response DTO, service mapping, entity, projection, or frontend type. Trace one field from the form to the database and back, field by field, checking name, type, nullability, formatting, and JSON contract.
---

# تدقيق عقد من النهاية للنهاية

## متى تُستخدم

- إضافة أو تعديل حقل في `DocumentUpsertRequest` / `DocumentResponse` أو أي DTO آخر.
- تغيير نوع حقل (`string` ↔ `DateTime?` ↔ `decimal?`) أو إعادة تسميته.
- تغيير استعلام/إسقاط (`projection`) أو إحصاء يعيد حقلًا للواجهة.
- مراجعة ذاتية قبل إعلان إنجاز أي مهمة تمسّ العقد (وهي بند إلزامي في `AGENTS.md`).

## المبدأ

اختر **حقلًا واحدًا** وتتبّعه عبر الطبقات الست، ولا تعلن النجاح قبل أن تتطابق كل خلية في الجدول:

```
نموذج الواجهة → نوع TS → طلب JSON → DTO طلب → Service/Validator → Entity → EF Config
→ استعلام/إسقاط → DTO استجابة → JSON → واجهة العرض
```

## خطوات التنفيذ

1. **حدّد الحقل والمسار**: مثال `RegistrationDate` في مسار «إضافة ملف».

2. **اجمع كل المواضع** (بدل البحث العشوائي):

   ```powershell
   git grep -n "RegistrationDate" -- backend frontend docs/openapi
   git grep -n "registrationDate" -- frontend
   ```

3. **ابنِ جدول التدقيق** (طبقة، ملف، رمز، نوع، قابل للفراغ، ملاحظة) واملأه من الكود لا من الذاكرة.
   المسارات المرجعية المعتمدة:

   | الطبقة | المرجع |
   |---|---|
   | طلب | `backend/src/DocGenerator.Application/DTOs/DocumentRequests.cs` (`DocumentUpsertRequest`) |
   | استجابة | `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs` (`DocumentResponse`) |
   | منطق/تحقق | `backend/src/DocGenerator.Application/Services/DocumentService.cs` |
   | تحقق الحقول | `backend/src/DocGenerator.Application/Common/DocumentValidator.cs` |
   | تاريخ حر | `backend/src/DocGenerator.Application/Common/FreeDateParser.cs` + `ActionDateParser.cs` |
   | تخزين/إسقاط | `backend/src/DocGenerator.Infrastructure/Persistence/DocumentRepository.cs` |
   | حارس الكتابة | `backend/src/DocGenerator.Application/Services/DocumentWriteGuard.cs` |
   | واجهة | `frontend/src/types/document.ts` |
   | عميل مولّد | `frontend/src/api/generated.ts` (المصدر `docs/openapi/docgen-v1.json`) |

4. **افحص هذه النقاط بالترتيب**:

   - **الاسم**: `PascalCase` في `C#`، `camelCase` في `JSON`، مطابق في `TS`. لا اسم بديل في طبقة واحدة.
   - **النوع**: `string?` في الطلب مقابل `DateTime?` في التخزين مقابل `string?` في الاستجابة؛ `decimal?`
     مقابل `number`؛ لا `int` لحقل يمكن أن يكون فارغًا.
   - **القابلية للفراغ**: لكل حقل `null` مسار واضح: قبول في الطلب، تخزين `null`، عرض فراغ،
     وعدم كسر `projection` أو إحصاء (`SUM`/`AVG` على فراغ = `null` لا صفر).
   - **التواريخ**: الحقول النصية الحرة تُحلَّل عبر `DocumentValidator.ParseDateTime` ←
     `FreeDateParser.Parse` ← `ActionDateParser.TryParse`؛ والاستجابة `yyyy-MM-dd` عبر
     `FreeDateParser.ToResponse`.
     **المرجع التنفيذي هو `ActionDateParser.Formats` في الكود** (حاليًا خمس صيغ: `d/M/yyyy`,
     `dd/MM/yyyy`, `d-M-yyyy`, `dd-MM-yyyy`, `yyyy-MM-dd`، بلا بديل مرن). إن وجدت اختلافًا مع
     `AGENTS.md` فأبلغ عنه كسؤال للمالك ولا تعدّل القاعدة من تلقاء نفسك.
   - **الأرقام/العملات**: `decimal` في الخلفية، وتنسيق العرض عبر `Intl.NumberFormat` لا صيغة يدوية.
   - **التعدادات**: نفس الأعضاء نصًا في `C#` و`TS`، ورفض القيمة غير المعروفة بخطأ `4xx` لا بسقوط صامت.
   - **المجاميع**: إن كان الحقل يدخل إحصاءً، تحقق من الاستعلام نفسه لا من الـ `DTO` فقط.

5. **تحقق من العميل المولّد** (بوابة `CI`):

   ```powershell
   cd frontend
   npm run generate:api
   git diff --exit-code -- src/api/generated.ts
   ```

   أي فرق = عقد `OpenAPI` غير محدّث أو تغيير غير مقصود.

6. **الاختبارات**: أضف/حدّث اختبار خلفية يغطي الحقل (قيمة، فارغ، `null`، حدّ أقصى) واختبار واجهة
   يعرضه. الحقل الذي لا اختبار له = حقل غير مُدقَّق.

7. **بعد التعديل**: أعد الخطوتين 2 و5، ثم شغّل بوابات الواجهة والخلفية (انظر مهارة
   `release-gate`).

## قائمة الأخطاء الشائعة

- `DateTime?` في الخلفية و`string` غير اختياري في `TS` ⇒ تعطّل الواجهة عند `null`.
- تنسيق تاريخ في الواجهة يدويًا (`split('/')`) بدل تمرير النص كما هو إلى الخلفية.
- إسقاط `Select` ينسى حقلًا أُضيف حديثًا ⇒ يظهر فارغًا بصمت في القائمة.
- تعديل `DTO` دون تحديث `docs/openapi/docgen-v1.json` وإعادة التوليد.
- اختبار يغطي الحالة الممتلئة فقط دون الفراغ/`null`/الحد الأقصى.

## قالب التقرير

| الطبقة | الملف | النوع | فراغ؟ | الحالة |
|---|---|---|---|---|
| ... | ... | ... | ... | مطابق/انحراف |

ثم: الانحرافات المكتشفة، ما أُصلح، وما بقي كسؤال للمالك، ونتيجة `generate:api` diff.
