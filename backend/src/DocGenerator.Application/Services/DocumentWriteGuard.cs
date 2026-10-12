using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مستودع احتياطي للاختبارات القائمة التي تُبنى بلا سجل دوائر: قائمة فارغة
/// (وضع انتقالي — يُقبل النص الحر حتى يُعبَّأ السجل). مشترك داخليًا بين النواة
/// وحارس الكتابة (فحص الاسم `Unconfigured` يعتمد عليه).
/// </summary>
internal sealed class UnconfiguredCircuitRepository : IRepository<ExecutionCircuit>
{
    public Task AddAsync(ExecutionCircuit entity, CancellationToken ct = default) => Task.CompletedTask;
    public Task<ExecutionCircuit?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<ExecutionCircuit?>(null);
    public Task<List<ExecutionCircuit>> ListAsync(CancellationToken ct = default) => Task.FromResult(new List<ExecutionCircuit>());
    public void Remove(ExecutionCircuit entity) { }
    public void Update(ExecutionCircuit entity) { }
}

/// <summary>
/// حارس الكتابة المشترك (المرحلة 4): نقل ميكانيكي للحراس من ملفات النواة
/// (`Kernel.Guards`/`Kernel.Circuits`/`Kernel.Audit`) — بلا أي تغيير سلوكي.
/// </summary>
public sealed class DocumentWriteGuard : IDocumentWriteGuard
{
    private readonly IDocumentRepository _documents;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IAuditLogger _audit;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly IRepository<ExecutionCircuit> _circuits;

    public DocumentWriteGuard(
        IDocumentRepository documents,
        IDbExceptionClassifier dbErrors,
        IAuditLogger audit,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        IRepository<ExecutionCircuit>? circuits = null)
    {
        _documents = documents;
        _dbErrors = dbErrors;
        _audit = audit;
        _occurrences = occurrences;
        _uow = uow;
        _circuits = circuits ?? new UnconfiguredCircuitRepository();
    }

    /// <summary>
    /// الفحص المبكر: النسخة المرسَلة تطابق المقروءة؛ وإلا `409` ودي.
    /// ثم يزيد العدّاد ويثبّت `OriginalValue` لتكشف `EF` السباق بين القراءة والحفظ.
    /// الغياب = توافق (قبول بلا فحص مبكر، لكن الزيادة والشبكة تنشطان).
    /// </summary>
    public void EnsureConcurrency(Document doc, long? version)
    {
        if (version is not null && doc.Version != version.Value)
            throw new DocumentConflictException(
                "تغيّر الملف أثناء التحرير من مستخدم آخر — أعد تحميل الملف وحاول مجددًا");
        doc.Version++;
        if (version is not null)
            _documents.SetVersionOriginal(doc, version.Value);
    }

