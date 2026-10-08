using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تنفيذ استعلامات كتب المطالعة. البحث (q) يمتد لرقم الكتاب، واسم المنفذ عليه،
/// ونصوص رسائله العادية؛ الترقيم server-side عبر PagedResult.
/// </summary>
public class ReviewLetterRepository : Repository<ReviewLetter>, IReviewLetterRepository
{
    public ReviewLetterRepository(DocGeneratorDbContext db) : base(db) { }

    private IQueryable<ReviewLetter> DetailedLetters => Db.ReviewLetters
        .AsNoTracking()
        .Include(l => l.CreatedBy)
        .Include(l => l.Document)
        .ThenInclude(d => d!.BaseNumbers)
        // دائرة الملف وشعبتها لاشتقاق مالك الكتاب المرتبط (§10.1).
        .Include(l => l.Document)
        .ThenInclude(d => d!.ExecutionCircuit)
        .ThenInclude(c => c!.Section)
        .Include(l => l.RecipientSection)
        .Include(l => l.Branch)
        .Include(l => l.Messages.OrderBy(m => m.Id));

    public Task<(List<ReviewLetter> Items, int TotalCount)> SearchForLawyerAsync(
        int userId, string? q, int page, int perPage, CancellationToken ct = default)
        => SearchAsync(Db.ReviewLetters.Where(l => l.CreatedById == userId), q, null, page, perPage, ct);

    /// <summary>
    /// بحث نطاق المالك (§10): مالك الكتاب = شعبة دائرة ملفه (بلا دائرة → القسم)،
    /// وبلا ملف = الشعبة المستلمة (`null` → القسم). `ownerSectionId` فارغٌ للقسم.
    /// </summary>
    public Task<(List<ReviewLetter> Items, int TotalCount)> SearchForScopeAsync(
        int branchId, int? ownerSectionId, string? q, int page, int perPage, CancellationToken ct = default)
        => SearchAsync(InScope(Db.ReviewLetters.Where(l => l.BranchId == branchId), ownerSectionId), q, null, page, perPage, ct);

    /// <summary>قيد ملكية الكتاب المشترك للبحث والعدّاد (§10.1 + §10.2).</summary>
    private static IQueryable<ReviewLetter> InScope(IQueryable<ReviewLetter> source, int? ownerSectionId)
        => source.Where(l => l.DocumentId == null
            ? l.RecipientSectionId == ownerSectionId
            : (l.Document!.ExecutionCircuitId == null
                ? ownerSectionId == null
                : l.Document.ExecutionCircuit!.SectionId == ownerSectionId));

    public Task<(List<ReviewLetter> Items, int TotalCount)> SearchAllAsync(
        string? administrativeBranch, string? q, int page, int perPage, CancellationToken ct = default)
    {
        // فراغ الفرع يعني عدم اختياره — يُنفَّذ بلا عناصر (الحجب قبل اختيار الفرع)،
        // فيبقى العقد ذاتي الحماية حتى لو استُدعي المستودع مباشرة دون حارس الخدمة.
        if (string.IsNullOrWhiteSpace(administrativeBranch))
            return Task.FromResult((new List<ReviewLetter>(), 0));
        return SearchAsync(Db.ReviewLetters, q, administrativeBranch, page, perPage, ct);
    }

