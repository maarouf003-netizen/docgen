using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class CorrespondenceService : ICorrespondenceService
{
    public async Task<CorrespondenceDto> CreateAsync(CreateCorrespondenceRequest request,
        int actorUserId, string? actorName, UserRole role, int? actorBranchId,
        CancellationToken ct = default, int? actorSectionId = null)
    {
        if (!CanWrite(role))
            throw new UnauthorizedAccessException("الدور غير مخوّل لتسطير المراسلات");

        if (!Correspondence.IsValidImportance(request.Importance))
            throw new ArgumentException("الأهمية غير صالحة — اختر: عادي أو هام أو عاجل");

        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        var bodyText = RequireBodyText(bodyHtml, "نص المراسلة مطلوب", "نص المراسلة");

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

            if (!await MayAttachAsync(document, actorUserId, role, actorBranchId, actorSectionId, ct))
                throw new UnauthorizedAccessException("لا تملك صلاحية تسطير مراسلة على هذا الملف");

            // فرض الأهلية بالمصدر نفسه: مستلم مراسلة مربوطة بملف يجب أن يكون ضمن
            // المؤهلين (مندوب←محامو الملف؛ محامٍ/رئيس←مناديب نطاق الملف) — حتى لا
            // تصل مراسلة مرتبطة لطرف خارج نطاقها عبر مُرسِلٍ متعمّد لتجاوز البحث.
            if (!await IsEligibleTargetAsync(document, role, target.Id, ct))
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
            recipientSectionId = await ResolveHeadRecipientSectionAsync(
                request.RecipientSectionId, target, document, role, actorBranchId, ct);
        else if (request.RecipientSectionId is not null)
            throw new ArgumentException("الشعبة المختارة للمستلم الرئيس فقط");

        // الفرع/المحافظة/بادئة الرقم: للمرتبطة بملف من فرع الملف، وللعامة من فرع
        // المنشئ، وللعامة من مندوب (بلا فرع) من محافظة جهته.
        var (branchId, governorate, prefix) = await ResolveScopeAsync(
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
                    AuthorName = actorName ?? string.Empty,
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
        var candidate = await GenerateUniqueNumberAsync(prefix, now, ct);
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
                    await _audit.LogAsync(actorName, "create_correspondence",
                        details: $"سطّر مراسلة ({scope}) برقم {letter.CorrespondenceNumber} إلى {target.FullName}",
                        ct: token);
                }, ct);
                break;
            }
            catch (Exception ex) when (_errors.IsUniqueViolation(ex))
            {
                if (attempt + 1 >= MaxNumberGenerationAttempts)
                    throw new DocumentConflictException("تعذر توليد رقم فريد للمراسلة، حاول مجدداً", ex);
                candidate = await GenerateUniqueNumberAsync(prefix, now, ct);
            }
        }

        var stored = await _letters.GetByIdWithDetailsAsync(letter.Id, ct) ?? letter;
        // المنشئ لا يمكن أن يكون المستلم (متحقَّق منه أعلاه)، فـ«حق التوثيق» false هنا.
        return ToDto(stored, actorUserId);
    }

    public async Task<CorrespondenceMessageDto> AddAddendumAsync(int correspondenceId,
        AddCorrespondenceAddendumRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        var bodyText = RequireBodyText(bodyHtml, "نص اللاحق مطلوب", "نص اللاحق");

        var letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        // اللاحق من المنشئ نفسه حصرًا (أحد الأطراف) — كتابة المندوب تصل عبر مسار البوابة.
        if (letter.CreatedById != actorUserId)
            throw new UnauthorizedAccessException("اللاحق يُضاف من منشئ المراسلة نفسه");

        var now = _clock.GetUtcNow().UtcDateTime;
        var addendum = new CorrespondenceMessage
        {
            CorrespondenceId = letter.Id,
            Kind = CorrespondenceMessage.KindAddendum,
            BodyHtml = bodyHtml,
            BodyPlainText = bodyText,
            MessageNumber = NextMessageNumber(letter),
            MessageDate = now,
            AuthorId = actorUserId,
            AuthorName = actorName ?? string.Empty,
            AuthorRole = (await _users.GetByIdAsync(actorUserId, ct))?.Role.ToString().ToLowerInvariant()
                ?? "lawyer",
        };
        letter.Messages.Add(addendum);
        letter.UpdatedAt = now;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "add_correspondence_addendum",
                details: $"أضاف لاحقًا إلى المراسلة رقم {letter.CorrespondenceNumber}",
                ct: token);
        }, ct);

        return ToMessageDto(addendum);
    }

    public async Task<CorrespondenceMessageDto> ReplyAsync(int correspondenceId,
        ReplyCorrespondenceRequest request, int actorUserId, string? actorName,
        CancellationToken ct = default)
    {
        var bodyHtml = HtmlInputSanitizer.Sanitize(request.BodyHtml);
        var bodyText = RequireBodyText(bodyHtml, "نص الرد مطلوب", "نص الرد");

        var letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        // الرد من الطرف المستلم المعيَّن حصرًا (أحد الأطراف) — لا رد لرئيس غير طرف.
        if (letter.TargetUserId != actorUserId)
            throw new UnauthorizedAccessException("الرد متاح للطرف المستلم المعيَّن فقط");

        // الرد من المستلم المعيَّن يعدّ تأكيدًا لمشاهدته (قرار المالك 2026-11-05):
        // يُوثَّق تلقائيًا مع الرد نفسه، فلا تبقى المراسلة «بانتظار المشاهدة» بعد
        // ردٍّ وقع بعد قراءتها. التوثيق مرة واحدة، وأول توثيق هو المرجع.
        var now = _clock.GetUtcNow().UtcDateTime;
        var needsReceipt = letter.Receipts.All(r => r.UserId != actorUserId);

        for (var attempt = 0; ; attempt++)
        {
            var reply = new CorrespondenceMessage
            {
                CorrespondenceId = letter.Id,
                Kind = CorrespondenceMessage.KindReply,
                BodyHtml = bodyHtml,
                BodyPlainText = bodyText,
                MessageNumber = NextMessageNumber(letter),
                MessageDate = now,
                AuthorId = actorUserId,
                AuthorName = actorName ?? string.Empty,
                AuthorRole = (await _users.GetByIdAsync(actorUserId, ct))?.Role.ToString().ToLowerInvariant()
                    ?? "entitymanager",
            };
            letter.Messages.Add(reply);
            letter.UpdatedAt = now;
            if (needsReceipt)
            {
                letter.Receipts.Add(new CorrespondenceReceipt
                {
                    CorrespondenceId = letter.Id,
                    UserId = actorUserId,
                    UserName = actorName ?? string.Empty,
                    SeenAt = now,
                });
            }

            try
            {
                await _tx.RunAsync(async token =>
                {
                    await _uow.SaveChangesAsync(token);
                    await _audit.LogAsync(actorName, "reply_correspondence",
                        details: $"رد على المراسلة رقم {letter.CorrespondenceNumber}",
                        ct: token);
                }, ct);
                return ToMessageDto(reply);
            }
            catch (Exception ex) when (attempt == 0 && needsReceipt && _errors.IsUniqueViolation(ex))
            {
                // سباق توثيق متزامن (ردّان من تبويبين): معاملة الرد تراجعت كاملةً،
                // والتوثيق الفائز يكفي — يُعاد الرد وحده على كيان متتبَّع نظيف.
                _uow.ClearChanges();
                letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
                    ?? throw new KeyNotFoundException("المراسلة غير موجودة");
                needsReceipt = false;
            }
        }
    }

    public async Task<CorrespondenceReceiptDto> MarkSeenAsync(int correspondenceId,
        int actorUserId, string? actorName, UserRole role, int? actorBranchId,
        CancellationToken ct = default, int? actorSectionId = null)
    {
        var letter = await _letters.GetTrackedWithDetailsAsync(correspondenceId, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, actorSectionId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المراسلة");

        // التوثيق مقصور على المستلم: من يستلم هو من يشاهد.
        if (letter.TargetUserId != actorUserId)
            throw new UnauthorizedAccessException("تأكيد المشاهدة مقصور على الطرف المستلم");

        var existing = letter.Receipts.FirstOrDefault(r => r.UserId == actorUserId);
        if (existing is not null)
            return new CorrespondenceReceiptDto(existing.UserId, existing.UserName, existing.SeenAt);

        var receipt = new CorrespondenceReceipt
        {
            CorrespondenceId = letter.Id,
            UserId = actorUserId,
            UserName = actorName ?? string.Empty,
            SeenAt = _clock.GetUtcNow().UtcDateTime,
        };
        letter.Receipts.Add(receipt);

        try
        {
            await _tx.RunAsync(async token =>
            {
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "mark_correspondence_seen",
                    details: $"أكّد مشاهدة المراسلة رقم {letter.CorrespondenceNumber}",
                    ct: token);
            }, ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق تأكيد متزامن (نقر مزدوج/تبويبان): القيد الفريد منع التكرار —
            // يُرجَع التوثيق الفائز بدل خطأ 500، فأول تأكيد يبقى المرجع.
            var winner = await _letters.FindReceiptAsync(letter.Id, actorUserId, ct);
            if (winner is null)
                throw;
            return new CorrespondenceReceiptDto(winner.UserId, winner.UserName, winner.SeenAt);
        }

        return new CorrespondenceReceiptDto(receipt.UserId, receipt.UserName, receipt.SeenAt);
    }
}
