# `P4-06d` — تقييم ضجيج `Roslynator`/`StyleCop` بدليل مقيس — 2026-11-26

> **النطاق:** إكمال الدين المؤجل في `P4-06` («تقييم ضجيج مستقل قبل التفعيل») بقياس فعلي
> على شجرة مصدر كاملة، ثم قرار صريح: تفعيل مجموعة صغيرة آمنة أو تأجيل موثّق.
> **لا حزم جديدة**، ولا تعديل `Directory.Packages.props`/`packages.lock.json`، ولا تغيير
> سلوك أو عقد `API` أو هجرة. القياس جرى **خارج الشجرة المتتبَّعة** في نسخة مؤقتة.
>
> **المراجع:** [`47`](47-p4-06-format-gate-20261117.md) (بوابة التنسيق)،
> [`30`](30-p4-06b-lockfiles-20261116.md) (أقفال الاعتماديات)،
> `docs/PERFECTION_ROADMAP.md` §`P4-06` و§6.1، والمراجعتان [`253`](253-p4-06d-critical-review-20261126.md)
> و[`254`](254-p4-06d-adversarial-review-20261126.md).

## 1. السؤال الدقيق

`P4-06` أغلقت التنسيق والاعتماديات، وتركت سؤالًا واحدًا: **هل يستحق `Roslynator`/`StyleCop`
تفعيلهما فوق بوابة `dotnet format` القائمة؟** السؤال ليس تفضيلًا أسلوبيًا: النظام يعمل
بسياسة **صفر تحذيرات** (`TreatWarningsAsErrors` + `-warnaserror` في `CI`)، فأي محلّل يُضاف
إما أن يكون **نظيفًا اليوم** أو يُسقط كل بناء حتى يُصلَح/يُستثنى كل ما ينتجه. لذلك وجب
قياس **العدد والفئات** أولًا، لا الحكم بالانطباع.

الخطر الثاني الذي كشفه التقييم: `dotnet_diagnostic.<ID>.severity` في `.editorconfig`
**لا يفعّل المحلّل**. إعداد قاعدة لمحلّل غير مُشار إليه يمرّر البناء نظيفًا بلا أي فحص —
«خضرة كاذبة». وهذا ما يفسّر لماذا لا تصلح صياغة «تفعيل قواعد بـ`.editorconfig` فقط»
كحل: هي إعداد **صامت** بلا أثر (البرهان في §3.6).

## 2. المنهج (كيف قيس بلا التزام اعتماديات)

1. **نسخة مؤقتة خارج المستودع:** `%TEMP%\opencode\p4-06d-eval\backend`، منسوخة من
   `backend/` بـ`robocopy /E` مع استثناء `bin`/`obj`/`TestResults` و**استثناء
   `packages.lock.json`** (كي لا يتدخل وضع القفل). 1,073 ملف `.cs` — الشجرة كاملة.
2. **الحزم في النسخة المؤقتة فقط:** `PackageReference` لـ`Roslynator.Analyzers` و
   `StyleCop.Analyzers` في `Directory.Build.props` المؤقت + `PackageVersion` في
   `Directory.Packages.props` المؤقت. ملفات المستودع لم تُمسّ (`git diff` على
   `*.csproj`/`*.props`/`*.lock.json` = **0**).
3. **الإصدارات المقيسة:** `Roslynator.Analyzers` **4.16.1** و**5.0.1** (كلاهما قيس؛
   النتائج متطابقة — §3.3)، و`StyleCop.Analyzers` **1.3.180** (آخر مستقر؛ `1.4.x` ما زال
   `beta` فاستُثني عمدًا).
4. **ثلاثة مصادر قياس مستقلة:**
   - **سجل البناء:** `dotnet build DocGenerator.sln --no-restore -v q`
     مع `/p:TreatWarningsAsErrors=false`، ثم **إزالة تكرار** بمفتاح
     `(المعرّف، الملف، السطر، العمود)`: كل تشخيص يظهر **مرتين** في سجل MSBuild، والرقم
     المعتمد هو الفريد (37,631 في الحالة الافتراضية؛ السجل الخام 75,280 سطرًا).
   - **`dotnet format analyzers`:** `--verify-no-changes --report <dir>` ينتج
     `format-report.json` يشمل **كل** التشخيصات (بما فيها `Info`/`Hidden` وقواعد وضع
     التوثيق) — سطح أعلى من بوابة `-warnaserror`.
   - **جرد القواعد:** أداة مؤقتة (`net10.0` + `Microsoft.CodeAnalysis.CSharp 4.7.0`)
     تُحمّل تجميعات المحلّلين بانعكاس وتقرأ `SupportedDiagnostics` (المعرّف، الفئة،
     الشدة الافتراضية، `IsEnabledByDefault`) — 446 صفًا.
