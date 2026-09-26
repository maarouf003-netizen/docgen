using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تنفيذ استعلامات المراسلات. البحث (q) يمتد لرقم المراسلة، واسم المنفذ عليه،
/// ونصوص رسائلها العادية، واسمي المنشئ والمستلم؛ الترقيم server-side عبر PagedResult.
/// </summary>
public class CorrespondenceRepository : Repository<Correspondence>, ICorrespondenceRepository
{
    public CorrespondenceRepository(DocGeneratorDbContext db) : base(db) { }

    private IQueryable<Correspondence> DetailedLetters => Db.Correspondences
        .AsNoTracking()
        .Include(c => c.CreatedBy)
        .Include(c => c.TargetUser)
        .Include(c => c.Document)
        .ThenInclude(d => d!.BaseNumbers)
        .Include(c => c.Branch)
        .Include(c => c.Messages.OrderBy(m => m.Id))
        .Include(c => c.Receipts.OrderBy(r => r.SeenAt));

    public Task<(List<Correspondence> Items, int TotalCount)> SearchForPartyAsync(
        int userId, string? q, string? importance, int page, int perPage,
        CancellationToken ct = default)
        => SearchAsync(
            Db.Correspondences.Where(c => c.CreatedById == userId || c.TargetUserId == userId),
            q, null, importance, page, perPage, ct);

    public Task<(List<Correspondence> Items, int TotalCount)> SearchForBranchAsync(
        int branchId, string governorate, string? q, string? importance,
        int page, int perPage, CancellationToken ct = default)
        => SearchAsync(
            Db.Correspondences.Where(c =>
                c.BranchId == branchId
                || (c.BranchId == null && c.Governorate == governorate)),
            q, null, importance, page, perPage, ct);

    public Task<(List<Correspondence> Items, int TotalCount)> SearchAllAsync(
        string? governorate, string? q, string? importance, int page, int perPage,
        CancellationToken ct = default)
    {
        // فراغ المحافظة يعني عدم اختيارها — يُنفَّذ بلا عناصر (الحجب قبل الاختيار)،
        // فيبقى العقد ذاتي الحماية حتى لو استُدعي المستودع مباشرة دون حارس الخدمة.
        if (string.IsNullOrWhiteSpace(governorate))
            return Task.FromResult((new List<Correspondence>(), 0));
        return SearchAsync(Db.Correspondences, q, governorate, importance, page, perPage, ct);
    }

    public Task<List<string>> GetGovernoratesAsync(CancellationToken ct = default)
        => Db.Correspondences
            .AsNoTracking()
            .Where(c => c.Governorate != null && c.Governorate != string.Empty)
            .Select(c => c.Governorate)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync(ct);

    public async Task<Correspondence?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => await DetailedLetters.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<Correspondence>> ListByDocumentAsync(int documentId, CancellationToken ct = default)
        => await DetailedLetters
            .Where(c => c.DocumentId == documentId)
            .OrderByDescending(c => c.CorrespondenceDate)
            .ToListAsync(ct);

    public Task<bool> NumberExistsAsync(string correspondenceNumber, CancellationToken ct = default)
        => Db.Correspondences.AnyAsync(c => c.CorrespondenceNumber == correspondenceNumber, ct);

    public Task<int> CountUrgentUnseenForTargetAsync(int userId, CancellationToken ct = default)
        => Db.Correspondences.CountAsync(
            c => c.TargetUserId == userId
                && c.Importance == Correspondence.ImportanceUrgent
                && !c.Receipts.Any(r => r.UserId == userId),
            ct);

