using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    /// <summary>
    /// نقل ملكية دائرة لمالك جديد داخل الفرع نفسه (قرار §2.5 + §8.3): المفتوح
    /// والمغلق يتبع الجديد (الملكية على الدائرة نفسها)، والتدقيق ثابت. الإحالات
    /// المفتوحة (§6.6): تُنشأ تنبيهات بديلة للمالك الجديد (لا تعديل القديمة) —
    /// و`ForwardState` لا يتغير (استثناء فرعي لا شعبي).
    /// </summary>
    public async Task<ExecutionCircuitDto> TransferCircuitAsync(
        int circuitId, int? targetSectionId, int actorUserId, string? actorName, CancellationToken ct = default, long? version = null)
    {
        ExecutionCircuitDto result = null!;
        try
        {
            await _tx.RunAsync(async token =>
            {
                var circuit = await _circuits.GetByIdAsync(circuitId, token)
                    ?? throw new ArgumentException("الدائرة غير موجودة");
                if (version is not null && circuit.Version != version.Value)
                    throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");

                Section? targetSection = null;
                if (targetSectionId.HasValue)
                {
                    targetSection = await _sections.GetByIdAsync(targetSectionId.Value, token)
                        ?? throw new ArgumentException("الشعبة الهدف غير موجودة");
                    if (targetSection.BranchId != circuit.BranchId)
                        throw new ArgumentException("النقل داخل الفرع نفسه — الشعبة من فرع آخر");
                    if (!targetSection.IsActive)
                        throw new ArgumentException("الشعبة الهدف معطلة — اختر شعبة نشطة");
                }

                if (circuit.SectionId == targetSectionId)
                {
                    result = await ToDtoAsync(circuit, token);
                    return;
                }

                var oldOwner = circuit.SectionId.HasValue
                    ? (await _sections.GetByIdAsync(circuit.SectionId.Value, token))?.Name ?? "؟"
                    : "رئيس القسم";
                var newOwner = targetSection?.Name ?? "رئيس القسم";

                circuit.SectionId = targetSectionId;
                circuit.UpdatedAt = DateTime.UtcNow;
                circuit.Version++;
                _circuits.Update(circuit);
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
                // النتيجة تُثبَّت هنا قبل أي خروج مبكر (مالك بلا رئيس) — وإلا عاد
                // `null` فتحوّل `Ok` إلى `204`.
                result = await ToDtoAsync(circuit, token);

                await _audit.LogAsync(actorName, "transfer_circuit", null, null,
                    $"نقل الدائرة «{circuit.Name}» من {oldOwner} إلى {newOwner} (الملفات المفتوحة والمغلقة تتبع المالك الجديد)", token);

                // المالك الجديد: رئيس الشعبة الهدف، أو رئيس القسم للقسم.
                var newHead = targetSectionId.HasValue
                    ? await _users.FindActiveHeadAsync(UserRole.SubHead, circuit.BranchId, targetSectionId, token)
                    : await _users.FindActiveHeadAsync(UserRole.Head, circuit.BranchId, null, token);
                if (newHead is null)
                    return;

                await _successions.AddAsync(new HeadSuccession
                {
                    BranchId = circuit.BranchId,
                    SectionId = targetSectionId,
                    UserId = newHead.Id,
                    Role = newHead.Role,
                    Event = HeadSuccessionEventCatalog.CircuitTransferred,
                    At = DateTime.UtcNow,
                    ActorName = actorName,
                    Reason = $"نُقلت الدائرة «{circuit.Name}» من {oldOwner} إلى {newOwner}",
                }, token);

                // بدائل المعلّق للمالك الجديد (§6.6): إنابات واردة معلقة + استئنافات
                // بلا إسناد على ملفات الدائرة — تنبيه واحد جامع (لا تعديل القديمة).
                var fileIds = (await _documents.ListByCircuitAsync(circuit.Id, includeDeleted: false, token))
                    .Select(d => d.Id)
                    .ToList();
                var pendingDelegations = (await _delegations.ListByDelegatedCircuitAsync(circuit.Id, token))
                    .Count(g => g.Status == DelegationStatusCatalog.PendingHead);
                var pendingAppeals = 0;
                if (fileIds.Count > 0)
                {
                    pendingAppeals = (await _appeals.ListByDocumentIdsAsync(fileIds, token))
                        .Count(a => a.Status == AppealStatusCatalog.Pending && a.AssignedLawyerId == null);
                }

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.CircuitTransferred,
                        HeadAlertMessages.CircuitTransferred(new(
                            circuit.Name, fileIds.Count, pendingDelegations, pendingAppeals)),
                        HeadAlertAudience.SingleHead,
                        circuit.BranchId,
                        actorUserId,
                        ExecutionCircuitId: circuit.Id,
                        RecipientUserId: newHead.Id),
                    supersedePlan: null,
                    occurredAt: DateTime.UtcNow,
                    ct: token);
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // كتابتان متزامنتان حقيقيتان على الدائرة نفسها — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا", ex);
        }
        return result;
    }
}