5. **التحقق السلبي** (§3.6): إعداد بلا حزمة ⇒ بناء نظيف كاذب؛ حزمة بلا إعداد ⇒ فشل فوري.

## 3. النتائج

### 3.1 مخزون القواعد

| الحزمة | القواعد | الشدة الافتراضية | الفئات |
|---|---:|---|---|
| `Roslynator.Analyzers` 4.16.1 | **243** (`RCS` ×242 + `ROS0003`) | `Warning` **16** · `Info` **148** · `Hidden` **79** | `Roslynator` (و`ROS0003` بلا فئة) |
| `StyleCop.Analyzers` 1.3.180 | **203** (`SA` ×199 + `SX` ×4) | `Warning` **202** · `Hidden` **1** (`SA1119_p`) | 8 فئات + `SpecialRules` |

توزيع `StyleCop` على الفئات: `DocumentationRules` 54، `ReadabilityRules` 43،
`SpacingRules` 29، `LayoutRules` 21، `NamingRules` 20، `MaintainabilityRules` 17،
`OrderingRules` 17، `SpecialRules` 2.

**ملاحظة دلالية مهمة:** `Roslynator` يعرض `IsEnabledByDefault=true` لـ186 قاعدة، لكن
عمله الفعلي مختلف: الافتراضي المقيس على الشجرة = **16 تشخيصًا فقط** من 3 قواعد. فالحكم
على «الضجيج» يجب أن يكون بالقياس لا بوصف الحزمة.

### 3.2 الحالة الافتراضية (`S0`) — الحزم مضافة بلا أي إعداد

| المقياس | `StyleCop` | `Roslynator` | الإجمالي |
|---|---:|---:|---:|
| تشخيصات فريدة في البناء (شدّة `warning`+) | **37,615** | **16** | **37,631** |
| عدد القواعد المسببة | 51 | 3 | 54 |
| سطح `dotnet format` (يشمل `Info`/`Hidden`/وضع التوثيق) | 50,562 | 17 | **50,579** |
| أثره على بوابة `-warnaserror` | يفشل عند **أول** تشخيص | يفشل عند أول 16 | — |

توزيع البناء على الفئات: `ReadabilityRules` 24,462 · `OrderingRules` 4,977 ·
`LayoutRules` 4,199 · `NamingRules` 2,223 · `DocumentationRules` 1,146 ·
`MaintainabilityRules` 606 · `SpacingRules` 2 · `Roslynator` 16.

وإضافة إلى ذلك يطلق `StyleCop` تحذير **`SA0001`** («تحليل تعليقات `XML` معطّل») في كل
مشروع (9 مشاريع) لأن المشاريع لا تولّد ملف توثيق: أي أن تفعيله يفرض قرارًا ثانيًا
(`GenerateDocumentationFile`) لا يخصّ التنسيق.

**ثلاث قواعد `Roslynator` الافتراضية المسببة (16 مخالفة):**

| القاعدة | المعنى | العدد | أين |
|---|---|---:|---|
| `RCS1194` | أنواع الاستثناءات ينبغي أن تنفّذ البانىات الثلاثة | 12 | `DocumentConflictException`، `HeadAlertExceptions`، `IdempotencyGuard` + 3 أنواع اختبار |
| `RCS1075` | تجنّب `catch` الفارغ | 3 | `IdempotencyGuard` ×1، `IdempotencyStore` ×2 |
| `RCS1155` | استخدم `StringComparison` في `string.Compare` | 1 | `AuditLogRepository` |

