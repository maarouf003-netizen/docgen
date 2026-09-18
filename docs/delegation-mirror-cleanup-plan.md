# خطة التنظيف اللاحقة لحزمة مرآة الإنابة (R1 + R3 + D1) — نهائية بانتظار الاعتماد

> **الحالة: مسودة تنفيذ مفصلة — لا يُنفَّذ أي بند قبل موافقة صريحة.**
> المرجع الملزم: `docs/delegation-mirror-plan.md` (§10 والقرارات 10-17).
> الأساس: `33d4913` (حزمة §10) ← `978c20a` (إصلاح الخللين + B6) ← `2b14bce` (تصحيح تعليق).

## 1) الهدف والنطاق

| البند | النوع | الملفات | ملخص |
|---|---|---|---|
| R1 | كود + اختبارات | `DocumentDelegationService.cs` + `DocumentDelegationServiceTests.cs` | حذف نسخ `FileReceiptNumber/Date` من `CopyBooks` لمطابقة عقد المرآة B6 |
| R3 | تعليق فقط | `DocumentService.Apply.cs` (`NormalizeHeirs`) | تصحيح الموضوع الكاذب («ملاك الأصل») وادعاء إزالة التكرار |
| D1 | توثيق فقط | `docs/delegation-mirror-plan.md` (إلحاق §12) | توثيق التكافؤ الوظيفي للترقيم الموقعي في الوضع العادي — **دون تحريف Q4** |
| R2 | **مستبعد صراحة** | — | الحارس لا يقفل `DocumentType` بينما `ApplyRequest` يكتبه (`Apply.cs:207`) — يُترك كملاحظة موثقة (القفل ليس مجانيًا: خطر رفض كاذب + اختبار جديد، مقابل أثر معدوم) |

**لا هجرات EF في أي بند** — لا `dotnet ef database update` عند النشر (يُذكر في تقرير الإنجاز إلزامًا).

## 2) الحقائق المؤكدة بالأدلة (أساس البنود)

- R1 بلا أثر على سلوك صحيح، ويغلق قناة تمرير بيانات متسخة جديدة:
  - الإحصاءات تستخدم `FileReceiptDate` للفترة فقط عند `side ∈ {Executed, Deposit}` (`StatisticsRepository.cs:260-263` و`:318-321`)؛ والإنابة لا تُسطر إلا على طالبة تنفيذ (`ValidateSourceForDelegation` في `DocumentDelegationService.cs:622`) والمناب يرث الصفة (`:296`) — ففترته من `RegistrationDate`.
  - `ApplyRequest` يصفّر الحقلين على طالبة تنفيذ (`DocumentService.Apply.cs:354-359`) — قيمة المصدر `null` إنتاجيًا.
  - الحقلان العاديان غير معروضين إطلاقًا في `frontend/` (المعروض فقط `renewalFileReceipt*`).
- R3 تعليق منسوخ حرفيًا من `AssetMapper.NormalizeOwners` (`AssetMapper.cs:6`) — الموضوع («ملاك الأصل») والادعاء («تُلغى التكرارات») كاذبان معًا؛ والدالة على مسار الحارس نفسه (`DelegationMirror.cs:116`).
- D1 تكافؤ وظيفي لا انحراف: الورثة يُعاد بناؤهم من صف الكفيل نفسه في الطلب (`Apply.cs:421-424` عبر `g.GuarantorNumber` المُرسَل) فالترقيم الموقعي (`DocumentForm.tsx:734`) يجرّ الورثة مع آبائهم — لا يتامى ولا تفاعل مع قاعدة الفجوة (قرار 10).

## 3) البند R1 — تعديل `CopyBooks` (سطرا حذف)

الموقع: `backend/src/DocGenerator.Application/Services/DocumentDelegationService.cs:790-800`.

```csharp
// قبل:
    private static void CopyBooks(Document source, Document target)
    {
        target.FileArrivalNumber = source.FileArrivalNumber;
        target.FileArrivalDate = source.FileArrivalDate;
        target.FileIncoming = source.FileIncoming;
        target.FileIncomingDate = source.FileIncomingDate;
        target.UnderFilingNumber = source.UnderFilingNumber;
        target.FileReceiptNumber = source.FileReceiptNumber;   // ← حذف
        target.FileReceiptDate = source.FileReceiptDate;       // ← حذف
        target.SeizureDate = source.SeizureDate;
    }
// بعد: نفس الدالة دون السطرين المحذوفين، دون أي إضافة أخرى.
```

