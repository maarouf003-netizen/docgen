using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>إضافة لاحق إلى كتاب مطالعة — محامي الكتاب فقط، وإعادته لبانتظار الرد.</summary>
public sealed record AddReviewLetterAddendumCommand(
    int LetterId,
    AddReviewLetterAddendumRequest Request,
    int ActorUserId,
    string? ActorName) : ICommand<ReviewLetterMessageDto>;

internal sealed class AddReviewLetterAddendumCommandHandler
    : CommandHandlerBase<AddReviewLetterAddendumCommand, ReviewLetterMessageDto>
{
    private readonly IReviewLetterReplyWriteRules _rules;
    private readonly IReviewLetterRepository _letters;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public AddReviewLetterAddendumCommandHandler(
        IReviewLetterReplyWriteRules rules,
        IReviewLetterRepository letters,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<AddReviewLetterAddendumCommand, ReviewLetterMessageDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _letters = letters;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<ReviewLetterMessageDto> HandleCoreAsync(
        AddReviewLetterAddendumCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص اللاحق مطلوب");

        // قراءة متتبَّعة (GetByIdAsync بلا AsNoTracking) لتُحدَّث حالة الكتاب في المعاملة نفسها.
        var letter = await _letters.GetByIdAsync(command.LetterId, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (letter.CreatedById != command.ActorUserId)
            throw new UnauthorizedAccessException("اللاحق يُضاف من محامي الكتاب نفسه");

        var branchCode = await _rules.ResolveBranchCodeAsync(letter.BranchId, ct);
        var now = DateTime.UtcNow;
        var addendumNumber = await _rules.GenerateUniqueNumberAsync(branchCode, now, ct);

        var addendum = new ReviewLetterMessage
        {
            ReviewLetterId = letter.Id,
            Kind = ReviewLetterMessage.KindAddendum,
            BodyHtml = bodyHtml,
            BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
            MessageNumber = addendumNumber,
            MessageDate = now,
            AuthorId = command.ActorUserId,
            AuthorName = command.ActorName ?? string.Empty,
            AuthorRole = nameof(UserRole.Lawyer).ToLowerInvariant(),
        };
        letter.Messages.Add(addendum);

        // أي لاحق يعيد الكتاب إلى «بانتظار رد» حتى لو سبق الرد عليه.
        letter.IsAnswered = false;
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "add_review_letter_addendum",
                details: $"أضاف لاحقاً إلى كتاب المطالعة رقم {letter.LetterNumber}",
                ct: token);
        }, ct);

        return _rules.ToMessageDto(addendum);
    }
}

/// <summary>رد مالك الكتاب على كتاب المطالعة — يولّد رقم الرد ويعلّم الكتاب «تم الرد».</summary>
public sealed record ReplyReviewLetterCommand(
    int LetterId,
    ReplyReviewLetterRequest Request,
    int ActorUserId,
    string? ActorName,
    int ActorBranchId,
    UserRole Role = UserRole.Head,
    int? ActorSectionId = null) : ICommand<ReviewLetterMessageDto>;

internal sealed class ReplyReviewLetterCommandHandler
    : CommandHandlerBase<ReplyReviewLetterCommand, ReviewLetterMessageDto>
{
    private readonly IReviewLetterReplyWriteRules _rules;
    private readonly IReviewLetterRepository _letters;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ReplyReviewLetterCommandHandler(
        IReviewLetterReplyWriteRules rules,
        IReviewLetterRepository letters,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ReplyReviewLetterCommand, ReviewLetterMessageDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _letters = letters;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<ReviewLetterMessageDto> HandleCoreAsync(
        ReplyReviewLetterCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(bodyHtml)))
            throw new ArgumentException("نص الرد مطلوب");

        var letter = await _letters.GetByIdAsync(command.LetterId, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (letter.BranchId != command.ActorBranchId)
            throw new UnauthorizedAccessException("رد المطالعات مقصور على كتب الفرع نفسه");
        _rules.EnsureLetterOwner(command.Role, command.ActorSectionId, await _rules.OwnerSectionOfAsync(letter, ct));

        var branchCode = await _rules.ResolveBranchCodeAsync(command.ActorBranchId, ct);
        var now = DateTime.UtcNow;
        var replyNumber = await _rules.GenerateUniqueNumberAsync(branchCode, now, ct);

        var reply = new ReviewLetterMessage
        {
            ReviewLetterId = letter.Id,
            Kind = ReviewLetterMessage.KindReply,
            BodyHtml = bodyHtml,
            BodyPlainText = HtmlInputSanitizer.ToPlainText(bodyHtml),
            MessageNumber = replyNumber,
            MessageDate = now,
            AuthorId = command.ActorUserId,
            AuthorName = command.ActorName ?? string.Empty,
            AuthorRole = command.Role.ToString().ToLowerInvariant(),
        };
        letter.Messages.Add(reply);

        letter.IsAnswered = true;
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            // تنبيه المحامي صاحب الكتاب بالرد — ضمن المعاملة نفسها، مع دمج الردود
            // المتتالية غير المقروءة في تنبيه واحد بدل تراكمها.
            await _rules.StageReplyAlertAsync(letter, reply, command.ActorUserId, command.ActorName, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "reply_review_letter",
                details: $"رد على كتاب المطالعة رقم {letter.LetterNumber}",
                ct: token);
        }, ct);

        return _rules.ToMessageDto(reply);
    }
}