هذه ليست مخالفات تنسيقية: `RCS1194` يعني **إضافة أعضاء عامة** (سطح `API`)، و`RCS1155`
تغيير **دلالة مقارنة نصية**، و`RCS1075` قرار **سلوك** في مسارات عدم التكرار. أي إصلاح
لها يخرج عن نطاق جولة «بلا تغيير سلوك/عقد».

### 3.3 تفعيل كل قواعد `Roslynator` (`S1`)

لقياس السطح الكامل (لا الافتراضي)، أُضيف في النسخة المؤقتة:
`dotnet_analyzer_diagnostic.category-roslynator.severity = warning`.

| المقياس | `Roslynator` 4.16.1 | `Roslynator` 5.0.1 |
|---|---:|---:|
| تشخيصات البناء الفريدة | **841** | **841** |
| القواعد المسببة | 47 | 47 |
| الإجمالي مع `StyleCop` الافتراضي | 38,456 | 38,456 |
| سطح `dotnet format` (كل الشدات) | 2,788 (54 قاعدة) | — |

**تطابق 4.16.1 و5.0.1 حرفيًا** (841/47 والتوزيع نفسه) ⇒ الاستنتاج غير حسّاس لإصدار
`Roslynator` في نطاق القياس. أعلى القواعد: `RCS1001` 217، `RCS1077` 134، `RCS1261` 85،
`RCS1249` 34، `RCS1181` 32، `RCS1146` 28، `RCS1201` 24، `RCS1112` 23، `RCS1205` 23.

### 3.4 جدول القواعد — الحالة الافتراضية (54 قاعدة، 37,631 تشخيصًا)

| القاعدة | المخالفات | الفئة |
|---|---:|---|
| SA1101 | 21571 | StyleCop.CSharp.ReadabilityRules |
| SA1200 | 4389 | StyleCop.CSharp.OrderingRules |
| SA1309 | 2163 | StyleCop.CSharp.NamingRules |
| SA1503 | 2085 | StyleCop.CSharp.LayoutRules |
| SA1117 | 1752 | StyleCop.CSharp.ReadabilityRules |
| SA1515 | 1106 | StyleCop.CSharp.LayoutRules |
| SA1633 | 1058 | StyleCop.CSharp.DocumentationRules |
| SA1116 | 832 | StyleCop.CSharp.ReadabilityRules |
| SA1402 | 553 | StyleCop.CSharp.MaintainabilityRules |
| SA1513 | 359 | StyleCop.CSharp.LayoutRules |
| SA1202 | 272 | StyleCop.CSharp.OrderingRules |
| SA1516 | 191 | StyleCop.CSharp.LayoutRules |
| SA1122 | 183 | StyleCop.CSharp.ReadabilityRules |
| SA1519 | 147 | StyleCop.CSharp.LayoutRules |
| SA1512 | 110 | StyleCop.CSharp.LayoutRules |
| SA1201 | 91 | StyleCop.CSharp.OrderingRules |
| SA1649 | 88 | StyleCop.CSharp.DocumentationRules |
| SA1210 | 83 | StyleCop.CSharp.OrderingRules |
| SA1507 | 83 | StyleCop.CSharp.LayoutRules |
| SA1204 | 81 | StyleCop.CSharp.OrderingRules |
| SA1118 | 49 | StyleCop.CSharp.ReadabilityRules |
| SA1501 | 37 | StyleCop.CSharp.LayoutRules |
| SA1216 | 33 | StyleCop.CSharp.OrderingRules |
| SA1502 | 28 | StyleCop.CSharp.LayoutRules |
| SA1505 | 27 | StyleCop.CSharp.LayoutRules |
| SA1413 | 26 | StyleCop.CSharp.MaintainabilityRules |
| SA1143 | 23 | StyleCop.CSharp.ReadabilityRules |
| SA1316 | 19 | StyleCop.CSharp.NamingRules |
| SA1315 | 18 | StyleCop.CSharp.NamingRules |
| SA1128 | 18 | StyleCop.CSharp.ReadabilityRules |
| SA1107 | 18 | StyleCop.CSharp.ReadabilityRules |
| SA1203 | 15 | StyleCop.CSharp.OrderingRules |
| SA1500 | 15 | StyleCop.CSharp.LayoutRules |
| SA1407 | 15 | StyleCop.CSharp.MaintainabilityRules |
| SA1308 | 15 | StyleCop.CSharp.NamingRules |
| SA1208 | 13 | StyleCop.CSharp.OrderingRules |
| RCS1194 | 12 | Roslynator |
| SA1127 | 10 | StyleCop.CSharp.ReadabilityRules |
| SA1508 | 9 | StyleCop.CSharp.LayoutRules |
| SA1401 | 5 | StyleCop.CSharp.MaintainabilityRules |
| SA1119 | 4 | StyleCop.CSharp.MaintainabilityRules |
| SA1414 | 4 | StyleCop.CSharp.ReadabilityRules |
| RCS1075 | 3 | Roslynator |
| SA1300 | 3 | StyleCop.CSharp.NamingRules |
| SA1408 | 3 | StyleCop.CSharp.MaintainabilityRules |
| SA1025 | 2 | StyleCop.CSharp.SpacingRules |
| SA1306 | 2 | StyleCop.CSharp.NamingRules |
| SA1313 | 2 | StyleCop.CSharp.NamingRules |
| SA1518 | 1 | StyleCop.CSharp.LayoutRules |
| SA1520 | 1 | StyleCop.CSharp.LayoutRules |
| RCS1155 | 1 | Roslynator |
| SA1124 | 1 | StyleCop.CSharp.ReadabilityRules |
| SA1303 | 1 | StyleCop.CSharp.NamingRules |
| SA1137 | 1 | StyleCop.CSharp.ReadabilityRules |