    public Task<List<string>> GetAdministrativeBranchesAsync(CancellationToken ct = default)
        => Db.ReviewLetters
            .AsNoTracking()
            .Where(l => l.Branch != null && l.Branch.Name != null && l.Branch.Name != string.Empty)
            .Select(l => l.Branch!.Name!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

    /// <summary>عدد كتب النطاق بانتظار الرد (§10 — جرس المالك الأحمر).</summary>
    public Task<int> CountPendingForScopeAsync(int branchId, int? ownerSectionId, CancellationToken ct = default)
        => InScope(Db.ReviewLetters.Where(l => l.BranchId == branchId && !l.IsAnswered), ownerSectionId)
            .CountAsync(ct);

    public async Task<ReviewLetter?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => await DetailedLetters.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<List<ReviewLetter>> ListByDocumentAsync(int documentId, CancellationToken ct = default)
        => await DetailedLetters
            .Where(l => l.DocumentId == documentId)
            .OrderByDescending(l => l.LetterDate)
            .ToListAsync(ct);

    public Task<bool> NumberExistsAsync(string letterNumber, CancellationToken ct = default)
        => Db.ReviewLetters.AnyAsync(l => l.LetterNumber == letterNumber, ct);

    public Task<int> CountUnseenReplyLettersForLawyerAsync(int userId, CancellationToken ct = default)
        => Db.ReviewLetters.CountAsync(
            l => l.CreatedById == userId
                && l.Messages.Any(m => m.Kind == ReviewLetterMessage.KindReply && !m.IsSeenByLawyer),
            ct);

    public async Task<ReviewLetter?> GetTrackedWithMessagesAsync(int id, CancellationToken ct = default)
        // تتبُّع مفعّل عمدًا: الرسائل ستُحدَّث (أعلام الإطلاع) في معاملة الكاتب.
        => await Db.ReviewLetters
            .Include(l => l.Messages)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    private async Task<(List<ReviewLetter> Items, int TotalCount)> SearchAsync(
        IQueryable<ReviewLetter> source, string? q, string? administrativeBranch,
        int page, int perPage, CancellationToken ct)
    {
        var query = source.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l =>
                l.LetterNumber.Contains(term)
                || (l.Document != null
                    && ((l.Document.BorrowerName ?? string.Empty) + " " +
                        (l.Document.BorrowerFather ?? string.Empty) + " " +
                        (l.Document.BorrowerFamily ?? string.Empty)).Contains(term))
                || l.Messages.Any(m => m.BodyPlainText.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(administrativeBranch))
        {
            var term = administrativeBranch.Trim();
            query = query.Where(l => l.Branch != null && l.Branch.Name == term);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.UpdatedAt)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(l => new ReviewLetter
            {
                Id = l.Id,
                BranchId = l.BranchId,
                CreatedById = l.CreatedById,
                DocumentId = l.DocumentId,
                RecipientSectionId = l.RecipientSectionId,
                RecipientSection = l.RecipientSection == null ? null : new Section
                {
                    Id = l.RecipientSection.Id,
                    Name = l.RecipientSection.Name,
                },
                LetterNumber = l.LetterNumber,
                LetterDate = l.LetterDate,
                IsAnswered = l.IsAnswered,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
                Branch = l.Branch == null ? null : new Branch
                {
                    Id = l.Branch.Id,
                    Name = l.Branch.Name,
                },
                CreatedBy = l.CreatedBy == null ? null : new User
                {
                    Id = l.CreatedBy.Id,
                    FullName = l.CreatedBy.FullName,
                    Role = l.CreatedBy.Role,
                },
                Document = l.Document == null ? null : new Document
                {
                    Id = l.Document.Id,
                    BorrowerName = l.Document.BorrowerName,
                    BorrowerFather = l.Document.BorrowerFather,
                    BorrowerFamily = l.Document.BorrowerFamily,
                    FileNumber = l.Document.FileNumber,
                    FileType = l.Document.FileType,
                    FileYear = l.Document.FileYear,
                    Court = l.Document.Court,
                    // أرقام الأساس لازمة للمحلل المركزي (EffectiveFileIdentity) في سياق
                    // كتاب المراجعة — بدونها يتدهور العرض بصمت إلى رقم الملف الأصلي.
                    BaseNumbers = l.Document.BaseNumbers
                        .Select(b => new DocumentBaseNumber
                        {
                            Id = b.Id,
                            DocumentId = b.DocumentId,
                            Year = b.Year,
                            BaseNumber = b.BaseNumber,
                            CreatedAt = b.CreatedAt,
                        }).ToList(),
                },
                Messages = l.Messages
                    .OrderBy(m => m.Id)
                    .Select(m => new ReviewLetterMessage
                    {
                        Id = m.Id,
                        Kind = m.Kind,
                        BodyPlainText = m.BodyPlainText,
                        MessageNumber = m.MessageNumber,
                        MessageDate = m.MessageDate,
                        AuthorId = m.AuthorId,
                        AuthorName = m.AuthorName,
                        AuthorRole = m.AuthorRole,
                        IsSeenByLawyer = m.IsSeenByLawyer,
                    }).ToList(),
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
