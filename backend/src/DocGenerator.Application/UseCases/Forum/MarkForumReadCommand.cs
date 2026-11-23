using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>
/// تعليم القراءة حتى رسالة — عدّادي (`max` فقط بلا تراجع)، ويُعيد العلامة الجديدة.
/// <para>
/// **استثناء موثّق:** لا يستعمل وسم `ITransactionalRequest` بل يملك المعاملة عبر
/// `ITransactionRunner`، لأن الأصل يبتلع سباق الإدراج المزدوج (انتهاك القيد الفريد)
/// **بعد** تراجع المعاملة. لو ابتُلع داخل نواة وسوم المعاملة لصار الالتزام على معاملة
/// مُجهضة. الحفاظ على الحدود الأصلية أولى من توحيد الشكل.
/// </para>
/// </summary>
public sealed record MarkForumReadCommand(
    int ActorUserId,
    string? ActorName,
    int UpToMessageId) : ICommand<int>;

public sealed class MarkForumReadCommandHandler
    : CommandHandlerBase<MarkForumReadCommand, int>
{
    private readonly IForumRepository _forum;
    private readonly IRepository<ForumMessageRead> _reads;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly TimeProvider _clock;
    private readonly IDbExceptionClassifier _errors;

    public MarkForumReadCommandHandler(
        IForumRepository forum,
        IRepository<ForumMessageRead> reads,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        TimeProvider clock,
        IDbExceptionClassifier errors,
        IEnumerable<IPipelineBehavior<MarkForumReadCommand, int>> behaviors)
        : base(behaviors)
    {
        _forum = forum;
        _reads = reads;
        _users = users;
        _uow = uow;
        _tx = tx;
        _clock = clock;
        _errors = errors;
    }

    protected override async Task<int> HandleCoreAsync(
        MarkForumReadCommand command, CancellationToken ct)
    {
        if (command.UpToMessageId < 1)
            throw new ArgumentException("معرف الرسالة غير صالح");
        var member = await ForumMembership.RequireAsync(_users, command.ActorUserId, ct);

        var currentMax = await _forum.MaxReadMessageIdAsync(command.ActorUserId, ct);
        if (command.UpToMessageId <= currentMax)
            return currentMax;

        // رسائل الغير فقط — قراءة الذات لا توثَّق (تُستثنى في المستودع).
        var existing = await _forum.ExistingIdsInRangeAsync(
            command.ActorUserId, currentMax, command.UpToMessageId, ct);
        var already = await _forum.ReadIdsAsync(command.ActorUserId, existing, ct);
        var missing = existing.Except(already).ToList();
        if (missing.Count == 0)
            return command.UpToMessageId;

        var name = string.IsNullOrWhiteSpace(command.ActorName)
            ? ForumIdentityParts.DisplayName(member)
            : command.ActorName.Trim();
        var now = _clock.GetUtcNow().UtcDateTime;
        try
        {
            await _tx.RunAsync(async token =>
            {
                foreach (var messageId in missing)
                    await _reads.AddAsync(new ForumMessageRead
                    {
                        MessageId = messageId,
                        UserId = command.ActorUserId,
                        UserName = name,
                        ReadAtUtc = now,
                    }, token);
                await _uow.SaveChangesAsync(token);
            }, ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق توثيق متزامن (فتح مزدوج/إعادة إرسال): فحص التفرّد خارج المعاملة
            // قد يتجاوزه إدراج متزامن بالصف نفسه — والقيد الفريد هو الحارس الأخير،
            // والصف المطلوب موجود فعلًا فالنتيجة متحقة (نجاح لا 500).
        }

        return command.UpToMessageId;
    }
}