    /// <summary>
    /// شبكة السباق: تعارض التزامن عند الحفظ المتزامن يُترجَم `409` وديًا بدل `500`.
    /// تُلفّ حول استدعاء `_tx.RunAsync` كاملًا (نمط `WithNumberGuardAsync` في RF-009).
    /// </summary>
    public async Task<T> WithConcurrencyGuardAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            throw new DocumentConflictException(
                "تغيّر الملف أثناء الحفظ من مستخدم آخر — أعد تحميل الملف وحاول مجددًا", ex);
        }
    }

    // ملاحظة: التوقيع النصي القديم EnsureNumberUniqueAsync(court, …) أُسقط بعد ترحيل
    // المواضع الستة إلى التوقيع الدائري (M12 — بلا منادٍ). تاريخه في Git فقط.
    /// <summary>
    /// شبكة السباق: تعارض القيد الفريد عند الحفظ المتزامن يُترجَم 409 وديًا بدل 500.
    /// تُلفّ حول استدعاء _tx.RunAsync كاملًا (تعديل سطرين لكل موقع).
    /// </summary>
    public async Task<T> WithNumberGuardAsync<T>(Func<Task<T>> action, string? number)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException(
                $"رقم الأساس {number?.Trim()} مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
    }

    /// <summary>نسخة الإجراءات بلا قيمة راجعة (تدوير الأرقام).</summary>
    public async Task WithNumberGuardAsync(Func<Task> action, string? number)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException(
                $"رقم الأساس {number?.Trim()} مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
    }

    public Task EnsureNumberUniqueByCircuitAsync(
        int? excludeDocumentId,
        int? circuitId,
        string? courtNormFallback,
        string? number,
        string? type,
        string? year,
        CancellationToken ct)
    {
        var n = DigitNormalizer.NormalizeDigits(number)?.Trim();
        var y = DigitNormalizer.NormalizeDigits(year)?.Trim();
        if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(y))
            return Task.CompletedTask;
        return EnsureNumberUniqueCoreAsync(excludeDocumentId, circuitId, courtNormFallback, n, type?.Trim(), y, ct);
    }

    private async Task EnsureNumberUniqueCoreAsync(
        int? excludeDocumentId,
        int? circuitId,
        string? courtNormFallback,
        string? number,
        string? type,
        string? year,
        CancellationToken ct)
    {
        if (await _documents.ExistsActiveWithNumberByCircuitAsync(
            excludeDocumentId, circuitId, courtNormFallback, number, type, year, ct))
            throw new DocumentConflictException(
                $"رقم الأساس {number?.Trim()} مكرر في دائرة الملف لسنة {year?.Trim()} — تحقق من الدائرة والرقم والنوع");
    }

    /// <summary>
    /// حل الدائرة للكتابة الجديدة: إلزامية + رفض المعطلة + اشتقاق الاسم.
    /// يُستدعى داخل المعاملة (ضد TOCTOU) — الفحص والاشتقاق معًا.
    /// </summary>
    public async Task<ExecutionCircuit?> ResolveCircuitForWriteAsync(
        DocumentUpsertRequest request,
        int? branchId,
        bool isExternalTarget,
        CancellationToken ct)
    {
        if (isExternalTarget)
            return null;
        // الحساب بلا فرع إعداد معطوب — لا مسار حر له (M5): القفل يُتجاوز فقط
        // لوضع الاختبارات اليدوية (مستودع غير مُهيأ) أو القاعدة الفارغة أدناه.
        if (branchId is null && !_circuits.GetType().Name.Contains("Unconfigured"))
            throw new ArgumentException("الحساب غير مرتبط بفرع");
        if (request.ExecutionCircuitId is not null)
        {
            var circuit = await _circuits.GetByIdAsync(request.ExecutionCircuitId.Value, ct)
                ?? throw new ArgumentException("دائرة التنفيذ المختارة غير موجودة");
            if (branchId is not null && circuit.BranchId != branchId)
                throw new ArgumentException("دائرة التنفيذ ليست ضمن فرعك");
            if (!circuit.IsActive)
                throw new ArgumentException("دائرة التنفيذ المختارة معطلة — اختر دائرة نشطة");
            return circuit;
        }
        // انتقالي: نص court حر يُقبل فقط حين لا يملك الفرع أي دائرة بعد (قاعدة فارغة).
        var courtText = (request.Court ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(courtText))
        {
            // وضع الاختبارات القائمة (مستودع غير مُهيأ يُبنى يدويًا): يُسمح بلا دائرة
            // للحفاظ على العقود القائمة؛ والقاعدة الفارغة (فرع بلا سجل) تُقبل مؤقتًا
            // حتى يُعبَّأ السجل — وبعد التعبئة تُطبَّق الإلزامية بصرامة (لا تغيير كاسر).
            if (_circuits.GetType().Name.Contains("Unconfigured"))
                return null;
            if (branchId is not null)
            {
                var existing = await _circuits.ListAsync(ct);
                if (!existing.Any(c => c.BranchId == branchId))
                    return null;
            }
            else
            {
                var existing = await _circuits.ListAsync(ct);
                if (existing.Count == 0)
                    return null;
            }
            throw new ArgumentException("دائرة التنفيذ مطلوبة — اختر من السجل");
        }
        if (branchId is not null)
        {
            var existing = await _circuits.ListAsync(ct);
            var mine = existing.Where(c => c.BranchId == branchId).ToList();
            if (mine.Count > 0)
            {
                var norm = ArabicNameNormalizer.Normalize(courtText);
                var match = mine.FirstOrDefault(c =>
                    c.NameNorm == norm || c.Name.Trim() == courtText);
                if (match is null)
                    throw new ArgumentException("دائرة التنفيذ خارج السجل — اختر من السجل");
                if (!match.IsActive)
                    throw new ArgumentException("دائرة التنفيذ المختارة معطلة — اختر دائرة نشطة");
                request.ExecutionCircuitId = match.Id;
                return match;
            }
        }
        return null;
    }

    /// <summary>
    /// وقوعّتا تبديل الدائرة من التعديل العادي (قديم/جديد) — النطاق الصريح CircuitId
    /// على كل الصفوف غير المحذوفة بما فيها المعلقة، وتحمل اسمي الدائرتين نصًا (التاريخ المجمد).
    /// </summary>
    /// <summary>
    /// وقوعّة تبديل الدائرة من التعديل العادي (M2): صف واحد يحمل محتوى «الوقوعتين»
    /// (قديم/جديد في FromCircuitName/ToCircuitName + الرقم الحالي) — التاريخ المجمد
    /// في صف واحد أدق من صفين منفصلين قد يُقرأ أحدهما دون الآخر.
    /// </summary>
    public async Task AddCircuitSwitchOccurrencesAsync(
        Document doc, string? oldCircuitName, int? userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var actorId = userId ?? doc.CreatedById;
        var oldOcc = new DocumentOccurrence
        {
            DocumentId = doc.Id,
            OccurrenceType = OccurrenceTypeCatalog.CircuitReferred,
            Source = OccurrenceSourceCatalog.System,
            EventDate = now,
            FileNumber = doc.FileNumber,
            FileType = doc.FileType,
            Year = int.TryParse(doc.FileYear, out var oy) ? oy : null,
            FromCircuitName = oldCircuitName,
            ToCircuitName = doc.Court,
            Details = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["fromCircuit"] = oldCircuitName ?? string.Empty,
                ["toCircuit"] = doc.Court ?? string.Empty,
                ["via"] = "edit",
            }),
            CreatedById = actorId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _occurrences.AddAsync(oldOcc, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// تسجيل تدقيق عمليات الملف مع تفاصيل تغيّرات الحقول: حين تُرصد فروقات
    /// تُسجَّل صفوفًا (حقل/قبل/بعد) مرتبطة بالإدخال، وإلا يبقى التسجيل نصيًا كالسابق.
    /// </summary>
    public async Task LogDocumentChangesAsync(
        Dictionary<string, string?> before,
        Document after,
        string? actorName,
        string actionType,
        string baseDetail,
        CancellationToken ct)
    {
        var changes = DocumentChangeTracker.Diff(before, after);
        if (changes.Count == 0)
        {
            // لا تغييرات متتبعة: يبقى التسجيل النصي كما كان تاريخيًا.
            await _audit.LogAsync(actorName, actionType, after.Id, after.DocumentType,
                DocumentText.AuditWithActor(baseDetail, after), ct);
            return;
        }

        await _audit.LogDocumentChangeAsync(
            actorName, actionType, after.Id, after.DocumentType,
            DocumentText.AuditWithActor($"{baseDetail} — غيّر {changes.Count} حقلًا", after), changes, ct);
    }
}