### 3.5 جدول قواعد `Roslynator` عند تفعيلها كلها (47 قاعدة، 841 تشخيصًا)

| القاعدة | المخالفات | القاعدة | المخالفات | القاعدة | المخالفات |
|---|---:|---|---:|---|---:|
| RCS1001 | 217 | RCS1118 | 17 | RCS1190 | 4 |
| RCS1077 | 134 | RCS1123 | 15 | RCS1075 | 3 |
| RCS1261 | 85 | RCS1238 | 13 | RCS1032 | 3 |
| RCS1249 | 34 | RCS1194 | 12 | RCS1015 | 3 |
| RCS1181 | 32 | RCS1085 | 10 | RCS1089 | 3 |
| RCS1146 | 28 | RCS1113 | 10 | RCS1214 | 3 |
| RCS1201 | 24 | RCS1163 | 9 | RCS1186 | 3 |
| RCS1112 | 23 | RCS1003 | 9 | RCS1192 | 3 |
| RCS1205 | 23 | RCS1196 | 9 | RCS1267 | 2 |
| RCS1173 | 21 | RCS1161 | 8 | RCS1047 | 2 |
| RCS1124 | 19 | RCS1257 | 8 | RCS1199 | 1 |
| RCS1168 | 18 | RCS1156 | 7 | RCS1155 | 1 |
| RCS1031 | 7 | RCS1251 | 6 | RCS1213 | 1 |
| RCS1246 | 4 | RCS1068 | 1 | RCS1217 | 1 |
| RCS1227 | 1 | RCS1189 | 1 | RCS1259 | 1 |
| RCS1235 | 1 | RCS1074 | 1 | — | — |

القواعد الصفرية **على سطح `dotnet format` الكامل**: **188** من 242 قاعدة `RCS`، و**133** من
199 قاعدة `SA` (وبحسب مخالفات البناء وحده: 195 و148) — أي أن «مجموعة صفرية المخالفات»
موجودة بكثرة، لكنها (§4.2) لا تُنفَّذ بلا الحزمة.

### 3.6 التحقق السلبي المزدوج (جوهر القرار)

