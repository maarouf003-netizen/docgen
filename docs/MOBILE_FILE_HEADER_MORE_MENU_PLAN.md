# الخطة النهائية المحكمة: ترويسة الملف (قائمة المزيد) + الهوية المدمجة + صفحة مراسلات مستقلة — بانتظار أمر التنفيذ

> الحالة: خطة نهائية مفصلة — **لا تنفيذ كود قبل موافقة صريحة** على هذه النسخة.
> المراجع: `frontend/src/pages/DocumentView.tsx:126-132` (تأثير تنظيف `id`) `168-170` (`canCreateAppeal`) `342-471` (الترويسة والهوية)، `frontend/src/pages/PortalFileDetail.tsx:313-357`، `frontend/src/hooks/useFloatingMenu.ts`، `frontend/src/hooks/useMediaQuery.ts:22`، `frontend/src/components/correspondence/DocumentCorrespondenceCard.tsx:27-63`، `frontend/src/utils/documentDisplay.ts:61-65`، `frontend/src/components/view/viewFormat.ts:60-66`، `frontend/src/components/view/FileDataCard.tsx:82-90`، `frontend/src/pages/DocumentsList.tsx:183-242` (نمط `المزيد` القائم)، `frontend/src/App.tsx:80-81` (`allowInternal`) `295-337` (مسارات البوابة)، `frontend/src/pages/CorrespondenceDetail.tsx:76-78`.

## 1) القرارات المثبتة (نهائية — لا يُنفَّذ خارجها)

| # | القرار | المثبت |
|---|---|---|
| 1 | نقطة كسر `المزيد` | `1023px` الموحد عبر `useIsMobile()` نفسه (اتساق مع تبويبات الأقسام؛ يشمل اللوحي — مقصود) |
| 2 | لوحة الاستئناف | **متداخلة داخل قائمة `المزيد`** بحالة مستقلة `appealSubOpen` — بلا خطاف ثالث وبلا `createPortal` ثانٍ (تفادي تعارض `useDismiss`) |
| 3 | حالة لا-صلاحية المراسلات | إضافة Prop اختياري `onAccessDenied?: () => void` إلى `DocumentCorrespondenceCard` (غيابه = السلوك الحالي `return null`) |
| 4 | سلوك المسودة (تحت رفع) | البطاقة المدمجة تعرض `—` للمسودة — اتساقًا مع القائمة ومع دلالات `displayFileNumber()` |
| 5 | قيمة بطاقة النوع | `doc.fileType` **وحده** (مقصوصًا؛ بلا احتياط `documentType`)، وعند غيابه **فراغ** (قيمة فارغة والبطاقة ظاهرة) — توجيه صريح يخرج عن اصطلاح `—` في بقية البطاقات، يُراجَع بصريًا عند التنفيذ |
| 6 | حذف بطاقة المراسلات | نهائي — جوال ومكتبي، داخلي وبوابة؛ وزر `مراسلات` يصبح `Link` انتقال لا تمرير |
| 7 | رجوع تفاصيل المراسلة | لاحقًا — يُوثَّق كمتابعة (`state.from` في `CorrespondenceDetail`) دون مساس الآن |
| 8 | المساران الجديدان (افتراض بلا اعتراض) | `/documents/:id/correspondence` (داخلي) + `/portal/files/:id/correspondence` (بوابة) |
| 9 | `FileDataCard` (افتراض بلا اعتراض) | تُترك كما هي (ازدواج الرقم/النوع مع الشريط اللاصق مقبول مؤقتًا) |
| 10 | تسمية البطاقة المدمجة | `dt` = **«رقم الملف والسنة»** (تغطي القيمتين المدمجتين) + تحديث الاختبار تبعًا لذلك |
| 11 | وصولية اللوحة | اللوحة الخارجية **`role="dialog" aria-label="المزيد"`** (popover عام — `Tab` طبيعي)؛ بند `استئناف ▾` بزر `aria-expanded` + `aria-controls`، واللوحة المتداخلة `role="menu" aria-label="نوع الاستئناف"` |

## 2) النطاق الدقيق للملفات

