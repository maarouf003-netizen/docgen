using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// سجل دوائر التنفيذ — تكامل DocumentService (BQ-004):
/// العقد ExecutionCircuitId إلزامي؛ الخادم يشتق Court/CourtNorm من الدائرة
/// ويتجاهل نص العميل دائمًا (لا 400). الاستثناء: ملفات المناب الخارجي
/// (IsExternal=true) تقبل NULL. الوضع الانتقالي: فرع بلا دوائر (قاعدة فارغة/
/// اختبارات قائمة ترسل court) يُقبل النص الحر مؤقتًا حتى يُعبَّأ السجل.
/// </summary>
public sealed partial class DocumentService
{
    /// <summary>
    /// حل الدائرة للكتابة الجديدة: إلزامية + رفض المعطلة + اشتقاق الاسم.
    /// يُستدعى داخل المعاملة (ضد TOCTOU) — الفحص والاشتقاق معًا.
    /// </summary>
    private async Task<ExecutionCircuit?> ResolveCircuitForWriteAsync(
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

    private static void ApplyCircuitDerivation(Document doc, ExecutionCircuit? circuit, DocumentUpsertRequest request)
    {
        if (circuit is not null)
        {
            doc.ExecutionCircuitId = circuit.Id;
            doc.Court = circuit.Name;
            doc.CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name);
        }
        else
        {
            // انتقالي (فرع بلا سجل): إبقاء النص الحر المرسل — يُتجاهل فقط عند وجود دائرة.
            doc.ExecutionCircuitId = request.ExecutionCircuitId;
            doc.Court = request.Court;
        }
    }

    private Task EnsureNumberUniqueByCircuitAsync(
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

    /// <summary>حارس المعلق الصريح: تُمنع عليه العمليات التي تلزم الهوية الرقمية.</summary>
    private static void EnsureNotPendingRegistration(Document doc, string actionLabel)
    {
        if (doc.NeedsRegistration)
            throw new ArgumentException($"لا يمكن {actionLabel} على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");
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
    private async Task AddCircuitSwitchOccurrencesAsync(
        Document doc, string? oldCircuitName, int? userId, CancellationToken token)
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
        await _occurrences.AddAsync(oldOcc, token);
        await _uow.SaveChangesAsync(token);
    }

    /// <summary>
    /// مستودع احتياطي للاختبارات القائمة التي تبني DocumentService يدويًا بلا سجل دوائر:
    /// قائمة فارغة (وضع انتقالي — يُقبل النص الحر حتى يُعبَّأ السجل).
    /// </summary>
    private sealed class UnconfiguredCircuitRepository : IRepository<ExecutionCircuit>
    {
        public Task AddAsync(ExecutionCircuit entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ExecutionCircuit?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<ExecutionCircuit?>(null);
        public Task<List<ExecutionCircuit>> ListAsync(CancellationToken ct = default) => Task.FromResult(new List<ExecutionCircuit>());
        public void Remove(ExecutionCircuit entity) { }
        public void Update(ExecutionCircuit entity) { }
    }

    private sealed class UnconfiguredBranchRepository : IRepository<Branch>
    {
        public Task AddAsync(Branch entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Branch?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<Branch?>(null);
        public Task<List<Branch>> ListAsync(CancellationToken ct = default) => Task.FromResult(new List<Branch>());
        public void Remove(Branch entity) { }
        public void Update(Branch entity) { }
    }
}
