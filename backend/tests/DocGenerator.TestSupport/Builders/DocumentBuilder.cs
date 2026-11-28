using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.TestSupport.Builders;

/// <summary>
/// `P1-03a` — builder حتمي لكيان <see cref="Document"/> في الاختبارات.
/// الافتراضيات لا تلوّث نوافذ الفترات الحالية (تاريخ إدخال قديم ثابت)، وكل تعديل
/// صريح عبر <c>With…</c> ليبقى الفرق مقروءًا في الاختبار.
/// </summary>
public sealed class DocumentBuilder
{
    /// <summary>تاريخ إدخال ثابت قديم (خارج فترات 2026) كي لا تدخل البذور في إحصاءات الشهر الحالي.</summary>
    public static readonly DateTime LegacyCreatedAt = new(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly Document _document = new()
    {
        BranchId = 1,
        CreatedById = 1,
        IsDraft = false,
        ExecStatus = string.Empty,
        BorrowerName = "مقترض",
        BorrowerFamily = "عائلة",
        AmountNumeric = 0,
        CreatedAt = LegacyCreatedAt,
    };

    public static DocumentBuilder Create() => new();

    public DocumentBuilder WithBranch(int? branchId)
    {
        _document.BranchId = branchId;
        return this;
    }

    public DocumentBuilder WithCreatedBy(int userId)
    {
        _document.CreatedById = userId;
        return this;
    }

    public DocumentBuilder Draft(bool isDraft = true)
    {
        _document.IsDraft = isDraft;
        return this;
    }

    public DocumentBuilder WithExecStatus(string execStatus)
    {
        _document.ExecStatus = execStatus;
        return this;
    }

    public DocumentBuilder WithExecSubStatus(string? execSubStatus)
    {
        _document.ExecSubStatus = execSubStatus;
        return this;
    }

    public DocumentBuilder WithExecutedStatus(string? executedStatus)
    {
        _document.ExecutedStatus = executedStatus;
        return this;
    }

    public DocumentBuilder WithSide(string generalEntitySide)
    {
        _document.GeneralEntitySide = generalEntitySide;
        return this;
    }

    public DocumentBuilder Borrower(string name, string? family = null, string? father = null)
    {
        _document.BorrowerName = name;
        _document.BorrowerFamily = family;
        _document.BorrowerFather = father;
        return this;
    }

    public DocumentBuilder Amount(decimal amount, string? currency = null)
    {
        _document.AmountNumeric = amount;
        if (currency is not null) _document.Currency = currency;
        return this;
    }

    public DocumentBuilder Collected(decimal? amount, string? currency = null)
    {
        _document.CollectedAmount = amount;
        if (currency is not null) _document.CollectedCurrency = currency;
        return this;
    }

    public DocumentBuilder CreatedAt(DateTime value)
    {
        _document.CreatedAt = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        return this;
    }

    public DocumentBuilder RegistrationDate(DateTime? date)
    {
        _document.RegistrationDate = date is null
            ? null
            : new DocumentRegistrationDate { DateParsed = date.Value.Date };
        return this;
    }

    public DocumentBuilder FileReceiptDate(DateTime? date)
    {
        _document.FileReceiptDate = date;
        return this;
    }

    public DocumentBuilder Contract(string? selector)
    {
        _document.ContractTypeSelector = selector;
        return this;
    }

    public Document Build() => _document;
}