| الملف | التغيير |
|---|---|
| `frontend/src/pages/DocumentView.tsx` | حصر الأزرار الخمسة بـ `!isMobile` + قائمة `المزيد` عند `isMobile` + `appealSubOpen` + هوية مدمجة (كل المقاسات) + زر `مراسلات` يصبح `Link` + حذف `DocumentCorrespondenceCard` من `securityPanel` + تنظيف `moreMenu`/`appealSubOpen` عند تبديل `id` |
| `frontend/src/pages/PortalFileDetail.tsx` | هوية مدمجة (كل المقاسات) + زر `مراسلات` يصبح `Link` للبوابة + حذف `DocumentCorrespondenceCard` |
| `frontend/src/components/view/viewFormat.ts` | دالة مشتركة جديدة `identityFileNumber(doc)` للقيمة المدمجة (تستهلكها الصفحتان — بلا تكرار) |
| `frontend/src/components/correspondence/DocumentCorrespondenceCard.tsx` | إضافة `onAccessDenied?` فقط (عقد متوافق رجعيًا) |
| `frontend/src/pages/FileCorrespondence.tsx` **جديد** | صفحة داخلية رفيعة: عودة للملف + عنوان + البطاقة |
| `frontend/src/pages/PortalFileCorrespondence.tsx` **جديد** | نظير البوابة (`portal`) |
| `frontend/src/App.tsx` | مساران جديدان داخل الحراس القائمين فقط |
| الاختبارات | تحديث `DocumentView.test.tsx` + `PortalFileDetail.test.tsx` + ملفا اختبار جديدان للصفحتين (بند §7) |
| خارج النطاق | `FileDataCard`/`formatFileNumber`، `CorrespondenceDetail`، الخلفية، الهجرات (لا شيء منها) |

## 3) المواصفة (أ): قائمة `المزيد` — عند `isMobile` فقط في `DocumentView`

* **جدول أحكام المقاس (إلزامي — منع تكرار الإجراءات على الجوال):**

  | العنصر | الشرط |
  |---|---|
  | `تعديل`، زر `استئناف ▾` المكتبي، `توليد مستندات`، `توجيه تنبيه`، `نقل الملف` | `!isMobile && <الشرط الحالي حرفيًا>` |
  | زر `المزيد ▾` + لوحته | `isMobile && …` (بنود اللوحة بشروط الظهور الحالية حرفيًا) |
  | `الإجراءات والملاحظات` + `عودة` | بلا شرط (ظاهران دائمًا كما اليوم) |

* البنية: خطاف واحد إضافي `moreMenu = useFloatingMenu()` بجانب `appealMenu` القائم (يبقى لسطح المكتب مقيَّدًا بـ `!isMobile`). زر `المزيد ▾` بنمط زر `المزيد` القائم (`DocumentsList.tsx:188-209`: `aria-expanded` + `aria-haspopup="menu"` + سهم SVG + `min-h-11`)، واللوحة الخارجية **`role="dialog" aria-label="المزيد"`** عبر `createPortal(document.body)` بعرض **`w-56`** على الأقل (قرار وصولية §1-11 — لا `role="menu"` خارجيًا فيبقى `Tab` طبيعيًا).
* البنود (بنفس شروط الظهور الحالية حرفيًا، `min-h-11` + `w-full` + خلفية كل زر الأصلية + `hover` أغمق خاص بكل لون + `focus-visible:ring-*` خاص):
  1. `تعديل` (`bg-emerald-800`) — عند `canEdit` — `Link` إلى `/documents/:id/edit` (نمط `Link role="menuitem"` كما في `DocumentsList.tsx:220-229`).
  2. `مراسلات` (`bg-sky-800`) — دائم — `Link` إلى `/documents/:id/correspondence`.
  3. `استئناف ▾` (`bg-[#800000]`) — عند `canCreateAppeal` — زر `aria-expanded={appealSubOpen}` + `aria-controls="appeal-submenu"` يوسّع/يطوي **لوحة متداخلة** `id="appeal-submenu" role="menu" aria-label="نوع الاستئناف"` داخل اللوحة الأم تعرض `مستأنِفين` و`مستأنف علينا` (`appealSubOpen` حالة `useState` مستقلة — **ممنوع** إعادة استعمال `appealMenu.open`)؛ الاختيار يغلق `moreMenu` ويصفّر `appealSubOpen` ثم `setAppealFormVariant(direction)` (نفس `AppealFormModal` دون مساس).
  4. `توليد مستندات` (`bg-gray-800`) — عند `!isExecuted && !isDelegationExecuted` — يغلق ويفتح `DocumentGenerationModal`.
  5. `توجيه تنبيه` (`bg-red-600`) — عند `canDirectAlert`.
  6. `نقل الملف` (`bg-sky-800`) — عند `canTransfer`.