| التجربة | الإعداد | النتيجة |
|---|---|---|
| **(أ) إعداد بلا حزمة** | `.editorconfig` يحوي `dotnet_diagnostic.SA1101.severity = error` و`SA1503` و`SX1101` و`RCS1194`، والمشاريع **بلا** أي حزمة محلّل | `dotnet build -warnaserror` ⇒ **نجح، 0 Warning / 0 Error** مع وجود 21,571 مخالفة `SA1101` حقيقية في المصدر — **خضرة كاذبة مُبرهنة** |
| **(ب) حزمة بلا إعداد** | `StyleCop` مضاف، بلا أي إعداد | `dotnet build -warnaserror` ⇒ **فشل** (exit 1)، أوقف البناء عند أول مشروع: **235 خطأ** في `DocGenerator.Domain` وحده |
| **(ج) حزمة + إعداد لقاعدة واحدة** | `StyleCop` + `dotnet_diagnostic.SA1101.severity = error`، مع `/p:TreatWarningsAsErrors=false` وبلا `-warnaserror` | **فشل** (exit 1) بخطأ `SA1101` فعلي («Prefix local calls with this») — الإعداد صار منفّذًا لأن الحزمة محمّلة، وهذا يثبت أن أثر الإعداد يأتي من الحزمة لا من نصّه |

**الاستنتاج:** الشرط الحقيقي للتفعيل هو وجود الحزمة في الترجمة؛ وبدونها كل إعداد شدة
«زينة» لا تُسقط أي بوابة. لذلك صار هذا الشرط **حارسًا آليًا** (§5).

## 4. القرار

### 4.1 `StyleCop` — **لا تفعيل** (تأكيد قرار النطاق §6.1 بدليل)

- 37,615 مخالفة افتراضية في 51 قاعدة، تُسقط `-warnaserror` فورًا.
- لتبقى البوابة خضراء يجب تعطيل **51 قاعدة** — هي التي تنتج الـ37,615 مخالفة؛ وما تبقّى
  من قواعد الحزمة (152) بلا مخالفات أصلًا، فلا يُضيف إنفاذًا ذا قيمة (`SA1101` وحدها
  21,571 لأن الشيفرة لا تستخدم `this.`).
- القواعد المتبقية القابلة للتفعيل بلا مخالفات لا تغطي إلا شريحة صغيرة، وتكلفة
  الالتزام بها (تنسيق كل ملف جديد بترتيب `StyleCop` الصارم + `SA0001` + `SA1633`
  لرؤوس الملفات) قرار أسلوبي شامل لا يخصّ بوابة جودة.
- **النتيجة:** تعطيل فعلي = صفر إنفاذ؛ تفعيل فعلي = 37,615 إصلاحًا تنسيقيًا في جولة لا
  تسمح بتغيير كود. لذلك يبقى **متجاوزًا** كما في §6.1، والآن **بدليل مقيس** لا بانطباع.

### 4.2 `Roslynator` — **تأجيل** (وليس تفعيل مجموعة صفرية)

- الافتراضي ليس صفرًا: **16 مخالفة** في 3 قواعد، وكلها **ليست تنسيقية**:
  `RCS1194` (12) = إضافة بانىات عامة لأنواع استثناء = **سطح `API`**، `RCS1155` (1) =
  تغيير دلالة مقارنة نصية، `RCS1075` (3) = قرار سلوك في `catch` فارغ.
- «تفعيل مجموعة صفرية المخالفات» يقتضي إضافة الحزمة أولًا (وإلا فالإعداد صامت — §3.6)،
  ثم **كبت** القواعد الثلاث الافتراضية المسببة (`none`) لتظل `-warnaserror` خضراء — أي دفع
  ثمن اعتمادية جديدة + تحديث **9** ملفات `packages.lock.json` مقابل **كبت** القواعد الوحيدة
  التي تلتقط مشكلات حقيقية في هذا المستودع.
- البديل (إصلاح الـ16) ممنوع في هذه الجولة: يغيّر سطحًا عامًا وسلوك مقارنة/`catch`.
- **النتيجة:** التأجيل هو القرار الصحيح؛ البيانات لا تُظهر «مكسبًا نظيفًا منخفض المخاطر»
  الذي يشترطه تفعيل الحزم.

### 4.3 شرط إعادة النظر (دقيق)

| الحزمة | الشرط | الكلفة المقيسة | ما يلزم |
|---|---|---|---|
| `Roslynator.Analyzers` | جولة **تسمح بتغيير كود/`API`** وتريد إنفاذ `RCS` | 16 إصلاحًا (أو كبت 4 قواعد معلّل) + 841 إن فعّلت الكل | `PackageVersion` في `Directory.Packages.props` + `PackageReference` في `Directory.Build.props` + تحديث **9** أقفال + إبقاء `-warnaserror` أخضر |
| `StyleCop.Analyzers` | قرار مالك صريح بتبنّي أسلوب `StyleCop` كاملًا (مشروع تنسيق مستقل) | 37,615 إصلاحًا في 51 قاعدة + قرار `GenerateDocumentationFile` | جولة مستقلة، لا تُدمج مع تغيير وظيفي |

