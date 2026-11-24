using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>تسطير مراسلة (مربوطة بملف أو عامة) لطرف معيَّن بالاسم، برقم وتاريخ تلقائيين.</summary>
public sealed record CreateCorrespondenceCommand(
    CreateCorrespondenceRequest Request,
    int ActorUserId,
    string? ActorName,
    UserRole Role,
    int? ActorBranchId,
    int? ActorSectionId = null) : ICommand<CorrespondenceDto>;

internal sealed class CreateCorrespondenceCommandHandler
    : CommandHandlerBase<CreateCorrespondenceCommand, CorrespondenceDto>
{
    private const int MaxNumberGenerationAttempts = 20;

    private readonly ICorrespondenceWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IDocumentRepository _documents;
    private readonly ICorrespondenceRepository _letters;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly IDbExceptionClassifier _errors;

    public CreateCorrespondenceCommandHandler(
        ICorrespondenceWriteRules rules,
        IUserRepository users,
        IDocumentRepository documents,
        ICorrespondenceRepository letters,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        IDbExceptionClassifier errors,
        IEnumerable<IPipelineBehavior<CreateCorrespondenceCommand, CorrespondenceDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _documents = documents;
        _letters = letters;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _errors = errors;
    }

    protected override async Task<CorrespondenceDto> HandleCoreAsync(
        CreateCorrespondenceCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actorUserId = command.ActorUserId;
        var role = command.Role;
        var actorBranchId = command.ActorBranchId;
        var actorSectionId = command.ActorSectionId;

        if (!_rules.CanWrite(role))
            throw new UnauthorizedAccessException("الدور غير مخوّل لتسطير المراسلات");

        if (!Correspondence.IsValidImportance(request.Importance))
            throw new ArgumentException("الأهمية غير صالحة — اختر: عادي أو هام أو عاجل");

        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        var bodyText = _rules.RequireBodyText(bodyHtml, "نص المراسلة مطلوب", "نص المراسلة");

        // الطرف المستلم معيَّن بالاسم: حساب نشط من الأدوار الثلاثة، غير المنشئ نفسه.
        var target = await _users.GetByIdAsync(request.TargetUserId, ct)
            ?? throw new ArgumentException("المستلم غير موجود");
        if (!target.IsActive)
            throw new ArgumentException("حساب المستلم معطّل — اختر مستلمًا نشطًا");
        if (target.Id == actorUserId)
            throw new ArgumentException("لا يمكن تسطير مراسلة لنفسك — حدّد مستلمًا آخر");
        if (target.Role is not (UserRole.Lawyer or UserRole.Head or UserRole.SubHead or UserRole.EntityManager))
            throw new ArgumentException("المستلم يجب أن يكون محاميًا أو رئيس قسم/شعبة أو مندوب جهة");

        Document? document = null;
        if (request.DocumentId is not null)
        {
            document = await _documents.GetByIdAsync(request.DocumentId.Value, ct)
                ?? throw new ArgumentException("الملف غير موجود");

            if (!await _rules.MayAttachAsync(document, actorUserId, role, actorBranchId, actorSectionId, ct))
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مراسلة على هذا الملف");

            // فرض الأهلية بالمصدر نفسه: مستلم مراسلة مربوطة بملف يجب أن يكون ضمن
            // المؤهلين (مندوب←محامو الملف؛ محامٍ/رئيس←مناديب نطاق الملف) — حتى لا
            // تصل مراسلة مرتبطة لطرف خارج نطاقها عبر مُرسِلٍ متعمّد لتجاوز البحث.
            if (!await _rules.IsEligibleTargetAsync(document, role, target.Id, ct))
            {
                var eligibilityMessage = role == UserRole.EntityManager
                    ? "المستلم يجب أن يكون محامي الملف نفسه أو أحد متابعيه لهذه المراسلة"
                    : "المستلم يجب أن يكون مندوب جهة ضمن نطاق هذا الملف";
                throw new ArgumentException(eligibilityMessage);
            }
        }

        // عقد المستلم الرئيس (§10.2 + قرار 24): تُجمَّد الشعبة على المراسلة،
        // والرئيس المفعّل وقت الإنشاء في `TargetUserId` — فلا يعيد التعاقب توجيهها.
        // المرتبطة بملف لا تصل لرئيس أبدًا (مثبت: تُرفض أعلاه بالأهلية).
        int? recipientSectionId = null;
        if (target.Role is UserRole.Head or UserRole.SubHead)
            recipientSectionId = await _rules.ResolveHeadRecipientSectionAsync(
                request.RecipientSectionId, target, document, role, actorBranchId, ct);
        else if (request.RecipientSectionId is not null)
            throw new ArgumentException("الشعبة المختارة للمستلم الرئيس فقط");

        // الفرع/المحافظة/بادئة الرقم: للمرتبطة بملف من فرع الملف، وللعامة من فرع
        // المنشئ، وللعامة من مندوب (بلا فرع) من محافظة جهته.
        var (branchId, governorate, prefix) = await _rules.ResolveScopeAsync(
            document, role, actorUserId, actorBranchId, ct);

        // مصدر الوقت الوحيد: الساعة المحقونة (قابلة للتجميد في الاختبارات) —
        // لا `DateTime.UtcNow` مباشرًا في أي مسار كتابة.
        var now = _clock.GetUtcNow().UtcDateTime;
        var letter = new Correspondence
        {
            BranchId = branchId,
            Governorate = governorate,
            CreatedById = actorUserId,
            TargetUserId = target.Id,
            RecipientSectionId = recipientSectionId,
            DocumentId = document?.Id,
            CorrespondenceNumber = string.Empty, // يُضبط أدناه مع رقم الرسالة الأولى = المرشّح النهائي بعد حل أي سباق.
            CorrespondenceDate = now,
            Importance = request.Importance,
            CreatedAt = now,
            UpdatedAt = now,
            Messages =
            [
                new CorrespondenceMessage
                {
                    Kind = CorrespondenceMessage.KindLetter,
                    BodyHtml = bodyHtml,
                    BodyPlainText = bodyText,
                    MessageNumber = string.Empty, // يُضبط أدناه = رقم المراسلة النهائي.
                    MessageDate = now,
                    AuthorId = actorUserId,
                    AuthorName = command.ActorName ?? string.Empty,
                    AuthorRole = role.ToString().ToLowerInvariant(),
                },
            ],
        };
        var firstMessage = letter.Messages.Single(m => m.Kind == CorrespondenceMessage.KindLetter);

        var scope = document is null ? "عامة" : $"ملف {document.Id}";
        // سباق الترقيم: فحص التفرّد خارج المعاملة قد يتجاوزه إدراج متزامن بالمرشّح
        // نفسه؛ القيد الفريد على CorrespondenceNumber هو الحارس الأخير، وعند
        // اصطدامه يُعاد التوليد بمرشّح جديد على الكيان نفسه بدل خطأ 500 —
        // فإعادة الحفظ بعد فشل سباق تُكرّر الإدراج الوحيد المعلّق لا غير، والتدقيق
        // لا يُسجَّل إلا داخل المحاولة الناجحة.
        var candidate = await _rules.GenerateUniqueNumberAsync(prefix, now, ct);
        for (var attempt = 0; attempt < MaxNumberGenerationAttempts; attempt++)
        {
            letter.CorrespondenceNumber = candidate;
            firstMessage.MessageNumber = candidate;
            try
            {
                await _tx.RunAsync(async token =>
                {
                    await _letters.AddAsync(letter, token);
                    await _uow.SaveChangesAsync(token);
                    await _audit.LogAsync(command.ActorName, "create_correspondence",
                        details: $"سطّر مراسلة ({scope}) برقم {letter.CorrespondenceNumber} إلى {target.FullName}",
                        ct: token);
                }, ct);
                break;
            }
            catch (Exception ex) when (_errors.IsUniqueViolation(ex))
            {
                if (attempt + 1 >= MaxNumberGenerationAttempts)
                    throw new DocumentConflictException("تعذر توليد رقم فريد للمراسلة، حاول مجدداً", ex);
                candidate = await _rules.GenerateUniqueNumberAsync(prefix, now, ct);
            }
        }

        var stored = await _letters.GetByIdWithDetailsAsync(letter.Id, ct) ?? letter;
        // المنشئ لا يمكن أن يكون المستلم (متحقَّق منه أعلاه)، فـ«حق التوثيق» false هنا.
        return _rules.ToDto(stored, actorUserId);
    }
}