* خارج القائمة يبقى ظاهرًا فقط: `الإجراءات والملاحظات` + `عودة`. لا فرع "قائمة فارغة" (بند `مراسلات` دائم — بلا شِفرة ميتة).
* الإغلاق: اختيار بند، نقر خارج اللوحة، أو `Escape` (عبر `useDismiss` الجاهز)؛ وإضافة `moreMenu.setOpen(false)` + `setAppealSubOpen(false)` إلى تأثير تنظيف `id` القائم (`DocumentView.tsx:126-132`).
* `PortalFileDetail`: بلا قائمة (زر وحيد أصلًا) — يُستبدل زر التمرير بـ `Link` للصفحة (§5) فقط.

## 4) المواصفة (ب): شريط الهوية — كل المقاسات في الصفحتين

* الصيغة:
  ```text
  [رقم الملف والسنة: 66 / 2026]  [نوع الملف: <fileType أو فراغ>]  [الدائرة: <court أو —>]  (+ [الفرع] في DocumentView عند showBranch كما هو)
  ```
* الدالة المشتركة `identityFileNumber(doc)` في `viewFormat.ts` (وحدة قابلة للاختبار):
  * الرقم: `(doc.displayFileNumber ?? doc.fileNumber ?? '').trim()` والسنة مثله؛ الدمج رقم + ` / ` + سنة؛ غياب أحدهما يعرض الآخر وحده؛ غيابهما (أو `isDraft`) → `—`. (ملاحظة: `||` بعد `trim()` لا `??` — تغطية الفراغ النصي الكامن في `DocumentView.tsx:453,458` و`PortalFileDetail.tsx:344,350`).
  * القيمة الرقمية داخل `dir="ltr"` + `isolate` + `tabular-nums` (منع أثر bidi في السياق RTL).
* النوع: `(doc.fileType ?? '').trim()` — فراغ عند الغياب (قرار §1-5، يُراجَع بصريًا).
* الدائرة: `doc.court || '—'` كما هي. `dl` يبقى بأزواج `dt/dd` صحيحة.

## 5) المواصفة (ج): صفحتا المراسلات + الحذف

* المساران في `App.tsx` (إضافة فقط، بلا مساس بالقائم):
  * `/documents/:id/correspondence` → `<FileCorrespondence />` داخل `<RequireRole allowed={allowInternal}>` (أي: محامٍ/رئيس/مدير/مشرف — `App.tsx:80-81`).
  * `/portal/files/:id/correspondence` → `<PortalFileCorrespondence />` داخل حارس `entitymanager` (نظير `‎/portal/files/:id` في `App.tsx:317-321`).
  * لا تعارض ترتيب (المقطع الثابت `correspondence` يتفوق على `:id` في `react-router v7`).
* الصفحتان (wrapper رفيع، يُعاد استعمال `DocumentCorrespondenceCard` نقلًا لا نسخًا):
  * رابط `عودة إلى الملف` (`/documents/:id` أو `/portal/files/:id`) + عنوان الملف (عبر `GET /documents/:id` أو `/portal/files/:id` — طلب إضافي واحد موثّق، لازم لاسم نافذة `تسطير مراسلة`).
  * `documentTitle` = **عبارة البطاقة حرفيًا**: `fullName(doc) || doc.documentType || undefined` (أي `debtorFullName || doc.documentType` كما في `DocumentView.tsx:247` — **لا** عبارة الترويسة `executedTitle(doc)` في `:339`، وإلا تغيّر عنوان نافذة التسطير لملفات المنفَّذ).
  * صلاحية الإنشاء: الداخلية `(canEdit && isOwner) || canTransfer` (شرط البطاقة اليوم — `DocumentView.tsx:248`)؛ البوابة `true` (كما اليوم — `PortalFileDetail.tsx:411`).
  * `onAccessDenied` → تنبيه `role="alert"` (`لا صلاحية لعرض مراسلات هذا الملف`) + رابط العودة (لا صفحة بيضاء).
  * روابط عناصر القائمة تبقى `/correspondence/:id` (داخلي) أو `/portal/correspondence/:id` (بوابة حسب `portal` — `DocumentCorrespondenceCard.tsx:27`) دون تغيير.
