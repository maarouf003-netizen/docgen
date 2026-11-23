using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
    public async Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(request.BorrowerName)
            && (request.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                || string.IsNullOrEmpty(request.GeneralEntitySide)))
            throw new ArgumentException("اسم المقترض مطلوب");

        DocumentValidator.NormalizeCollections(request);
        DocumentValidator.ValidateSide(request);
        DocumentValidator.ValidateExecutedRequest(request);
        DocumentValidator.ValidateGuarantorCount(request);
        DocumentValidator.ValidateRegistrationDate(request);
        DocumentValidator.ValidateFileYearNotFuture(request.FileYear, CurrentYear());
        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
        DocumentValidator.ValidateAnnexes(request);

        // المحامي المختص هو الذي سجّل الدخول وأنشأ الملف: يُسنَد الملف إليه تلقائياً
        // باسمه الكامل (وليس من الطلب)، ويبقى محصّناً من أي كتابة عبر نموذج التعديل.
        var actor = await _users.GetByIdAsync(userId, ct);
        if (actor is null)
            throw new ArgumentException("المستخدم الذي ينشئ الملف غير موجود");

        var doc = new Document
        {
            BranchId = branchId,
            CreatedById = userId,
            Lawyer = string.IsNullOrWhiteSpace(actor.FullName) ? actor.Username : actor.FullName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        ApplyRequest(doc, request);
        // سجل الدوائر: اشتقاق الدائرة (إلزامية) قبل المشتقات — يتجاهل نص العميل.
        var createCircuit = await _writeGuard.ResolveCircuitForWriteAsync(request, branchId, isExternalTarget: false, ct);
        DocumentText.ApplyCircuitDerivation(doc, createCircuit, request);
        DocumentText.FillDerivedFields(doc);
        ApplyRegistrationDate(doc, request.FileRegistrationDate);

        // RF-011: حجز المفتاح بعد كل التحققات (المدخل الباطل 400 بلا حجز) وقبل أي
        // كتابة — التكرار بنفس البصمة يُعيد المخزن (`IdempotentReplayException`)،
        // والقيد المعالَج يُرفَض `409`، والبصمة المختلفة نية جديدة تُنفَّذ طازجة.
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "documents.create",
            userId, idempotencyKey, IdempotencyGuard.Fingerprint(request), ct);

        DocumentResponse result;
        try
        {
            result = await CreateCoreAsync(doc, request, userId, actor, actorName, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        await IdempotencyGuard.CompleteAsync(_idempotency, ticket, IdempotencyGuard.Snapshot(result), ct: ct);
        return result;
    }

    /// <summary>نواة الإنشاء (فحص الترقيم + المعاملة) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<DocumentResponse> CreateCoreAsync(
        Document doc, DocumentUpsertRequest request, int userId, User actor, string? actorName, CancellationToken ct)
    {
        // RF-009 بتوقيع الدائرة (البند 28/34): مطابقة FK مباشرة بدل النص.
        await _writeGuard.EnsureNumberUniqueByCircuitAsync(null, doc.ExecutionCircuitId, doc.CourtNorm,
            doc.FileNumber, doc.FileType, doc.FileYear, ct);

        return await _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            await _documents.AddAsync(doc, token);
            await _uow.SaveChangesAsync(token);
            // الكاتب الوحيد: مزامنة صف القيد بعد حصول المستند على معرّفه (داخل المعاملة).
            await _numberRegistry.SyncRegistrationAsync(doc, userId, actorName, request.WarningToken, token, isNewDocument: true);
            // أول سجل تعاقب: منشئ الملف (Lawyer هو اسم المنشئ عند الإنشاء).
            await _documents.AddAssignmentAsync(doc.Id, AssignmentKindCatalog.Create,
                doc.Lawyer ?? actor.Username, null, DateTime.UtcNow, token);
            await _audit.LogAsync(actorName, "create", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"أنشأ المستند (رقم {doc.Id})", doc), token);
            // ملف أُنشئ مشطوبًا من البداية: يُسجَّل وقعة شطب تلقائيًا ليتسق سجل الوقوعات
            // مع البيانات المُرحَّلة (بعد حفظ المستند فيُعرف رقمه داخل المعاملة نفسها).
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                await _statusService.AddStruckOffOccurrenceAsync(doc, userId, token);
            await _uow.SaveChangesAsync(token);
            await _actionService.SeedInitialActionsAsync(doc, request.InitialActions, userId, actorName, token);
            return DocumentResponse.FromEntity(doc, CurrentYear());
        }, ct), doc.FileNumber);
    }
}