/// <summary>إضافة لاحق إلى مراسلة — منشئ المراسلة نفسه فقط.</summary>
public sealed record AddCorrespondenceAddendumCommand(
    int CorrespondenceId,
    AddCorrespondenceAddendumRequest Request,
    int ActorUserId,
    string? ActorName) : ICommand<CorrespondenceMessageDto>;

internal sealed class AddCorrespondenceAddendumCommandHandler
    : CommandHandlerBase<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto>
{
    private readonly ICorrespondenceWriteRules _rules;
    private readonly ICorrespondenceRepository _letters;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public AddCorrespondenceAddendumCommandHandler(
        ICorrespondenceWriteRules rules,
        ICorrespondenceRepository letters,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        IEnumerable<IPipelineBehavior<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _letters = letters;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
    }

    protected override async Task<CorrespondenceMessageDto> HandleCoreAsync(
        AddCorrespondenceAddendumCommand command, CancellationToken ct)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(command.Request.BodyHtml);
        var bodyText = _rules.RequireBodyText(bodyHtml, "نص اللاحق مطلوب", "نص اللاحق");

        var letter = await _letters.GetTrackedWithDetailsAsync(command.CorrespondenceId, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        // اللاحق من المنشئ نفسه حصرًا (أحد الأطراف) — كتابة المندوب تصل عبر مسار البوابة.
        if (letter.CreatedById != command.ActorUserId)
            throw new UnauthorizedAccessException("اللاحق يُضاف من منشئ المراسلة نفسه");

        var now = _clock.GetUtcNow().UtcDateTime;
        var addendum = new CorrespondenceMessage
        {
            CorrespondenceId = letter.Id,
            Kind = CorrespondenceMessage.KindAddendum,
            BodyHtml = bodyHtml,
            BodyPlainText = bodyText,
            MessageNumber = _rules.NextMessageNumber(letter),
            MessageDate = now,
            AuthorId = command.ActorUserId,
            AuthorName = command.ActorName ?? string.Empty,
            AuthorRole = (await _users.GetByIdAsync(command.ActorUserId, ct))?.Role.ToString().ToLowerInvariant()
                ?? "lawyer",
        };
        letter.Messages.Add(addendum);
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "add_correspondence_addendum",
                details: $"أضاف لاحقًا إلى المراسلة رقم {letter.CorrespondenceNumber}",
                ct: token);
        }, ct);

        return _rules.ToMessageDto(addendum);
    }
}

