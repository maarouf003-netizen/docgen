using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class CorrespondenceService : ICorrespondenceService
{
    public async Task<PagedResult<CorrespondenceListItemDto>> SearchAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, string? governorate,
        string? importance, int page, int perPage, CancellationToken ct = default,
        int? actorSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        var importanceFilter = NormalizeImportanceFilter(importance);

        // المدير/المشرف لا يرى أي مراسلة قبل اختيار المحافظة (حجب تام على مستوى الخدمة).
        if (role is UserRole.Manager or UserRole.Admin
            && string.IsNullOrWhiteSpace(governorate))
        {
            return new PagedResult<CorrespondenceListItemDto>
            {
                Page = page,
                PerPage = perPage,
            };
        }

        var (items, totalCount) = role switch
        {
            UserRole.Lawyer or UserRole.EntityManager
                => await _letters.SearchForPartyAsync(actorUserId, q, importanceFilter, page, perPage, ct),
            UserRole.Head when actorBranchId is not null
                => await _letters.SearchForBranchAsync(
                    actorBranchId.Value, await ResolveHeadGovernorateAsync(actorBranchId.Value, ct),
                    q, importanceFilter, page, perPage, ct),
            UserRole.Head
                => throw new UnauthorizedAccessException("رئيس القسم دون فرع لا يمكنه عرض المراسلات"),
            // نطاق الشعبة (§10): طرفٌ فيها أو ملك دائرة شعبته — بلا تدهور.
            UserRole.SubHead when actorBranchId is not null && actorSectionId is not null
                => await _letters.SearchForScopeAsync(
                    actorUserId, actorBranchId.Value, actorSectionId.Value,
                    q, importanceFilter, page, perPage, ct),
            UserRole.SubHead
                => throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(governorate, q, importanceFilter, page, perPage, ct),
            _ => throw new UnauthorizedAccessException("الدور غير مخوّل لعرض المراسلات"),
        };

        return new PagedResult<CorrespondenceListItemDto>
        {
            Items = items.Select(l => ToListItem(l, actorUserId)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
        };
    }

    public async Task<CorrespondenceDto> GetByIdAsync(int id, int actorUserId, UserRole role,
        int? actorBranchId, CancellationToken ct = default, int? actorSectionId = null)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(id, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        if (!await CanViewAsync(letter, actorUserId, role, actorBranchId, actorSectionId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المراسلة");

        return ToDto(letter, actorUserId);
    }

    public Task<int> CountUnseenAsync(int actorUserId, CancellationToken ct = default)
        => _letters.CountUnseenForTargetAsync(actorUserId, ct);

    public Task<List<string>> GetGovernoratesAsync(CancellationToken ct = default)
        => _letters.GetGovernoratesAsync(ct);


    public async Task<List<CorrespondenceListItemDto>> ListByDocumentAsync(int documentId,
        int actorUserId, UserRole role, int? actorBranchId, CancellationToken ct = default,
        int? actorSectionId = null)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException("الملف غير موجود");

        if (!await MayListDocumentAsync(document, actorUserId, role, actorBranchId, actorSectionId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على مراسلات هذا الملف");

        var letters = await _letters.ListByDocumentAsync(documentId, ct);

        // مندوب الجهة يرى من مراسلات الملف ما هو طرف فيه فقط — حتى داخل النطاق.
        if (role == UserRole.EntityManager)
            letters = letters
                .Where(l => l.CreatedById == actorUserId || l.TargetUserId == actorUserId)
                .ToList();

        // رئيس القسم يرى من مراسلات الملف ما يقع في فرعه أو محافظته فقط —
        // يغلق تسريبًا عبر ملف قديم بلا فرع يحمل مراسلة فرع آخر.
        if (role == UserRole.Head && actorBranchId is not null)
        {
            var headGovernorate = await ResolveHeadGovernorateAsync(actorBranchId.Value, ct);
            letters = letters
                .Where(l => l.BranchId == actorBranchId
                    || (l.BranchId is null && l.Governorate == headGovernorate))
                .ToList();
        }

        return letters.Select(l => ToListItem(l, actorUserId)).ToList();
    }

    private static CorrespondenceMessageDto ToMessageDto(CorrespondenceMessage m) => new(
        m.Id,
        m.Kind,
        m.BodyHtml,
        m.MessageNumber,
        m.MessageDate,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole);

    private CorrespondenceFileContextDto? FileContextOf(Correspondence letter)
    {
        var doc = letter.Document;
        if (doc is null)
            return null;

        var name = string.Join(' ', new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(name))
            name = doc.DocumentType ?? string.Empty;

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return new CorrespondenceFileContextDto(
            name,
            EffectiveFileIdentity.Number(doc, currentYear),
            doc.FileType,
            EffectiveFileIdentity.Year(doc, currentYear),
            doc.Court);
    }

    // التوثيق شأن المستلم وحده: يُعرض سجلّه وحده ويقرأه كل قارئ مصرَّح له
    // (المدير/رئيس القسم) بحالة واحدة مشتركة بدل «هل شاهدها كل واحد» المتناقضة.
    // وحقّ التوثيق قرار خادم يُشتقّ من هوية القارئ هنا، لا مقارنة في الواجهة.
    private CorrespondenceDto ToDto(Correspondence letter, int readerUserId)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var receipts = letter.Receipts
            .Where(r => r.UserId == letter.TargetUserId)
            .OrderBy(r => r.SeenAt)
            .ToList();
        return new CorrespondenceDto(
            letter.Id,
            letter.CorrespondenceNumber,
            letter.CorrespondenceDate,
            letter.Importance,
            letter.DocumentId,
            FileContextOf(letter),
            letter.BranchId,
            letter.Governorate,
            letter.Branch?.Name,
            letter.CreatedById,
            letter.CreatedBy?.FullName ?? string.Empty,
            letter.CreatedBy?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            letter.TargetUserId,
            letter.TargetUser?.FullName ?? string.Empty,
            letter.TargetUser?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            receipts.Count > 0 ? Correspondence.ViewStatusSeen : Correspondence.ViewStatusPending,
            letter.TargetUserId == readerUserId,
            // حق الرد للمستلم وحده أيضًا — يُحسب هنا لا في الواجهة، ومستقل عن حق التوثيق.
            letter.TargetUserId == readerUserId,
            messages.Select(ToMessageDto).ToList(),
            receipts.Select(r => new CorrespondenceReceiptDto(r.UserId, r.UserName, r.SeenAt)).ToList(),
            letter.CreatedAt,
            letter.RecipientSectionId,
            letter.Version);
    }

    private CorrespondenceListItemDto ToListItem(Correspondence letter, int viewerUserId)
    {
        var messages = letter.Messages.OrderBy(m => m.Id).ToList();
        var snippet = messages.FirstOrDefault(m => m.Kind == CorrespondenceMessage.KindLetter)?.BodyPlainText
            ?? messages.FirstOrDefault()?.BodyPlainText
            ?? string.Empty;
        var lastKind = messages.Count > 0 ? messages[^1].Kind : CorrespondenceMessage.KindLetter;
        // شارة الاطلاع تُقرأ من توثيق المستلم وحده، وحقّ التوثيق قرار خادم:
        // «يمكنني أن أوثّق» (المستلم) منفصل عن «هل وُثّقت» (كل القراء).
        var canMarkSeen = letter.TargetUserId == viewerUserId;
        var seen = letter.Receipts.Any(r => r.UserId == letter.TargetUserId);

        return new CorrespondenceListItemDto(
            letter.Id,
            letter.CorrespondenceNumber,
            letter.CorrespondenceDate,
            letter.Importance,
            letter.DocumentId,
            FileContextOf(letter),
            letter.CreatedBy?.FullName ?? string.Empty,
            letter.TargetUser?.FullName ?? string.Empty,
            snippet.Length > 160 ? snippet[..160] + "…" : snippet,
            lastKind,
            seen ? Correspondence.ViewStatusSeen : Correspondence.ViewStatusPending,
            canMarkSeen,
            // حق الرد للمستلم وحده — قرار خادم مستقل عن حق التوثيق.
            letter.TargetUserId == viewerUserId,
            messages.Count,
            letter.Branch?.Name,
            letter.Governorate,
            letter.UpdatedAt);
    }
}