- **مايكرو-إضافة اختيارية (دقة توثيقية بلا توسع):** ملخص الدالة (`:789`) يعدد «ورود الملف وكتاب الجهة العامة ورقم تحت رفع» ويُغفل `SeizureDate` المنسوخ فعلًا — يُلحق «وتاريخ إلقاء الحجز» أثناء لمس الدالة نفسها.

## 4) البند R1 — الاختبارات (تعديل + اختبار مخصّص)

### 4-أ) تنظيف `Assign_CopiesAllSourcePartiesAndBooksToTarget` (الأسطر 559-624)

- حذف الزرع 567-568 (`source.FileReceiptNumber = "قيد-9";` و`source.FileReceiptDate = new DateTime(2026, 8, 7);`) — حالة خارج العقد.
- حذف التأكيدين 622-623 (`Assert.Equal("قيد-9", ...)` و`Assert.Equal(new DateTime(2026, 8, 7), ...)`).
- تحديث تعليق السطر 616 من «كتب الملف المنيب تنتقل كلها» إلى «كتب الملف المنيب الخمسة تنتقل (ورود الملف/كتاب الجهة/تحت رفع)» — تسمية أمينة مطابقة لاصطلاح المشروع («الكتب الخمسة + تاريخ إلقاء الحجز» في `DelegationMirror.cs:49`)؛ إلقاء الحجز مغطّى باختبار التكافؤ (`:2169`).
- **محسوم — لا إضافة `SeizureDate`:** تغطيته قائمة في اختبار التكافؤ (`DocumentDelegationServiceTests.cs:2169`, حالة «تاريخ القاء الحجز»)، فلا داعي لتكرارها هنا. أُزيل البند الاختياري.

### 4-ب) اختبار مخصّص جديد (حماية انحدار B6)

الاسم: `Assign_DoesNotCopyFileReceiptFieldsToTarget` (لا كلمة `Executed` — المصدر طالبة تنفيذ بقيم متسخة).

```csharp
[Fact]
public async Task Assign_DoesNotCopyFileReceiptFieldsToTarget()
{
    // B6: حقلا «ورود الإخطار التنفيذي» خارج عقد المرآة. الزرع أدناه حالة متسخة
    // مستحيلة إنتاجيًا (ApplyRequest يصفّرهما على طالبة تنفيذ) والغرض تثبيت
    // العقد على مستوى الخدمة — CopyBooks هو الكاتب الوحيد المحتمل على الهدف
    // (يُبنى بـ new Document بقيم null افتراضيًا)، فأي فشل يشير إليه بدقة.
    var source = await CreateSourceAsync();
    source.FileReceiptNumber = "قيد-قديم";
    source.FileReceiptDate = new DateTime(2026, 8, 7);
    await _db.SaveChangesAsync();

    var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
    var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
    var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
        _head1.Id, _branch.Id, "head1");

    var target = await _db.Documents.AsNoTracking()
        .FirstAsync(d => d.Id == dto!.TargetDocumentId!.Value);
    Assert.Null(target.FileReceiptNumber);
    Assert.Null(target.FileReceiptDate);
}
```

- المساعدات المستخدمة (`CreateSourceAsync`، `SampleRequest`، `_service`، `_lawyer1/2`، `_head1`، `_branch`) قائمة ومستخدمة في الاختبار المجاور — بلا بنية جديدة.
- لا حاجة لاختبار مماثل لمسار `MirrorBooksInto` — مستبعد أصلًا (B6) ومغطّى باختبار التكافؤ.

## 5) البند R3 — تصحيح تعليق `NormalizeHeirs` (تعليق فقط، صفر سلوك)

الموقع: `backend/src/DocGenerator.Application/Services/DocumentService.Apply.cs:518-521`.

```csharp
// قبل:
    /// تطبيع قائمة ملاك الأصل: يُتجاهل الاسم الفارغ، ويُقصّ الاسم من الطرفين،
    /// وتُلغى التكرارات مع الحفاظ على ترتيب الاختيار الأصلي.
// بعد:
    /// تطبيع قائمة الورثة: يُتجاهل من فرغ اسمه الثلاثي، وتُقصّ الحقول من الطرفين،
    /// ويُقيَّد نوع العنوان والصفة بالقيم المسموح بها.
```

