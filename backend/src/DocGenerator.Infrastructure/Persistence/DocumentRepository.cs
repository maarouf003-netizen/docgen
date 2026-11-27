using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// بحث ترحّلي يُنفَّذ في قاعدة البيانات (Count + Skip/Take) بدل تحميل كل السجلات.
/// </summary>
public partial class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(DocGeneratorDbContext db) : base(db) { }

    /// <summary>فحص وجود رخيص (يحترم فلتر الحذف المنطقي).</summary>
    public Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => Db.Documents.AnyAsync(d => d.Id == id, ct);

    /// <summary>
    /// إسقاط خفيف (F-01) لفحص وصول المستند: خمسة أعمدة فقط بدل شجرة التحميل الكاملة.
    /// <c>SectionId</c> مشتق من الدائرة كما في <c>DocumentResponse.SectionId</c> بالضبط.
    /// </summary>
    public Task<DocumentAccessInfo?> GetAccessInfoAsync(int id, CancellationToken ct = default)
        => Db.Documents
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DocumentAccessInfo(
                d.Id,
                d.CreatedById,
                d.BranchId,
                d.ExecutionCircuitId,
                d.ExecutionCircuit != null ? d.ExecutionCircuit.SectionId : null))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// قراءة داخل معاملة مع قفل صف (`FOR UPDATE`) لمنع سباق العدّ/الإضافة.
    /// بعد <c>P1-08</c> لا مزوّد غير <c>PostgreSQL</c>؛ أي مزوّد آخر يُرفض صراحةً بدل
    /// تنفيذ قراءة **بلا قفل** تُخفي السباق.
    /// </summary>
    public virtual async Task<Document?> GetByIdForUpdateAsync(int id, CancellationToken ct = default)
    {
        if (Db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(
                $"GetByIdForUpdateAsync يتطلب مزوّد Npgsql (الحالي: {Db.Database.ProviderName ?? "غير محدد"}) — "
                + "لا يوجد مسار بديل بلا قفل صف.");
        }

        // أمر خام مقصود: تركيب `FromSql` مع `FirstOrDefault` قد يلفّ SQL في استعلام فرعي
        // فتتغير دلالات القفل. هنا يُنفَّذ `FOR UPDATE` كما هو داخل معاملة الخدمة.
        var transaction = Db.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "GetByIdForUpdateAsync يتطلب معاملة نشطة — القفل يُحرَّر فور نهاية الأمر خارجها.");
        var connection = Db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT 1 FROM \"Documents\" WHERE \"Id\" = @id FOR UPDATE";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = id;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(ct);
        }

        return await GetByIdAsync(id, ct);
    }

    /// <summary>
    /// قراءة طازجة بلا تتبع (B2): الأصول + الإنابات مع لقطاتها + المنابات (لحالاتها فقط).
    /// الكيانات المعادة منفصلة — أي تعديل عليها لا يُحفظ؛ تُمرَّر للفحص وبناء اللقطات قراءةً فقط.
    /// </summary>
    public async Task<Document?> GetByIdWithDelegationsNoTrackingAsync(int id, CancellationToken ct = default)
    {
        return await Db.Documents
            .AsNoTracking()
            .Include(d => d.Assets)
            .Include(d => d.Delegations)
                .ThenInclude(dl => dl.Assets)
            .Include(d => d.Delegations)
                .ThenInclude(dl => dl.TargetDocument)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

}
