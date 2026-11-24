using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>حفظ أرقام أساس السنة الحالية ذرّيًا (صفوف تدوير) — نقطة الكتابة الوحيدة،
/// والكاتب الفعلي `IDocumentNumberRegistry` (يملك معاملته وفحوص التكرار).</summary>
public sealed record SaveDocumentBaseNumbersCommand(
    int UserId,
    List<BaseNumberEntry> Entries,
    string? ActorName,
    string? WarningToken = null) : ICommand<bool>;

public sealed class SaveDocumentBaseNumbersCommandHandler
    : CommandHandlerBase<SaveDocumentBaseNumbersCommand, bool>
{
    private readonly IDocumentNumberRegistry _numberRegistry;

    public SaveDocumentBaseNumbersCommandHandler(
        IDocumentNumberRegistry numberRegistry,
        IEnumerable<IPipelineBehavior<SaveDocumentBaseNumbersCommand, bool>> behaviors)
        : base(behaviors)
        => _numberRegistry = numberRegistry;

    protected override async Task<bool> HandleCoreAsync(
        SaveDocumentBaseNumbersCommand command, CancellationToken ct)
    {
        await _numberRegistry.SaveRotationsAsync(
            command.UserId, command.ActorName, command.Entries, command.WarningToken, ct);
        return true;
    }
}

/// <summary>تصحيح رقم أساس سنة محددة موضعيًا (لا حذف).</summary>
public sealed record CorrectDocumentBaseNumberCommand(
    int DocumentId,
    int Year,
    CorrectBaseNumberRequest Request,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class CorrectDocumentBaseNumberCommandHandler
    : CommandHandlerBase<CorrectDocumentBaseNumberCommand, bool>
{
    private readonly IDocumentNumberRegistry _numberRegistry;

    public CorrectDocumentBaseNumberCommandHandler(
        IDocumentNumberRegistry numberRegistry,
        IEnumerable<IPipelineBehavior<CorrectDocumentBaseNumberCommand, bool>> behaviors)
        : base(behaviors)
        => _numberRegistry = numberRegistry;

    protected override async Task<bool> HandleCoreAsync(
        CorrectDocumentBaseNumberCommand command, CancellationToken ct)
    {
        if (command.Request is null || string.IsNullOrWhiteSpace(command.Request.BaseNumber))
            throw new ArgumentException("رقم الأساس مطلوب");
        await _numberRegistry.CorrectAsync(
            command.DocumentId, command.Year, command.Request.RowId, command.Request.BaseNumber!,
            command.UserId, command.ActorName, command.Request.WarningToken, ct);
        return true;
    }
}

/// <summary>التراجع عن تدوير السنة الحالية (`VoidedAt` + بقاء الصف).</summary>
public sealed record UndoDocumentRotationCommand(
    int DocumentId,
    int Year,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class UndoDocumentRotationCommandHandler
    : CommandHandlerBase<UndoDocumentRotationCommand, bool>
{
    private readonly IDocumentNumberRegistry _numberRegistry;

    public UndoDocumentRotationCommandHandler(
        IDocumentNumberRegistry numberRegistry,
        IEnumerable<IPipelineBehavior<UndoDocumentRotationCommand, bool>> behaviors)
        : base(behaviors)
        => _numberRegistry = numberRegistry;

    protected override async Task<bool> HandleCoreAsync(
        UndoDocumentRotationCommand command, CancellationToken ct)
    {
        await _numberRegistry.UndoRotationAsync(
            command.DocumentId, command.Year, command.UserId, command.ActorName, ct);
        return true;
    }
}