* الحذف: إزالة البطاقة من `securityPanel` (`DocumentView.tsx:244-250`) ومن أسفل `PortalFileDetail` (`:406-412`)، وإزالة كود التمرير (`href="#file-correspondence"` + `scrollIntoView` في الملفين) واستبداله بالروابط. ملاحظات التنظيف: `id="file-correspondence"` و`scroll-mt-24` في البطاقة يصبحان بلا مرجع (يُبقيان بلا ضرر أو يُحذفان — يُذكر في تقرير الإنجاز أيًّا كان المختار)، وحارس `prefers-reduced-motion` في معالج التمرير يُحذف معه (**تحسّن**: لم يعد لازمًا بلا تمرير برمجي). كسر مقصود موثّق: إشارات `#file-correspondence` القديمة تتوقف.
* متابعة موثقة (لا تُنفَّذ): `state.from` في `CorrespondenceDetail` ليعيد "عودة إلى المراسلات" إلى مراسلات الملف.

## 6) الحالات الحدية (قائمة تحقق)

1. جوال بلا `canCreateAppeal`: `المزيد` بلا بند استئناف؛ لا `AppealFormModal`.
2. ملف منفذ/منفذ إنابة: لا `توليد مستندات` (جوال ومكتبي — الشرط القديم).
3. سنة غائبة → الرقم وحده؛ رقمان غائبان/مسودة → `—`؛ نوع غائب → فراغ.
4. مراسلات فارغة → نص البطاقة القائم + زر التسطير حسب الصلاحية.
5. 403/404 على الصفحة → تنبيه + عودة (لا فراغ)؛ وفي الاستعمال المضمّن القديم (غير موجود بعد الحذف) يبقى `null`.
6. إغلاق بـ `Escape`/خارج اللوحة/تبديل `id` — لا قائمة معلقة (ويُصفَّر `appealSubOpen`).
7. أول رسم على الجوال يعرض مسار الجوال مباشرة (القيمة الابتدائية لـ `useIsMobile` — بلا وميض).
8. على الجوال: لا زر `تعديل`/`استئناف`/`نقل الملف` ظاهر خارج `المزيد` (منع تكرار — اختبار §7).

## 7) الاختبارات (إلزامي مع التنفيذ)

* `DocumentView.test.tsx`:
  * تحديث `2221-2229`: `getByText('رقم الملف والسنة')` بدل `getByText('رقم الملف')`، وعقدة مدمجة `66 / 2026`-الشكل بدل `99`/`2026` منفصلتين (`getByText('2026')` وحده سيفشل).
  * مراجعة سلاسل `mockResolvedValueOnce` في المواضع (**طلب المراسلات هو الخامس**: `887`، `991`، `1032`، `1076`، `1155`، `1391`، `1437`) بعد زواله (انزياح استجابة `Blob`)؛ وفحص احتياطي لسلسلة `1218-1219` (بلا طلب مراسلات).
  * إضافة `stubMobile(false)` في `beforeEach` (منع تسريب `stubMobile(true)` من اختبار التبويبات).
  * جديد (جوال): `المزيد` ظاهرة و`الإجراءات والملاحظات`/`عودة` خارجها؛ **غياب** `تعديل`/`استئناف`/`نقل الملف` قبل فتح `المزيد` (منع تكرار)؛ فتح `المزيد` ← `استئناف` (`aria-expanded=true`) ← اللوحة المتداخلة ← `مستأنِفين` يفتح `AppealFormModal`؛ رابط `مراسلات` إلى `/documents/1/correspondence`؛ الهوية `رقم الملف والسنة` + القيمة المدمجة + فراغ النوع.
* `PortalFileDetail.test.tsx`: تحديث `174-175` (التسمية الجديدة + الدمج)؛ رابط `/portal/files/1/correspondence`؛ غياب البطاقة؛ `stubMobile(false)` في `beforeEach`.
* جديدان: `FileCorrespondence.test.tsx` + `PortalFileCorrespondence.test.tsx` (عرض القائمة، التسطير حسب الصلاحية، 403 → تنبيه + عودة، رابط العودة للملف، عنوان النافذة من عبارة البطاقة).
* وحدة: `identityFileNumber` (رقم+سنة، سنة غائبة، رقمان غائبان، مسودة → `—`).

## 8) التحقق الكامل بعد التنفيذ (إلزامي)

## 9) ملحق التنفيذ الفعلي (انحرافات موثقة عن §§1-7 — بتاريخ التنفيذ)

> كل بند أدناه انحراف مقصود عن الخطة أعلاه مع سببه؛ الباقي نُفذ حرفيًا.

