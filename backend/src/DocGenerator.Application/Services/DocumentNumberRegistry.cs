using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// التنفيذ الوحيد لكتابة <see cref="DocumentBaseNumber"/> وحقول رقم الملف.
/// كل مسار: معاملة + قفل (دائرة، سنة) مرتّب + إعادة قراءة + فحص صلب/لين + تدقيق.
/// </summary>
public sealed partial class DocumentNumberRegistry : IDocumentNumberRegistry
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentBaseNumberRepository _baseNumbers;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly ITransactionContext _txContext;
    private readonly IAuditLogger _audit;
    private readonly INumberWarningTokenService _tokens;
    private readonly NumberRegistryOptions _options;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DocumentNumberRegistry(
        IDocumentRepository documents,
        IDocumentBaseNumberRepository baseNumbers,
        IRepository<ExecutionCircuit> circuits,
        IUnitOfWork uow,
        ITransactionRunner tx,
        ITransactionContext txContext,
        IAuditLogger audit,
        INumberWarningTokenService tokens,
        NumberRegistryOptions options,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _documents = documents;
        _baseNumbers = baseNumbers;
        _circuits = circuits;
        _uow = uow;
        _tx = tx;
        _txContext = txContext;
        _audit = audit;
        _tokens = tokens;
        _options = options;
        _clock = clock;
        _timeZone = timeZone;
    }

    public bool AllowsCircuitlessLegacy => _options.AllowCircuitlessLegacy;

    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);

    private Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
        => _txContext.HasActiveTransaction ? action() : _tx.RunAsync(_ => action(), ct);

    private Task InTransactionAsync(Func<Task> action, CancellationToken ct)
        => _txContext.HasActiveTransaction ? action() : _tx.RunAsync(async _ => await action(), ct);

    private async Task LockAsync(int circuitId, int year, CancellationToken ct)
    {
        if (_txContext.HasActiveTransaction)
            await _txContext.AcquireScopeLockAsync(
                string.Join(':', circuitId.ToString(CultureInfo.InvariantCulture),
                    year.ToString(CultureInfo.InvariantCulture)), ct);
    }

    /// <summary>
    /// قفل الملف أولًا: يُسلسل كل كتابات السجل لنفس الملف (قيد/تدوير/تصحيح/حذف/تجديد)
    /// حتى لو اختلفت السنة/الدائرة، ثم تُؤخذ أقفال النطاق مرتّبة لمنع الجمود.
    /// </summary>
    private async Task LockDocumentAsync(int documentId, CancellationToken ct)
    {
        if (_txContext.HasActiveTransaction)
            await _txContext.AcquireScopeLockAsync(
                $"doc:{documentId.ToString(CultureInfo.InvariantCulture)}", ct);
    }

    private async Task LockScopesAsync(IEnumerable<(int Circuit, int Year)> scopes, CancellationToken ct)
    {
        foreach (var scope in scopes.Distinct().OrderBy(s => s.Circuit).ThenBy(s => s.Year))
            await LockAsync(scope.Circuit, scope.Year, ct);
    }

    private sealed record PreparedRegistration(
        Document Doc, int CircuitId, int Year, string Number, string NumberKey,
        string TypeKey, string FileType, string CircuitName);

    private PreparedRegistration PrepareRegistration(Document doc, string number, string circuitName)
    {
        if (doc.ExecutionCircuitId is null)
            throw new ArgumentException("لا يمكن تسجيل رقم أساس لملف بلا دائرة تنفيذ");
        var currentYear = CurrentYear();
        var year = DocumentBaseNumberPolicy.ValidateYear(doc.FileYear, currentYear, "سنة القيد");
        if (year is null)
        {
            if (!_options.AllowCircuitlessLegacy)
                throw new ArgumentException("سنة قيد الملف مطلوبة عند تسجيل رقم أساس");
            year = currentYear;
        }
        var numberKey = DocumentBaseNumberPolicy.ValidateNumber(number)
            ?? throw new ArgumentException("رقم الأساس مطلوب");
        var typeKey = DocumentBaseNumberPolicy.NormalizeType(doc.FileType);
        return new PreparedRegistration(
            doc, doc.ExecutionCircuitId.Value, year.Value, numberKey, numberKey,
            typeKey, (doc.FileType ?? string.Empty).Trim(), circuitName);
    }

    private static void Void(DocumentBaseNumber row, int? actorUserId, string reason)
    {
        row.VoidedAt = DateTime.UtcNow;
        row.VoidedById = actorUserId;
        row.VoidReason = reason;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedById = actorUserId;
    }
}