/// <summary>رد الطرف المستلم على المراسلة — مع توثيق المشاهدة تلقائيًا.</summary>
public sealed record ReplyCorrespondenceCommand(
    int CorrespondenceId,
    ReplyCorrespondenceRequest Request,
    int ActorUserId,
    string? ActorName) : ICommand<CorrespondenceMessageDto>;

internal sealed class ReplyCorrespondenceCommandHandler
    : CommandHandlerBase<ReplyCorrespondenceCommand, CorrespondenceMessageDto>
{
    private readonly ICorrespondenceWriteRules _rules;
    private readonly ICorrespondenceRepository _letters;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly IDbExceptionClassifier _errors;

    public ReplyCorrespondenceCommandHandler(
        ICorrespondenceWriteRules rules,
        ICorrespondenceRepository letters,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        IDbExceptionClassifier errors,
        IEnumerable<IPipelineBehavior<ReplyCorrespondenceCommand, CorrespondenceMessageDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _letters = letters;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _errors = errors;
    }

    protected override async Task<CorrespondenceMessageDto> HandleCoreAsync(
        ReplyCorrespondenceCommand command, CancellationToken ct)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(command.Request.BodyHtml);
        var bodyText = _rules.RequireBodyText(bodyHtml, "نص الرد مطلوب", "نص الرد");

        var letter = await _letters.GetTrackedWithDetailsAsync(command.CorrespondenceId, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        // الرد من الطرف المستلم المعيَّن حصرًا (أحد الأطراف) — لا رد لرئيس غير طرف.
        if (letter.TargetUserId != command.ActorUserId)
            throw new UnauthorizedAccessException("الرد متاح للطرف المستلم المعيَّن فقط");

        // الرد من المستلم المعيَّن يعدّ تأكيدًا لمشاهدته (قرار المالك 2026-11-05):
        // يُوثَّق تلقائيًا مع الرد نفسه، فلا تبقى المراسلة «بانتظار المشاهدة» بعد
        // ردٍّ وقع بعد قراءتها. التوثيق مرة واحدة، وأول توثيق هو المرجع.
        var now = _clock.GetUtcNow().UtcDateTime;
        var needsReceipt = letter.Receipts.All(r => r.UserId != command.ActorUserId);

        for (var attempt = 0; ; attempt++)
        {
            var reply = new CorrespondenceMessage
            {
                CorrespondenceId = letter.Id,
                Kind = CorrespondenceMessage.KindReply,
                BodyHtml = bodyHtml,
                BodyPlainText = bodyText,
                MessageNumber = _rules.NextMessageNumber(letter),
                MessageDate = now,
                AuthorId = command.ActorUserId,
                AuthorName = command.ActorName ?? string.Empty,
                AuthorRole = (await _users.GetByIdAsync(command.ActorUserId, ct))?.Role.ToString().ToLowerInvariant()
                    ?? "entitymanager",
            };
            letter.Messages.Add(reply);
            letter.UpdatedAt = now;
            if (needsReceipt)
            {
                letter.Receipts.Add(new CorrespondenceReceipt
                {
                    CorrespondenceId = letter.Id,
                    UserId = command.ActorUserId,
                    UserName = command.ActorName ?? string.Empty,
                    SeenAt = now,
                });
            }

            try
            {
                await _tx.RunAsync(async token =>
                {
                    await _uow.SaveChangesAsync(token);
                    await _audit.LogAsync(command.ActorName, "reply_correspondence",
                        details: $"رد على المراسلة رقم {letter.CorrespondenceNumber}",
                        ct: token);
                }, ct);
                return _rules.ToMessageDto(reply);
            }
            catch (Exception ex) when (attempt == 0 && needsReceipt && _errors.IsUniqueViolation(ex))
            {
                // سباق توثيق متزامن (ردّان من تبويبين): معاملة الرد تراجعت كاملةً،
                // والتوثيق الفائز يكفي — يُعاد الرد وحده على كيان متتبَّع نظيف.
                _uow.ClearChanges();
                letter = await _letters.GetTrackedWithDetailsAsync(command.CorrespondenceId, ct)
                    ?? throw new KeyNotFoundException("المراسلة غير موجودة");
                needsReceipt = false;
            }
        }
    }
}