    public Task<CorrespondenceReceipt?> FindReceiptAsync(int correspondenceId, int userId,
        CancellationToken ct = default)
        => Db.CorrespondenceReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CorrespondenceId == correspondenceId && r.UserId == userId, ct);

    public async Task<Correspondence?> GetTrackedWithDetailsAsync(int id, CancellationToken ct = default)
        // تتبُّع مفعّل عمدًا: الرسائل والتوثيق ستُحدَّث في معاملة الكاتب نفسها.
        => await Db.Correspondences
            .Include(c => c.Messages)
            .Include(c => c.Receipts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    private async Task<(List<Correspondence> Items, int TotalCount)> SearchAsync(
        IQueryable<Correspondence> source, string? q, string? governorate, string? importance,
        int page, int perPage, CancellationToken ct)
    {
        var query = source.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                c.CorrespondenceNumber.Contains(term)
                || (c.Document != null
                    && ((c.Document.BorrowerName ?? string.Empty) + " " +
                        (c.Document.BorrowerFather ?? string.Empty) + " " +
                        (c.Document.BorrowerFamily ?? string.Empty)).Contains(term))
                || c.Messages.Any(m => m.BodyPlainText.Contains(term))
                || (c.CreatedBy != null && c.CreatedBy.FullName.Contains(term))
                || (c.TargetUser != null && c.TargetUser.FullName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(governorate))
        {
            var term = governorate.Trim();
            query = query.Where(c => c.Governorate == term);
        }

        if (!string.IsNullOrWhiteSpace(importance))
        {
            var term = importance.Trim();
            query = query.Where(c => c.Importance == term);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(c => new Correspondence
            {
                Id = c.Id,
                BranchId = c.BranchId,
                Governorate = c.Governorate,
                CreatedById = c.CreatedById,
                TargetUserId = c.TargetUserId,
                DocumentId = c.DocumentId,
                CorrespondenceNumber = c.CorrespondenceNumber,
                CorrespondenceDate = c.CorrespondenceDate,
                Importance = c.Importance,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                Branch = c.Branch == null ? null : new Branch
                {
                    Id = c.Branch.Id,
                    Name = c.Branch.Name,
                },
                CreatedBy = c.CreatedBy == null ? null : new User
                {
                    Id = c.CreatedBy.Id,
                    FullName = c.CreatedBy.FullName,
                    Role = c.CreatedBy.Role,
                },
                TargetUser = c.TargetUser == null ? null : new User
                {
                    Id = c.TargetUser.Id,
                    FullName = c.TargetUser.FullName,
                    Role = c.TargetUser.Role,
                },
                Document = c.Document == null ? null : new Document
                {
                    Id = c.Document.Id,
                    BorrowerName = c.Document.BorrowerName,
                    BorrowerFather = c.Document.BorrowerFather,
                    BorrowerFamily = c.Document.BorrowerFamily,
                    FileNumber = c.Document.FileNumber,
                    FileType = c.Document.FileType,
                    FileYear = c.Document.FileYear,
                    Court = c.Document.Court,
                    // أرقام الأساس لازمة للمحلل المركزي (EffectiveFileIdentity) في سياق
                    // المراسلة — بدونها يتدهور العرض بصمت إلى رقم الملف الأصلي.
                    BaseNumbers = c.Document.BaseNumbers
                        .Select(b => new DocumentBaseNumber
                        {
                            Id = b.Id,
                            DocumentId = b.DocumentId,
                            Year = b.Year,
                            BaseNumber = b.BaseNumber,
                            CreatedAt = b.CreatedAt,
                        }).ToList(),
                },
                Messages = c.Messages
                    .OrderBy(m => m.Id)
                    .Select(m => new CorrespondenceMessage
                    {
                        Id = m.Id,
                        Kind = m.Kind,
                        BodyPlainText = m.BodyPlainText,
                        MessageNumber = m.MessageNumber,
                        MessageDate = m.MessageDate,
                        AuthorId = m.AuthorId,
                        AuthorName = m.AuthorName,
                        AuthorRole = m.AuthorRole,
                    }).ToList(),
                // التوثيق للمستلم وحده (L9): تُسقَط صفوف غير المستلم في SQL نفسه،
                // فمسارات القائمة (`ToListItem`) لا تطابق أصلًا إلا صف المستلم —
                // الترشيح هنا محايد سلوكيًا ويُبقي الحمولة ≤ صف واحد لكل مراسلة.
                // (مسار الكاتب `GetTrackedWithDetailsAsync` يبقى كاملًا عمدًا:
                // `MarkSeenAsync` يفحص التوثيق القائم ويضيف عليه.)
                Receipts = c.Receipts
                    .Where(r => r.UserId == c.TargetUserId)
                    .Select(r => new CorrespondenceReceipt
                    {
                        Id = r.Id,
                        CorrespondenceId = r.CorrespondenceId,
                        UserId = r.UserId,
                        UserName = r.UserName,
                        SeenAt = r.SeenAt,
                    }).ToList(),
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
