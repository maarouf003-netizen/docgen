using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>تعليم ردود كتاب مطالعة كمقروءة من محامي الكتاب — يملك معاملته بنفسه.</summary>
public sealed record MarkReviewRepliesSeenCommand(int LetterId, int ActorUserId)
    : ICommand<bool>, ITransactionalRequest;

public sealed class MarkReviewRepliesSeenCommandHandler
    : CommandHandlerBase<MarkReviewRepliesSeenCommand, bool>
{
    private readonly IReviewLetterRepository _letters;
    private readonly IUnitOfWork _uow;

    public MarkReviewRepliesSeenCommandHandler(
        IReviewLetterRepository letters,
        IUnitOfWork uow,
        IEnumerable<IPipelineBehavior<MarkReviewRepliesSeenCommand, bool>> behaviors)
        : base(behaviors)
    {
        _letters = letters;
        _uow = uow;
    }

    protected override async Task<bool> HandleCoreAsync(MarkReviewRepliesSeenCommand command, CancellationToken ct)
    {
        var letter = await _letters.GetTrackedWithMessagesAsync(command.LetterId, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (letter.CreatedById != command.ActorUserId)
            throw new UnauthorizedAccessException("تعليم الإطلاع متاح لمحامي الكتاب نفسه");

        var changed = false;
        foreach (var message in letter.Messages.Where(
                     m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer))
        {
            message.IsSeenByLawyer = true;
            changed = true;
        }

        if (changed)
            await _uow.SaveChangesAsync(ct);

        return true;
    }
}
