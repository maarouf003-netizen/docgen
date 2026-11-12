# المتدخلون في الملف — تسليم `2026-11-12`

> وثيقة مؤرخة (لقطة تسليم). لا تُحرَّر بعد التجميد؛ أي تحديث لاحق يكون بملحق/وثيقة جديدة.
> **الفرع:** `feature/document-interventions` — **الحالة:** منفَّذ محليًا، بانتظار الدمج والنشر.

## 1) النطاق والقرارات

- **الميزة:** «إضافة متدخل» في صفحة تعديل ملف «الجهة العامة طالبة التنفيذ» و«الجهة العامة منفذ عليها».
- **الصفات:** `natural` (شخص طبيعي)، `legal` (شخص اعتباري)، `public` (جهة عامة من السجل المرجعي).
- **بيانات الطلب:** رقم ورود طلب التدخل، تاريخ الورود (إلزامي)، تاريخ القبول (اختياري، لا يسبق الورود)، الموضوع، الملاحظات.
- **الأطراف:** الطبيعي بكامل الهوية + العنوان/الوكيل + الممثل الشرعي + الورثة؛ الاعتباري ببياناته؛ الجهة العامة باختيار إلزامي من السجل المرجعي.
- **الحفظ:** فوري عبر مسارات فرعية مستقلة (لا ينتظر حفظ نموذج الملف، ولا يمسّ `Document.Version`).
- **الحالة:** مسموح في أي حالة للملف غير المحذوف (قرار المالك).
- **«عرض وايداع»:** مستثنى (الجهة فيها عارضة لا خصمًا) — قابل للتفعيل لاحقًا بتوسيع شرط الواجهة/الخدمة.
- **العرض:** مجموعة «المتدخلون» في بطاقة الأطراف، وسطر بنفسجي «طلب تدخل» في وقوعات الملف، ونافذة تفاصيل من الاثنين، وتظهر في بوابة الجهة تلقائيًا.
- **البحث:** أسماء المتدخلين وورثتهم وأرقام تسجيلهم تدخل `SearchText`.

## 2) العقود

```text
POST   /api/documents/{id}/interventions
PUT    /api/documents/{id}/interventions/{interventionId}
DELETE /api/documents/{id}/interventions/{interventionId}
```

- الصلاحية: `RolePermissions.CanEditDocuments` (محامٍ) + `CanAccessAsync` (المالك فقط للمحامي).
- `POST/PUT` يعيدان `DocumentInterventionDto` كاملًا، و`DELETE` يعيد `204`.
- التواريخ في الطلب نصوص حرة (`d/M/yyyy` …) تُفسَّر بـ`DocumentValidator.ParseDateTime`، وفي الاستجابة `yyyy-MM-dd` (قاعدة حقول التواريخ).
- الأخطاء: `400` برسالة عربية (صفة/اسم/تاريخ ورود/قبول قبل الورود/سجل مرجعي/حد أقصى)، `403` لغير المالك، `404` لمتدخل ملف آخر.

## 3) قاعدة البيانات

| الجدول | الحقول الأساسية |
|---|---|
| `DocumentInterventions` | `Nature`, `ReceiptNumber`, `ReceiptDate`, `AcceptanceDate`, `Subject`, `Notes`, هوية الطبيعي، الممثل الشرعي، حقول الاعتباري، `EntityBranch`, `RegistryId`, `CreatedById`, `CreatedAt`, `UpdatedAt` |
| `DocumentInterventionHeirs` | الاسم الثلاثي، `Capacity`, `AddressType`, `HeirAddress`، الممثل الشرعي، FK إلى `DocumentInterventions` (Cascade) |
| `DocumentOccurrences` (عمود جديد) | `DocumentInterventionId` (FK اختياري Cascade + فهرس) — يحذف الوقعة مع المتدخل ويفتح تفاصيله بلا فكّ JSON |

- الهجرتان: `20261112093527_AddDocumentInterventions` (SQLite) و`20261112093636_AddDocumentInterventions_Pg` (PostgreSQL).
- **لا Backfill**؛ ميزة جديدة لا صفوف سابقة.
- **النشر:** `dotnet ef database update --context DocGeneratorPostgresDbContext` قبل النسخة، و`--context DocGeneratorDbContext` على SQLite (ضمن `G2`).

## 4) سلوك الوقعة

- نوع جديد `intervention` في `OccurrenceTypeCatalog` بتسمية «طلب تدخل».
- `EventDate = ReceiptDate`، `Source = system`، و`Details` لقطة: `{ interventionId, intervenorName, acceptanceDate, nature }`.
- التعديل يعيد كتابة اللقطة والتاريخ؛ الحذف يحذف الوقعة في المعاملة نفسها (أثر التدقيق يبقى).
- السرد: «ورد طلب تدخل من {الاسم} بتاريخ {الورود} (تاريخ قبول طلب التدخل: {القبول})».

## 5) الواجهة

- زر **«＋ إضافة متدخل»** في **نهاية سطر** «صفة الجهة العامة… لا يمكن تغييرها بعد الإنشاء».
- `InterventionModal`: صفة المتدخل، بيانات الطلب، حقول الطرف، منتقي السجل المرجعي، وحفظ/إعادة تعيين/إلغاء.
- `DocumentInterventionsList`: قائمة المحفوظين + تعديل + حذف بتأكيد داخل التطبيق.
- `InterventionDetailsModal`: الهوية/العنوان/الورثة/الممثل أو بيانات الجهة + بيانات الطلب.
- `PartiesCard` و`OccurrencesCard` و`OccurrencesModal` و`DocumentView` و`PortalFileDetail` موسّعة، مع الحفاظ على العرض القرائي في البوابة.
- الجوال: نافذة ورقة، فوتر ثابت، أهداف لمس `min-h-11`، وحقول `AutoResizeTextarea` للنصوص الطويلة.

## 6) الاختبارات المنفَّذة

- `DocumentInterventionServiceTests` (13 اختبارًا): الصفات، الورثة/الممثل، السجل المرجعي، الإلزام والتواريخ، الوقعة، البحث، الحذف، حدود العدد، مستند آخر.
- `DocumentInterventionIntegrationTests` (5 اختبارات): إضافة/قراءة، تعديل/حذف، `400` عربي، حرمان رئيس القسم، حرمان محامٍ آخر.
- واجهة: `InterventionModal.test.tsx` (5)، `DocumentInterventionsList.test.tsx` (4)، `DocumentView.interventions.test.tsx` (3)، واختبارا زر «إضافة متدخل» في `DocumentForm.entityParties.test.tsx`.
- بوابات التحقق: `dotnet test` للخلفية (قيد التشغيل)، `npx oxlint src`، `npx tsc -b`، `npx vitest run`، `npm run build`.

## 7) ملاحظات معمارية (مراجعة نقدية)

- **لا `Version` bump:** حقول النموذج العامة لا تتغير، فلا تُسبب إضافة متدخل `409` لنموذج مفتوح، ولا يمحوها `PUT` العام (جدول مستقل).
- **`SearchText`:** يُعاد بناؤه داخل معاملة الحفظ من المجموعة المحمّلة؛ التزامن على اسم واحد في ملف واحد غير مؤثر عمليًا ويُصحح في أي حفظ لاحق.
- **الملف المناب:** المتدخلون محليون على ملفهم ولا يُنسخون للمناب (سلوك مقصود).
- **الوثائق:** `01-endpoints.md` و`02-authz-matrix.md` لقطتان مجمّدتان وفق سياسة `docs/audit`؛ هذه الوثيقة هي فرق العقود والصلاحيات المؤرخ.
