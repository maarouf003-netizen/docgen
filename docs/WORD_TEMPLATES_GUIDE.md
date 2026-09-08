# دليل إدارة قوالب الوورد (Word Templates)

مرجع نظامي لتعديل القوالب الموجودة وإضافة قوالب جديدة في منصّة توليد المستندات التنفيذية.

> **نطاق هذا الدليل:** ملفات القوالب وإعداداتها وربطها بالخلفية والواجهة والاختبارات. لا يتناول إعدادات النشر العامة ما لم يُشر إليها صراحة.

---

## 1) أين توجد القوالب؟

**المصدر الوحيد للتعديل:** `backend\src\DocGenerator.Api\WordTemplates\`

| الكود | اسم الملف | زر الواجهة | الفرع في الخلفية | المعاملات المطلوبة |
|-------|-----------|------------|------------------|-------------------|
| `001` | `summon.docx` | `استدعاء تنفيذي` (أساسي) | `001` أو `002` | لا شيء |
| `002` | `record.docx` | `محضر تنفيذي` (أساسي) | `001` أو `002` | لا شيء |
| `003` | `notice.docx` | `توليد إخطار تنفيذي` | `003` | كفيل (`recipient>0`) أو وريث (`heirId`) |
| `004` | `Seizure.docx` | `حجز منظومة` (أساسي) | `004` | لا شيء |
| `005` | `property.docx` | `توليد إخطار بيع غير منقولة` | `005` | `estateIds` (عقار واحد على الأقل) |
| `006` | `property notice via paper.docx` | `توليد إخطار بيع بالصحف` | `006` | `estateIds` (عقار واحد على الأقل) |
| `007` | `notice via paper.docx` | `توليد إخطار بالصحف` | `007` | كفيل/مقترض (`recipient`) أو وريث (`heirId`) |
| `PS` | `property seizure.docx` | `حجز عقاري` (أساسي) | `PS` | `estateIds` (يستخدم أول عقار فقط) |

> **ملاحظة هامّة:** يجب عدم تعديل أي نسخة مكرّرة تحت `backend\src\DocGenerator.Api\bin\...\WordTemplates` أو
> `backend\build-verify\WordTemplates` أو `backend\tests\...\bin\...\WordTemplates`. فهذه **نواتج** تُعاد
> بناؤها تلقائيًا عند كل بناء/نشر من المصدر أعلاه.

### أين يُعرَّف «ربط الكود ← اسم الملف»؟

يُعرَّف في مصدرين يجب أن يبقيا **متطابقين حرفًا بحرف**:

1. `backend\src\DocGenerator.Api\appsettings.json` (مقطع `WordTemplates:Templates`) — **الفعّال عند التشغيل**.
2. `backend\src\DocGenerator.Application\Common\Options\WordTemplatesOptions.cs` (القيم الافتراضية في الكود) —
   يُستخدم **دفاعيًا فقط** حين يكون قسم `WordTemplates` غائبًا أو ناقصًا من الإعداد عند التشغيل.

> أسباب المزامنة: امتلاك الكود دون «شرك خفي»؛ أي بيئة/اختبار مستقبلي يفتقد قسم `WordTemplates` كاملًا كان
> سيحصّره في 5 قوالب. هذه مزامنة **دفاعية بلا أثر وظيفي** على المسار التشغيلي القائم (حيث `appsettings`
> كامل بـ 8 قوالب). لا تُعدّ «إصلاح خلل قائم» — السلوك الحالي سليم.

### كيف تُنسخ القوالب إلى أماكن أخرى؟

- `backend\src\DocGenerator.Api\DocGenerator.Api.csproj` سطر `25`:
  ```xml
  <None Include="WordTemplates\**\*.docx" CopyToOutputDirectory="PreserveNewest" />
  ```
- بذلك تُنسخ أي `*.docx` تحت `WordTemplates\` إلى إخراج البناء والـ `dotnet publish` (بما في ذلك صورة Docker).
- **قيدان إلزاميان**:
  - الامتداد يجب أن يكون `docx` حصرًا (`dotx`/`doc` لن يُلتقطا بالـ `glob`).
  - `PreserveNewest` يعتمد الطابع الزمني؛ أي نسخة أقدم من النسخة الموجودة قد لا تُنسخ إلا بعد `Rebuild`.

---

## 2) صيغ الـ `placeholders` المدعومة

يُحوَّل القالب عبر `backend\src\DocGenerator.Application\Services\WordTemplateRenderer.cs`:

| الصيغة | السلوك |
|--------|--------|
| `{{key}}` / `{{ key }}` | يُدرج القيمة **نصًا مُهرَّبًا**؛ الأسطر الجديدة `\n` تتحوّل إلى `<w:br/>`. لا يمكن حقن OOXML. |
| `{{r key}}` | يُدرج القيمة **خامًا** (RichText) **فقط** إذا كانت القيمة تبدأ بـ `<w:`. أي قيمة أخرى تُهرَّب نصًا. محصورة بالمفاتيح الغنية التي تبنيها الخلفية (مثل `execution_debtors_and_its_adresses`). |
| `{{ key extra1 extra2 }}` | يستبدل `key` بالقيمة ثم يلحق `extra1 extra2` نصوصًا (مثال: `{{contain 1}}` → القيمة + `" 1"`). |
| مفتاح غير موجود في السياق | يُستبدل بـ **فراغ صامت**. خطأ إملائي في اسم المفتاح لا يُكتشف إلا بصريًا في المخرجات. |

**قيود صارمة:**
- أسماء المفاتيح **ASCII فقط** (`[A-Za-z0-9_]`)، لا عربية كاسم مفتاح (النمط `TokenRegex`).
- سياق المستند (المفاتيح المتاحة) يُبنى في `backend\src\DocGenerator.Application\Services\DocumentContextBuilder.cs`.
  عند إضافة مفتاح جديد للقالب يُضاف هنا.

### الأجزاء التي يعالجها المحرّك من ملف `docx` (zip)

`word/document.xml`، `word/header*`، `word/footer*`، `word/footnotes*`، `word/endnotes*`. غير المعالجة: `comments.xml` وغيره.

### فقرات الرسوم / مربعات النصوص

- فقرة تحوي `drawing`/`pict` (مربع نص، صورة، VML) **لا يُعاد بناؤها**؛ تُستبدل الـ placeholders نصًا داخل الـ runs النصية فقط.
- **`{{r key}}` داخل مربع نص/شكل يُصبح فارغًا** (معطّل عمدًا لئلا يكسر بنية الشكل). لا تضع `{{r …}}` داخل مربع نص — استخدم `{{key}}` نصًا عاديًا.

### أمان إلزامي عند التعديل

- قيم `Templates` يجب أن تبقى **أسماء ملفات مسطّحة** فقط — ممنوع `..` أو مسار مطلق (وإلا اجتياز دليل عبر `Path.Combine`).
- **ممنوع** ربط `{{r key}}` بحقل مستخدم خام (نص يدخله المستخدم). `{{r}}` مخصّصة للمفاتيح الغنية التي تُهرب خادميًا (عبر `XmlEscape`) ثم تُحقن — وإلا فتح باب حقن OOXML.

---

## 3) تعديل قالب موجود

1. افتح `backend\src\DocGenerator.Api\WordTemplates\<file>.docx` في Word/OnlyOffice/LibreOffice.
2. عدّل النص/التخطيط، وأدرج/عدّل الـ placeholders وفق §2 مع مراجعة المفاتيح المتاحة في `DocumentContextBuilder.cs`.
   - لا تقلق إن قسّم Word الـ placeholder إلى عدة `runs` — المحرّك يجمّع النصوص داخل الفقرة قبل المطابقة.
3. **التحقق**:
   - `dotnet build` (من `backend\`) ثم `dotnet test`.
   - توليد الوثيقة من الواجهة وفحص المخرجات بصريًا في Word.
   - انتبه: **كل توليد يزيد `PrintCount` ويحدّث `UpdatedAt`** على المستند — ليس عملية قراءة محضة.

---

## 4) إضافة قالب جديد (الخطوات كاملة)

1. **أنشئ `xxxx.docx`** وضعْه في `backend\src\DocGenerator.Api\WordTemplates\`. (امتداد `docx` حصرًا — انظر §1.)
2. **اربط الكود** في المصدرين معًا (متطابقان حرفًا):
   - `appsettings.json` → `WordTemplates:Templates`: أضف `"XXX": "xxxx.docx"`.
   - `WordTemplatesOptions.cs` → أضف السطر نفسه للقاموس الافتراضي.
   - اختر رمزًا فريدًا وحساسًا للحالة (لا تكرر `001..007`/`PS`). **الكود حساس للحالة** (`TryGetValue` بحساسية).
3. **إن لزم سياق خاص**: أضف فرعًا في `DocumentContextBuilder.cs` لقيمة `templateCode` الجديدة يُولّد المفاتيح الخاصة،
   مع إعادة استخدام الدوال الغنية القائمة (`BuildPropertySaleContext`، `BuildNoticePaperContext`، إلخ) ولا تعيد تنفيذها.
4. **أضف زر الواجهة** في `frontend\src\components\view\DocumentGenerationSection.tsx`:
   - قالب بسيط بلا معاملات → أضف سطرًا إلى `BASIC_DOCS`.
   - قالب يشترط عقارًا/شخصًا → أنشئ `handler` مناسبًا (نمط `generateSeizure`/`generateNotice`/`generateEstateNotice`) وزرًّا في القسم المناسب.
5. **أضف الاختبارات**:
   - خلفية: `backend\tests\DocGenerator.Api.Tests\WordGenerationIntegrationTests.cs` (توليد عبر `template=XXX` وصحّة بنية `docx`).
   - واجهة: `frontend\src\pages\DocumentView.test.tsx` (ظهور الزر/الرسالة وتأكيد استدعاء الـ API).
6. **التحقق الكامل** (§5) ثم **فحص بصري** للوثيقة المولدة.

> مرجع للاختبارات القائمة لصيغ الـ placeholders: `backend\tests\DocGenerator.Application.Tests\WordTemplateRendererTests.cs`
> (تغطّي: placeholder واحد، منقسم عبر `runs`، `{{r}}` خام، الأمان مقابل حقن XML، الأسطر الجديدة، القيمة الفارغة، مربع النصوص، `{{ key extra }}`).

---

## 5) قائمة التحقق الإلزامية (قبل إعلان الإنجاز)

يجب أن تبقى **كلها خضراء**:

- خلفية: `dotnet build` ثم `dotnet test` (من `backend\`).
- فحص الاتساق: `WordTemplatesOptions.cs` مطابق لـ `WordTemplates:Templates` في `appsettings.json` (استخدم `diff`).
- واجهة: `npx oxlint src`، `npx tsc -b`، `npx vitest run`، `npm run build` (من `frontend\`).
- مطابقة كل بند في الخطة المعتمدة مع ما نُفذ فعلًا؛ راجع العقود عبر frontend/backend حقلًا بحقل.
- فحص بقايا المصطلحات/الأسماء القديمة بـ `rg`/`Select-String` للمعرّفات المستبدلة.

### النشر

- تغيير قوالب/إعدادات قوالب **لا يتطلب هجرات EF**. 
- في نشر Docker/Render: أعد بناء الصورة لالتقاط الـ `docx` الجديدة.
- **ملاحظة جانبية (خارج النطاق لكن يجب التنبيه):** `Dockerfile` يبني بـ `dotnet/sdk:8.0` بينما يستهدف
  `DocGenerator.Api.csproj` حاليًا `net10.0`. هذه مشكلة نشر كامنة مستقلة تمامًا عن القوالب ويجب معالجتها عند النشر،
  ولا يُعالجها هذا الدليل.
- عند استدعاء توليد وثيقة:
  - كود قالب غير معروف → **`400`** (رسالة «قالب غير معروف»).
  - كود معروف لكن ملفه مفقود → **`500`** عام (بلا كشف مسار القالب).
  - `ps` بحرف صغير ≠ `PS` → `400` (حساسية الحالة).

---

## 6) مرجع المفاتيح المتاحة في السياق (مولَّدة في `DocumentContextBuilder.cs`)

**عامة (أي قالب):** `court`, `court_with_prefix`, `lawyer`, `borrower_name/father/family/mother/birth/register/national_id`,
`borrower_nature/registration_number/represented_by`, `contract_type/number/date`, `annex_type/number/date`,
`amount_numeric`, `amount_words`, `current_date`, `current_date_arabic`, `currency`, `contract_type_selector`,
`file_number/type/year/full`, `immediate_actions`, `immediate_actions_prefix`, `execution_debtor_and_its_adress`,
`borrower_address`, `borrower_address_type`, `applicant`, `raw_applicant`, `contain`, `contain_notice`, `contain1`,
`amount_words_record`, `amount_words_record_execution`, `amount_words_notice`,
`guarantor_{1..5}_name/father/family/mother/birth/register/national_id/address/address_type/nature/registration_number/represented_by`,
`real_estates`, `property`, `property_owner`, `execution_debtors_and_its_adresses` (RichText — عبر `{{r …}}`),
`execution_debtors`, `branch`.

**خاصة بقالب محدد:**

- `001`/`002`: `borrower_address` يُبنى كمحتوى RichText (ورثة المتوفَّى).
- `004`: `seizure_date{n}` و`court_with_prefix{n}` لـ `n = 1..5` حسب عدد المدينين؛ `seizure_date`.
- `005`: `property`, `property_owner`, وتفكيك اسم المالك إلى `borrower_name/father/family`.
- `006`: `property`, `property_owner`, `execution_debtor`.
- `007`/`PS`: `recipient_name`, `recipient_role`, `execution_debtor`, `property_number`, `property_district`, `Land_Registry`.
- `PS`: `estate_seizure_date` — تاريخ إلقاء الحجز الخاص بالعقار المحدد (`Asset.SeizureDate` بصيغة `yyyy-MM-dd`). عند غياب التاريخ يُقدَّم خط تعبئة يدوي «`…………………………………………`» ليُكتب التاريخ يدويًا، فلا تُترك خانة فارغة صامتة. الصيغة عامة لكل أنواع الأصول (مركبة/متجر/…) وتُستَخدم لاحقًا عند إضافة قوالبها.

> عند الحاجة لمفتاح جديد: أضفه في `DocumentContextBuilder.cs` (عبر الدوال الغنية القائمة)، ثم استخدمه في القالب بالصيغة المناسبة (§2).