**لا إعادة نظر تلقائية:** إضافة أي من الحزمتين بلا معالجة هذه الأرقام تُسقط `CI` فورًا —
وهذا هو الإنفاذ الطبيعي، فلا حاجة لحارس إضافي يمنع الإضافة.

## 5. الحارس المُضاف (بدل تفعيل غير فعّال)

`backend/tests/DocGenerator.Architecture.Tests/AnalyzerConfigurationTests.cs` +
`AnalyzerConfigurationPolicy.cs`:

1. **الفحص الحقيقي:** يجمع كل `.editorconfig` **و`.globalconfig`** في المستودع (مع تشذيب
   `bin`/`obj`/`node_modules`/`dist`/…) وكل `.csproj`/`.props`/`.targets` تحت `backend/`،
   ويفشل إن وُجد `dotnet_diagnostic.<RCS|SA|SX>.severity` بلا حزمة **مسنودة لكل المشاريع**:
   `PackageReference` في `Directory.Build.props`/`Directory.Build.targets` يغطي المشاريع
   **تحت مجلده فقط** (كما يفعل MSBuild)، أو إشارة في **كل** ملفات المشاريع. سبب شرط
   الشمول: إعداد `.editorconfig` عام لكل `*.cs`، فحزمة في مشروع واحد (أو في
   `backend/tests/Directory.Build.props` وحده) تعني أن بقية المشاريع تتجاهل الإعداد
   بصمت. والقراءة **غير حساسة لحالة الأحرف** لأن Roslyn يقرأ أسماء الخصائص كذلك.
2. **التحقق الذاتي (لا حارس أجوف):** 12 حالة (1 على المستودع + 3 `Theory` لعائلات
   `SA`/`SX`/`RCS` + 8 للمحرّك) تُثبت أن الحارس **يفشل فعلًا** عند إعداد بلا حزمة، وأنه
   يرفض حزمةً في مشروع واحد فقط أو في `Directory.Build.props` فرعي لا يغطي الجميع،
   ويقبل تغطيةً شاملة، ويتجاهل عائلات `SDK` المدمجة (`CA`/`IDE`)، والأسطر المعلّقة
   (`#`/`;`)، و`PackageVersion` وحدها (تعريف إصدار ليس تحميلًا)، ويلتقط المفاتيح
   بأحرف مختلطة.
3. **تحقق على المستودع الفعلي:** أُدخل مؤقتًا `dotnet_diagnostic.SA1101.severity = error`
   في `.editorconfig` الجذري ⇒ **FAIL**؛ وفي `.editorconfig` **متداخل** تحت
   `backend/src/` ⇒ **FAIL**؛ وفي **`.globalconfig`** جذري ⇒ **FAIL** (الحارس لا يفحص
   الجذري وحده ولا نوعًا واحدًا)؛ ثم أُعيد كل شيء ⇒ **12/12 نجاح**. لا أثر للطفرات في
   الالتزام.

الحد المعلن: الحارس يتحقق من **وجود الحزمة في كل المشاريع** لا من دلالة الشدة ولا من أن
القاعدة مُفعّلة فعليًا بعد `roslynator_*`؛ ومن يضيف الحزمة ثم يكبت كل القواعد يمرّ — وهذا
قرار ظاهر في المراجعة ومقصود (كبت صريح ≠ خضرة كاذبة). كما أنه لا ينمذج `glob` في
`.editorconfig`: إعداد داخل `[*.vb]` بلا حزمة يُرفض أيضًا (فشل مغلق، لا تمرير).

## 6. الملفات

