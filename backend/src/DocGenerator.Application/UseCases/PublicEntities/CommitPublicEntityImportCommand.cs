using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>اعتماد الاستيراد التاريخي لسجل الجهات — بمفتاح عدم تكرار (RF-011).</summary>
public sealed record CommitPublicEntityImportCommand(
    ImportCommitRequest Request,
    int ActorUserId,
    string? ActorName,
    string? IdempotencyKey = null) : ICommand<ImportCommitResultDto>;

internal sealed class CommitPublicEntityImportCommandHandler
    : CommandHandlerBase<CommitPublicEntityImportCommand, ImportCommitResultDto>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IIdempotencyStore? _idempotency;

    public CommitPublicEntityImportCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CommitPublicEntityImportCommand, ImportCommitResultDto>> behaviors,
        IIdempotencyStore? idempotency = null)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _idempotency = idempotency;
    }

    protected override async Task<ImportCommitResultDto> HandleCoreAsync(
        CommitPublicEntityImportCommand command, CancellationToken ct)
    {
        var request = command.Request;
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("لم تُحدَّد نصوص للاستيراد");

        // RF-011: حجز المفتاح بعد التحقق (المدخل الباطل 400 بلا حجز) — التكرار
        // يُعيد النتيجة المخزنة نفسها (الاعتماد الثاني: كل `skipped` لا إنشاء).
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "registry.import-commit",
            command.ActorUserId, command.IdempotencyKey, IdempotencyGuard.Fingerprint(request), ct);
        ImportCommitResultDto result;
        try
        {
            result = await CommitImportCoreAsync(
                request, command.ActorUserId, command.ActorName, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket);
            throw;
        }

        await IdempotencyGuard.CompleteAsync(_idempotency, ticket, IdempotencyGuard.Snapshot(result));
        return result;
    }

    private async Task<ImportCommitResultDto> CommitImportCoreAsync(
        ImportCommitRequest request, int actorUserId, string? actorName, CancellationToken ct)
    {
        // مرجعية الخادم: الكتابات البديلة تؤخذ من معاينة حية لا من طلب العميل.
        var candidates = (await _rules.BuildImportCandidatesAsync(ct))
            .ToDictionary(i => i.NormalizedName, StringComparer.Ordinal);

        int groupsCreated = 0, entriesCreated = 0, aliasesAdded = 0, skipped = 0;
        var knownGroups = new Dictionary<string, PublicEntityGroup>(StringComparer.Ordinal);

        await _tx.RunAsync(async token =>
        {
            foreach (var item in request.Items)
            {
                var canonical = _rules.Required(item.CanonicalName, "اسم الجهة مطلوب", 200);
                var entityType = _rules.ValidEntityType(item.EntityType);
                var governorate = _rules.Required(item.Governorate, "المحافظة مطلوبة", 100);
                var branchName = _rules.RequiredWithFallback(
                    item.BranchName, PublicEntityBranchCatalog.ParentBranchName, 200);
                var citationFormula = _rules.ValidCitationFormula(
                    item.CitationFormula, CitationFormulaCatalog.AddToJob);

                // فحص التكرار قبل اشتراط المعاينة: إعادة اعتماد بند مستورد سابقًا تتجاهله بهدوء.
                var canonicalNorm = ArabicNameNormalizer.Normalize(canonical);
                if (!knownGroups.TryGetValue(canonicalNorm, out var group))
                    group = await _rules.FindGroupByNormAsync(canonicalNorm, token);
                if (group is not null
                    && await _entities.EntryExistsAsync(group.Id, governorate, branchName, token))
                {
                    skipped++;
                    continue;
                }

                // مرجعية الخادم: الكتابات البديلة تؤخذ من معاينة حية لا من طلب العميل.
                if (!candidates.TryGetValue(item.NormalizedName, out var candidate))
                    throw new ArgumentException($"النص غير موجود في المعاينة الحالية: {item.NormalizedName}");

                if (group is null)
                {
                    group = new PublicEntityGroup
                    {
                        CanonicalName = canonical,
                        CanonicalNameNorm = canonicalNorm,
                        EntityType = entityType,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    };
                    await _entities.AddGroupAsync(group, token);
                    groupsCreated++;
                }
                knownGroups[canonicalNorm] = group;

                var entry = new PublicEntity
                {
                    Group = group,
                    Governorate = governorate,
                    BranchName = branchName,
                    CitationFormula = citationFormula,
                    Status = EntityStatusCatalog.Final,
                    CreatedById = actorUserId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                };
                if (item.AddVariantsAsAliases)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal) { canonicalNorm };
                    foreach (var variant in candidate.Variants)
                    {
                        var vNorm = ArabicNameNormalizer.Normalize(variant.Text);
                        if (vNorm.Length == 0 || !seen.Add(vNorm))
                            continue;
                        entry.Aliases.Add(new PublicEntityAlias { AliasText = variant.Text });
                        aliasesAdded++;
                    }
                }
                await _entities.AddEntryAsync(entry, token);
                entriesCreated++;
            }

            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "import_entity_registry",
                details: $"استورد نصوصًا تاريخية: {entriesCreated} قيدًا نهائيًا ({groupsCreated} هوية، {aliasesAdded} اسمًا بديلًا، تجاهل {skipped})", ct: token);
        }, ct);

        return new ImportCommitResultDto(groupsCreated, entriesCreated, aliasesAdded);
    }
}
