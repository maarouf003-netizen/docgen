# P2-06 — بوابة اعتماديات فعلية وSBOM — 2026-11-15

> نطاق هذه الجولة: تحويل فحص الاعتماديات من **تقرير** إلى **بوابة**، وإنتاج SBOM
> لكل من الخلفية والواجهة. تبقى بنود SAST/DAST وتفريغ الصور (Trivy) في `P2-06` الكامل.

## 1. المشكلة قبل التغيير

| البند | السلوك السابق | الأثر |
|---|---|---|
| `dotnet list package --vulnerable` | يُطبع التقرير ويعيد **رمز خروج 0** حتى مع ثغرات | خطوة `audit` كانت خضراء دائمًا؛ لا حجب فعلي |
| `npm audit --audit-level=high` | يفشل عند High+ | بوابة حقيقية ✔ |
| SBOM | غير موجود | لا جرد اعتماديات لأي إصدار |
| SAST/DAST/Trivy | غير موجود | يبقى في البند الكامل |

## 2. ما طُبّق

1. **`scripts/dependency-audit.mjs`**:
   - يقرأ `dotnet list backend/DocGenerator.sln package --vulnerable --include-transitive --format json`.
   - يصنّف الشدّة (نصية أو رقمية 0..3):
     - `High`/`Critical` ⇒ **فشل**.
     - `Low`/`Moderate` ⇒ تحذير فقط.
     - شدّة غير معروفة ⇒ **فشل** (fail-closed) حتى تُراجع الصيغة — تغيير مخطط NuGet
       يجب أن يوقف البوابة لا أن يمرّرها بصمت.
   - يتعامل مع غياب `frameworks` (يحذفه `dotnet` عند عدم وجود ثغرات).
   - يفشل صراحةً إذا تعذّر تشغيل `dotnet` أو كانت مخرجات JSON غير صالحة (لا «خضرة كاذبة»).
   - **اختبار ذاتي** (`--self-test`) على خمس عيّنات ثابتة في `scripts/fixtures/`
     (لا ثغرات، High، Critical، شدّة رقمية، Moderate) — يمنع انحدار المحلّل.
2. **`audit` job**:
   - خطوة `Self-test dependency gate parser` ثم `Dependency gate (High/Critical)`،
     و`npm audit --audit-level=high` كما هو.
   - **SBOM**: `CycloneDX 6.3.1` للخلفية (`-t` لاستثناء مشاريع الاختبار) و
     `@cyclonedx/cyclonedx-npm 7.0.1` للواجهة، ويُرفعان كأثر `sbom` (30 يومًا).
   - `cyclonedx-npm` يبني الشجرة عبر `npm ls` فيتطلب `node_modules`، لذلك تُشغَّل
     `npm ci` قبلها (وهي بذاتها تحقق من قابلية القفل لإعادة الإنتاج).
   - الإصدارات مثبّتة، والملفات المولّدة مستثناة في `.gitignore` (`artifacts/`).

## 3. التحقق

- `node scripts/dependency-audit.mjs --self-test` ⇒ نجح (5 عيّنات).
- `node scripts/dependency-audit.mjs` على الحل الحالي ⇒ نظيف («لا High/Critical في
  7 مشروعًا»).
- توليد SBOM محليًا: الخلفية 80 حزمة (`~113KB`)، الواجهة (`~382KB`) — الملفان صالحان.
- `ci.yml` مُحلَّل بمحلّل YAML محلي، وبنية وظيفة `audit` تعرض الخطوات الجديدة.

## 4. حدود

- البوابة تغطي الحزم المعلنة/المنتقلة التي يراها NuGet وnpm؛ لا تغطي كودًا مكتوبًا
  (SAST) ولا الصورة النهائية (Trivy) — يبقى في البند الكامل.
- SBOM يُنتج من `package-lock.json`/بنيان الحل، لا من صورة Docker المنشورة.
- `CycloneDX` يثبّت إصدارًا دقيقًا (6.3.1)؛ ترقيته تغيير واعٍ منفصل.