- الدقة مُطابَقة للجسم (`:528-553`): تجاهل فارغ الثلاثي (`:533-534`)، قصّ الطرفين، تقييد العنوان (`:537-538`) والصفة (`:540-542`) — ولا إزالة تكرار في أي سطر.
- يوسّع الكومت إلى ملف ثالث (`Apply.cs`) لم يُمسّ سابقًا — مقبول لأنه تعليقي بحت وفي مدار المراجعة نفسه.

## 6) البند D1 — ملاحظة توثيقية إلحاقية (لا كود، لا تحريف)

- تُلحق فقرة جديدة كـ §12 في `docs/delegation-mirror-plan.md`؛ **يُحظر المساس بنص Q4** (`:361-363`).
- النص المقترح:

```markdown
## 12) ملحق تنظيف ما بعد §10 (R1 + R3 + D1)

- **D1 — تكافؤ موثّق (لا تغيير كودي):** `DocumentForm.tsx:734` يرسل `i+1` في الوضع
  العادي (`contractTypeSelector === 'عادي'`) بدل الرقم المخزّن — مكافئ وظيفيًا لأن
  الوثائق العادية تُحفظ متصلة دائمًا، والورثة يُعاد بناؤهم من صف الكفيل نفسه في
  الطلب (`Apply.cs:421-424`) فلا كسر روابط ولا تفاعل مع قاعدة الفجوة (قرار 10).
```

## 7) بروتوكول التحقق الإلزامي (بعد التنفيذ، قبل الكومت)

1. البوابات الخمس بالترتيب (فاصل `;` لا `&&` في PowerShell) — العدّادات المتوقعة: خلفية **1273** (299 API + 974 Application بعد الاختبار الجديد)، واجهة **779**، `oxlint` صفر، `tsc -b` و`build` أخضر:
   `dotnet test` ثم `npx oxlint src` ثم `npx tsc -b` ثم `npx vitest run` ثم `npm run build`.
2. المراجعة الذاتية قبل إعلان الإنجاز (إلزام الأصول):
   - تدقيق العقد حقلًا بحقل عبر المسار (إسناد ← هدف، مرآة، حارس) — `FileReceipt` غائب عن الثلاثة؛
   - بحث البقايا: `FileReceiptNumber/Date` لا تظهر في `DocumentDelegationService.cs` إلا خارج `CopyBooks`، و«ملاك الأصل» لا تظهر في `Apply.cs`؛
   - مطابقة بنود هذه الخطة بندًا بندًا؛
   - الاصطلاحات: رسائل عربية، تسميات الحقول القائمة، بلا أبعاد/أنماط مكررة.

## 8) معايير القبول

- [ ] `CopyBooks` بلا أي إشارة لـ `FileReceipt` (كودًا وتعليقًا).
- [ ] **إثبات الأحمر-ثم-الأخضر (تدريجي، قبل الكومت):** يُكتب الاختبار الجديد أولًا ويُشغَّل وحده قبل تطبيق R1 على `CopyBooks` فيفشل (يُثبت أنه يحمي فعلًا)، ثم يُطبَّق R1 ويُعاد فينجح — بدل الاستدلال النظري من التغطية.
- [ ] تعليق `NormalizeHeirs` مطابق للجسم حرفيًا.
- [ ] §12 مُلحق وQ4 untouched (`git diff` على `delegation-mirror-plan.md` يُظهر إضافة فقط).
- [ ] البوابات الخمس خضراء بالعدّادات أعلاه؛ `DocumentView` المتقلب إن فشل يُعاد وحده ويُسجَّل.
- [ ] تقرير الإنجاز يتضمن تنبيه «لا هجرات — لا حاجة لأي `dotnet ef database update`».

## 9) التراجع والكومت

- التراجع: كومت واحد ذري — `git revert` يكفي؛ لا بيانات متأثرة (R1 يمنع كتابة صفوف متسخة جديدة فقط ولا يمسّ المخزّن).
- رسالة الكومت المقترحة: `fix(delegations): align assign-time book copy with mirror contract (B6)` مع متن يذكر R1/R3/D1 والاختبار الجديد وتنبيه «لا هجرات».