| الملف | الحالة | الدور |
|---|---|---|
| `backend/tests/DocGenerator.Architecture.Tests/AnalyzerConfigurationPolicy.cs` | جديد | منطق الاتساق (حزمة مسنودة لكل المشاريع ↔ إعداد) |
| `backend/tests/DocGenerator.Architecture.Tests/AnalyzerConfigurationTests.cs` | جديد | 12 حالة اختبار (1 للمستودع + 3 `Theory` + 8 للمحرّك) |
| `backend/tests/DocGenerator.Architecture.Tests/RepositoryLayout.cs` | محدَّث | `RepositoryRoot()` مشتركة بدل تكرار الصعود |
| `.editorconfig` | محدَّث (تعليق فقط) | توثيق التأجيل + قاعدة «لا شدة بلا حزمة» |
| `docs/PERFECTION_ROADMAP.md` | محدَّث | حالة `P4-06d` ونتيجة التقييم |
| `docs/README.md` | محدَّث | فهرس اللقطات `252`–`254` |

**لم تُضف أي حزمة ولم يُعدّل أي `lockfile`** (`git diff` على
`backend/**/*.csproj`، `Directory.Packages.props`، `packages.lock.json`،
`frontend/package*.json` = **0**).

## 7. التحقق (محلي)

| البوابة | الأمر | النتيجة |
|---|---|---|
| البناء | `dotnet build backend/DocGenerator.sln -warnaserror` | **0 Warning / 0 Error** |
| التنسيق | `dotnet format … --verify-no-changes --no-restore` | `exit 0` |
| المعمارية | `DocGenerator.Architecture.Tests` | **41/41** (29 سابقة + 12) |
| `Application` | `dotnet test …Application.Tests` | **772 passed / 1661 skipped / 2433** (لا `PostgreSQL` محليًا) |
| `Api` (غير تسلسلي) | `--filter "Category!=Sequential"` | **905 / 615 skipped / 1520** |
| `Api` (تسلسلي) | `--filter "Category=Sequential"` | **0 / 5 skipped** (بوابة `PostgreSQL`) |
| الحراس الستة | `subhead`/`determinism`/`migration-watchlist`/`migration-safety`/`secrets`/`postgres-suite --self-test` | **0,0,0,0,0,0** (7 هجرات مسجّلة، 6 سطور غير مشطوب) |
| تدقيق الاعتماديات | `dependency-audit --self-test` + `dependency-audit` + `npm-audit-guard --self-test` + `npm-audit-guard` | **0,0,0,0**؛ لا High/Critical |
| EF | `has-pending-model-changes` (سلسلة وهمية، `--no-build`) | «No changes… since the last migration» |
| الواجهة | `npx oxlint src scripts` / `npx tsc -b` / `npm run build` / `npm run guard` | **0 / 0 / ناجح / 94 ملفًا** |
| `vitest` | `npx vitest run` | **1809/1809** في **171** ملفًا (لا ملفات واجهة مُعدَّلة) |
| العميل المولَّد | `npm run generate:api` ثم `git diff --exit-code -- src/api/generated.ts` | **exit 0** بعد إعادة ضجيج `CRLF` (`git checkout`) |

## 8. الحدود

1. **القياس على نسخة طبق الأصل** لا على المستودع نفسه: نفس الملفات (1,073 `.cs`) ونفس
   المشاريع، لكن مسار مختلف — لا أثر على النتائج لأن المحلّلات تعمل لكل تجميعة.
2. **`StyleCop` 1.4.x (beta) لم يُقس**؛ الفارق المتوقع لا يغيّر الحكم (37,615 مقابل
   عتبة صفر)، لكنه حدّ معلن.
3. **وضع التوثيق:** سطح `dotnet format` (50,562 لـ`StyleCop`) يشمل قواعد `SA16xx`
   التي لا تعمل في البناء ما لم يُفعّل `GenerateDocumentationFile`؛ الرقم المعتمد للبوابة
   هو 37,615.
4. **أرقام `dotnet format` أعلى من البناء** لأنها تشمل `Info`/`Hidden`؛ لا تُقارن مباشرة
   بعتبة `-warnaserror`.
5. **الحارس لا يفهم دلالة الشدة** (خطأ/تحذير/كبت) — فقط الاتساق مع الاعتماديات.

## 9. الهجرات

**لا هجرة جديدة.** لا تغيير في نموذج `EF` (`has-pending-model-changes` نظيف)، والقائمة
المعلّقة تبقى **6** كما في `RUN_GUIDE.md` §9 و`AGENTS.md`.