/// <summary>تأكيد المشاهدة الصريح — مقصور على الطرف المستلم، وأول توثيق هو المرجع.</summary>
public sealed record MarkCorrespondenceSeenCommand(
    int CorrespondenceId,
    int ActorUserId,
    string? ActorName,
    UserRole Role,
    int? ActorBranchId,
    int? ActorSectionId = null) : ICommand<CorrespondenceReceiptDto>;

internal sealed class MarkCorrespondenceSeenCommandHandler
    : CommandHandlerBase<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto>
{
    private readonly ICorrespondenceWriteRules _rules;
    private readonly ICorrespondenceRepository _letters;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly IDbExceptionClassifier _errors;

    public MarkCorrespondenceSeenCommandHandler(
        ICorrespondenceWriteRules rules,
        ICorrespondenceRepository letters,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        IDbExceptionClassifier errors,
        IEnumerable<IPipelineBehavior<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _letters = letters;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _errors = errors;
    }

    protected override async Task<CorrespondenceReceiptDto> HandleCoreAsync(
        MarkCorrespondenceSeenCommand command, CancellationToken ct)
    {
        var letter = await _letters.GetTrackedWithDetailsAsync(command.CorrespondenceId, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        if (!await _rules.CanViewAsync(
                letter, command.ActorUserId, command.Role, command.ActorBranchId, command.ActorSectionId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المراسلة");

        // التوثيق مقصور على المستلم: من يستلم هو من يشاهد.
        if (letter.TargetUserId != command.ActorUserId)
            throw new UnauthorizedAccessException("تأكيد المشاهدة مقصور على الطرف المستلم");

        var existing = letter.Receipts.FirstOrDefault(r => r.UserId == command.ActorUserId);
        if (existing is not null)
            return new CorrespondenceReceiptDto(existing.UserId, existing.UserName, existing.SeenAt);

        var receipt = new CorrespondenceReceipt
        {
            CorrespondenceId = letter.Id,
            UserId = command.ActorUserId,
            UserName = command.ActorName ?? string.Empty,
            SeenAt = _clock.GetUtcNow().UtcDateTime,
        };
        letter.Receipts.Add(receipt);

        try
        {
            await _tx.RunAsync(async token =>
            {
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(command.ActorName, "mark_correspondence_seen",
                    details: $"أكّد مشاهدة المراسلة رقم {letter.CorrespondenceNumber}",
                    ct: token);
            }, ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق تأكيد متزامن (نقر مزدوج/تبويبان): القيد الفريد منع التكرار —
            // يُرجَع التوثيق الفائز بدل خطأ 500، فأول تأكيد يبقى المرجع.
            var winner = await _letters.FindReceiptAsync(letter.Id, command.ActorUserId, ct);
            if (winner is null)
                throw;
            return new CorrespondenceReceiptDto(winner.UserId, winner.UserName, winner.SeenAt);
        }

        return new CorrespondenceReceiptDto(receipt.UserId, receipt.UserName, receipt.SeenAt);
    }
}
