using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Xunit;

namespace DocGenerator.Application.Tests;

/// <summary>
/// W1/B1 + B3: «سبق تنفيذ الإيداع» يُثبَّت من مسار الإنشاء/التعديل لا من تغيير الحالة
/// وحده، وسياسة المبالغ ترفض السالب/التجاوز/أكثر من منزلتين عشريتين.
/// </summary>
public sealed class DocumentAmountAndDepositFlagsTests
{
    [Fact]
    public void ApplyRequest_DepositMarkedExecuted_SetsWasDepositExecuted()
    {
        var doc = new Document
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
            ExecutedStatus = ExecutedStatusCatalog.None,
        };
        var request = new DocumentUpsertRequest
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
            ExecutedStatus = ExecutedStatusCatalog.Executed,
            ContractTypeSelector = "عادي",
        };

        DocumentUpsertMapper.ApplyRequest(doc, request);

        Assert.True(doc.WasDepositExecuted);
    }

    [Fact]
    public void ApplyRequest_ExecutedSide_DoesNotSetDepositFlag()
    {
        var doc = new Document
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Executed,
            ExecutedStatus = ExecutedStatusCatalog.None,
        };
        var request = new DocumentUpsertRequest
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Executed,
            ExecutedStatus = ExecutedStatusCatalog.Executed,
            ContractTypeSelector = "عادي",
        };

        DocumentUpsertMapper.ApplyRequest(doc, request);

        Assert.False(doc.WasDepositExecuted);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(1.999)]
    public void EnsureValid_RejectsNegativeOrExtraScale(double raw)
    {
        var amount = (decimal)raw;

        Assert.Throws<ArgumentException>(() => DocumentAmountPolicy.EnsureValid(amount, "المبلغ"));
    }

    [Fact]
    public void EnsureValid_RejectsOverflowBeyondColumnPrecision()
    {
        Assert.Throws<ArgumentException>(
            () => DocumentAmountPolicy.EnsureValid(DocumentAmountPolicy.MaxAmount + 0.01m, "المبلغ"));
    }

    [Fact]
    public void EnsureValid_AcceptsNullZeroTwoDecimalsAndMax()
    {
        DocumentAmountPolicy.EnsureValid(null, "المبلغ");
        DocumentAmountPolicy.EnsureValid(0m, "المبلغ");
        DocumentAmountPolicy.EnsureValid(1500.5m, "المبلغ");
        DocumentAmountPolicy.EnsureValid(DocumentAmountPolicy.MaxAmount, "المبلغ");
    }

    [Fact]
    public void ApplyRequest_RejectsNegativeAmount()
    {
        var doc = new Document { GeneralEntitySide = GeneralEntitySideCatalog.Applicant };
        var request = new DocumentUpsertRequest
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            AmountNumeric = -5,
        };

        Assert.Throws<ArgumentException>(() => DocumentUpsertMapper.ApplyRequest(doc, request));
    }
}
