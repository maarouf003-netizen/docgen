using System.Globalization;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات محرك تتبع تغيّرات الحقول: الالتقاط والمقارنة والتنسيق والتسميات
/// وتوقيعات المجموعات — أداة المراجعة المؤسسية على مستوى الحقل.
/// </summary>
public class DocumentChangeTrackerTests
{
    private static Document NewDocument() => new()
    {
        BorrowerName = "أحمد",
        BorrowerFamily = "العلي",
        AmountNumeric = 1000m,
        Currency = "ليرة سورية",
        ExecStatus = string.Empty,
    };

    [Fact]
    public void Diff_DetectsChangedFields_WithArabicLabelsAndFormatting()
    {
        var before = DocumentChangeTracker.Capture(NewDocument());
        var after = NewDocument();
        after.BorrowerName = "أحمد محمد";
        after.AmountNumeric = 2500.5m;
        after.StruckOffDate = new DateTime(2026, 8, 1);
        after.GeneralEntitySide = GeneralEntitySideCatalog.Executed;

        var changes = DocumentChangeTracker.Diff(before, after);

        var byKey = changes.ToDictionary(c => c.FieldKey);
        Assert.Equal(4, changes.Count);
        Assert.Equal("اسم المنفذ عليه", byKey[nameof(Document.BorrowerName)].FieldLabel);
        Assert.Equal("أحمد", byKey[nameof(Document.BorrowerName)].OldValue);
        Assert.Equal("أحمد محمد", byKey[nameof(Document.BorrowerName)].NewValue);
        Assert.Equal("2500.5", byKey[nameof(Document.AmountNumeric)].NewValue);
        Assert.Equal("2026-08-01", byKey[nameof(Document.StruckOffDate)].NewValue);
        // خرائط القيم المرمزة تُعرض عربيًا
        Assert.Equal("الجهة العامة منفذ عليها", byKey[nameof(Document.GeneralEntitySide)].NewValue);
    }

    [Fact]
    public void Diff_IgnoresEqualValues_TechnicalFields_AndEmptyVsNull()
    {
        var doc = NewDocument();
        doc.ImmediateActions = "";
        var before = DocumentChangeTracker.Capture(doc);

        doc.UpdatedAt = DateTime.UtcNow.AddDays(3);
        doc.ViewCount = 99;
        doc.BorrowerName = " أحمد "; // فراغ طرفي لا يعد تغييرًا بعد التطبيع
        doc.ImmediateActions = null;  // فارغ ↔ null لا يعد تغييرًا

        var changes = DocumentChangeTracker.Diff(before, doc);

        Assert.DoesNotContain(changes, c => c.FieldKey == nameof(Document.UpdatedAt));
        Assert.DoesNotContain(changes, c => c.FieldKey == nameof(Document.ViewCount));
        Assert.DoesNotContain(changes, c => c.FieldKey == nameof(Document.BorrowerName));
        Assert.Empty(changes);
    }

    [Fact]
    public void Diff_ReportsCollectionSignatureChanges_AsSingleReadableRow()
    {
        var doc = NewDocument();
        var before = DocumentChangeTracker.Capture(doc);

        doc.Guarantors.Add(new Guarantor { GuarantorName = "خالد", GuarantorFamily = "زكي" });

        var changes = DocumentChangeTracker.Diff(before, doc);

        var row = Assert.Single(changes);
        Assert.Equal("__Col_Guarantors", row.FieldKey);
        Assert.Equal("الكفلاء", row.FieldLabel);
        Assert.Null(row.OldValue);
        Assert.Contains("خالد زكي", row.NewValue);
    }

    [Fact]
    public void Capture_FormatsBooleans_AndDatesWithTime()
    {
        var doc = NewDocument();
        doc.WasDepositExecuted = true;
        doc.ExecutedDepositDate = new DateTime(2026, 8, 1, 10, 30, 0);

        var snapshot = DocumentChangeTracker.Capture(doc);

        Assert.Equal("نعم", snapshot[nameof(Document.WasDepositExecuted)]);
        Assert.Equal("2026-08-01 10:30:00", snapshot[nameof(Document.ExecutedDepositDate)]);
        Assert.False(snapshot.ContainsKey(nameof(Document.SearchText)));
        Assert.False(snapshot.ContainsKey(nameof(Document.PrintCount)));
    }

    [Fact]
    public void Capture_UnderArabicCulture_FormatsNumericsInvariant()
    {
        // حتمية R0-4: تحت ثقافة بفاصل عشري مختلف (ar-EG: ٫) يجب أن تبقى
        // التنسيقات حتمية. ملاحظة دقيقة: تنسيق الصحيح (int/long) لاتيني أصلًا
        // على .NET (لا استبدال أرقام) فسطرا RegistryId قفل توصيفي، بينما سطر
        // العشرية حارس انحدار حقيقي (بدون Invariant لكانت "2500٫5").
        var current = CultureInfo.CurrentCulture;
        var currentUi = CultureInfo.CurrentUICulture;
        try
        {
            var arabic = new CultureInfo("ar-EG");
            CultureInfo.CurrentCulture = arabic;
            CultureInfo.CurrentUICulture = arabic;

            var doc = NewDocument();
            doc.AmountNumeric = 2500.5m;
            doc.ExecutionApplicants.Add(new ExecutionApplicant { Name = "سامي", Family = "الحمصي", RegistryId = 123 });

            var before = DocumentChangeTracker.Capture(NewDocument());
            var changes = DocumentChangeTracker.Diff(before, doc);
            var byKey = changes.ToDictionary(c => c.FieldKey);

            Assert.Equal("2500.5", byKey[nameof(Document.AmountNumeric)].NewValue);
            // مقارنة ترتيبية صريحة: مقارنة xUnit الافتراضية حساسة للثقافة
            // الحالية وقد تُكافئ لفظيًا بين الأرقام اللاتينية والمشرقية.
            Assert.Contains("123", byKey["__Col_ExecutionApplicants"].NewValue, StringComparison.Ordinal);
            Assert.DoesNotContain("١٢٣", byKey["__Col_ExecutionApplicants"].NewValue, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
            CultureInfo.CurrentUICulture = currentUi;
        }
    }

    [Fact]
    public void Capture_ManyGuarantors_TruncatesSignatureToColumnLimit()
    {
        // مع 49 كفيلًا بأسماء طويلة يتجاوز الربط حد العمود (2000) فيُرفض الحفظ
        // على Postgres — فيُقصّ بعلامة استمرار بدل الانهيار عند الحفظ.
        var doc = NewDocument();
        for (var n = 1; n <= PartyLimitsCatalog.MaxGuarantors; n++)
        {
            doc.Guarantors.Add(new Guarantor
            {
                GuarantorNumber = n,
                GuarantorName = new string('ك', 60),
                GuarantorFather = new string('ف', 60),
                GuarantorFamily = new string('ع', 60),
            });
        }

        var snapshot = DocumentChangeTracker.Capture(doc);
        var signature = snapshot["__Col_Guarantors"];

        Assert.NotNull(signature);
        Assert.True(signature.Length <= DocumentChangeTracker.MaxSignatureLength);
        Assert.EndsWith("…", signature);
    }

    [Fact]
    public void Capture_ShortSignature_NotTruncated()
    {
        var doc = NewDocument();
        doc.Guarantors.Add(new Guarantor { GuarantorName = "خالد", GuarantorFamily = "زكي" });

        var snapshot = DocumentChangeTracker.Capture(doc);

        Assert.Equal("خالد زكي", snapshot["__Col_Guarantors"]);
    }
}
