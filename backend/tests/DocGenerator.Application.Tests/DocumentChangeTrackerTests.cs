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
}