| # | البند | المنفذ | السبب/الدليل |
|---|---|---|---|
| 1 | §1-11: اللوحة الخارجية `role="dialog"` | أصبحت **`role="menu"`** + `FloatingFocusManager` (`modal={false}` + `initialFocus={0}`) + `useListNavigation` عبر `useFloatingMenu({ listNavigation: true })`؛ كل بنود المستوى الأول `role="menuitem"`؛ غلاف الاستئناف `role="group"` | قياس حي بمتصفح حقيقي أثبت أن `dialog` بلا إدارة تركيز: **16 ضغطة Tab** للوصول لأول بند + **فقدان التركيز إلى `body`** عند `Escape` من الداخل + `menuitem` يتيمة بلا أب `menu`. بعد الإصلاح: أول بند بـ **0 Tabs**، وأسهم عاملة مع التفاف، والتركيز يعود للمشغّل دائمًا (أُعيدت الجولة نفسها بعد الإصلاح) |
| 2 | §4: `identityFileNumber` باحتياط مستقل لكل نصف | **مصدر واحد للزوجين**: الزوج المعروض متى حضر أحد نصفيه، وإلا الزوج الخام | مطابقة تعاقد الخلفية الصريح (`EffectiveFileIdentity`: لا يُخلط رقم سجل مع سنة سجل آخر)؛ الفراغ النصي يُعامل غيابًا عند اختيار المصدر |
| 3 | (غير منصوص) تصفير `accessDenied` | `useEffect(() => setAccessDenied(false), [id])` في الصفحتين | تبديل الملف على المسار نفسه يعيد استعمال المكوّن — وإلا يظهر تنبيه الملف السابق كاذبًا (اختبار تنقل مخصص لكل صفحة) |
| 4 | §5: بنية الصفحتين الرفيعة | + سطر سياق الملف (عنوان التسطير + الرقم/السنة) تحت `h1`؛ رابط عودة وحيد عند `accessDenied` | L1/L2: إزالة ازدواج الاسم القابل للوصول + سياق للروابط العميقة (بلا طلبات جديدة) |
| 5 | تجميل | footnote البوابة `text-gray-500` (تباين AA)؛ حذف `divide-y divide-white/25` بين البنود الملوّنة؛ `useCancellableRequest<DocumentResponse>` (بلا `\| null`)؛ توحيد صياغة تعليق | L3/L5/L6/L7 — بلا تغيير سلوكي |
| 6 | §5: مصير `id="file-correspondence"` | **أُبقي** (لا مرجع له داخل المستودع — تحقق grep كامل) | إشارات خارجية/إشارات مرجعية قديمة؛ إزالته لاحقًا لا تكسر شيئًا |

* فحص `appealSubOpen` عند `Escape`/الخارج: كان يتسرب (`true` باقية) فأُضيف تصفيره عند أي إغلاق + اختبار مخصص.
* التحقق بعد الإصلاحات: `oxlint` صفر، `tsc -b` ناجح، `vitest` ‏121 ملفًا/1194 اختبارًا خضراء، `build` ناجح، وجولة متصفح بعدية بالقيم أعلاه.

1. تدقيق عقود حقلًا بحقل: `displayFileNumber/displayFileYear/fileNumber/fileYear/fileType/court/branchName` + مسارا المراسلات.
2. بحث بقايا: `file-correspondence` و`DocumentCorrespondenceCard` في صفحتي الملف (يجب ألا يبقيا إلا في الصفحتين الجديدتين واختبارات البطاقة).
3. مطابقة بنود §2-§6 مع المنفذ؛ أي انحراف يُذكر صراحة.
4. اصطلاحات: `useFloatingMenu`/`useIsMobile`، `min-h-11`، `focus-visible`، `tabular-nums`، بلا `transition: all`، بلا أسرار.
5. أوامر (كلها خضراء): `npx oxlint src` ثم `npx tsc -b` ثم `npx vitest run` ثم `npm run build`. لا `dotnet test`/هجرات: لا تغيير خلفية — **تنازل صريح موثّق السبب** (يُذكر في تقرير الإنجاز؛ وإن طُلب الالتزام الحرفي بـ `AGENTS.md` يُشغَّل `dotnet test` مرة واحدة).
6. فحص بصري `375px` **و`1024px`** (حدّ `المزيد`) + مراجعة فراغ بطاقة النوع مقابل `—` (قرار §1-5).
7. تقرير الإنجاز يذكر: المسارين الجديدين، الكسر المقصود (`#file-correspondence`)، مصير `id/scroll-mt-24`، ومتابعة `state.from`.
