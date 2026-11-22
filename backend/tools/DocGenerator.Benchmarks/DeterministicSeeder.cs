using DocGenerator.Application.Common;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Benchmarks;

/// <summary>
/// بذر حتمي لبيانات القياس: نفس البذرة والحجم يعطيان نفس الصفوف والعلاقات في كل تشغيل.
/// لا يستخدم أي مولّد عشوائي غير مثبّت البذرة، ولا أي مصدر وقت حقيقي في الحقول الدلالية.
/// </summary>
public static class DeterministicSeeder
{
    public const string TextSearchTerm = "أحمد";

    private static readonly DateTime TimelineStart =
        new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] BranchNames =
    {
        "مديرية تنفيذ دمشق",
        "مديرية تنفيذ حلب",
        "مديرية تنفيذ حمص",
        "مديرية تنفيذ اللاذقية",
        "مديرية تنفيذ طرطوس",
    };

    private static readonly string[] BranchCodes = { "DAM", "ALP", "HMS", "LAT", "TAR" };

    private static readonly string[] Governorates =
    {
        "دمشق", "ريف دمشق", "حلب", "حمص", "حماة", "اللاذقية", "طرطوس", "إدلب", "درعا", "السويداء",
    };

    private static readonly string[] SectionNames = { "قسم التنفيذ الأول", "قسم التنفيذ الثاني" };

    private static readonly string[] FirstNames =
    {
        "أحمد", "محمد", "خالد", "سامر", "يوسف", "عمر", "سليم", "ناصر", "فادي", "مازن",
    };

    private static readonly string[] FatherNames =
    {
        "عبد الله", "محمود", "سليمان", "فؤاد", "ناصر", "كمال", "سمير", "جمال", "رياض", "منير",
    };

    private static readonly string[] FamilyNames =
    {
        "الحلبي", "الدمشقي", "الشامي", "درويش", "العلي", "حسين", "قاسم", "حمدان", "بركات", "الخطيب",
    };

    private static readonly string[] CircuitSuffixes = { "الأولى", "الثانية" };

    private static readonly string[] PublicEntityPrefixes =
    {
        "وزارة", "مديرية", "مؤسسة", "شركة", "هيئة", "أمانة", "مجلس",
    };

    public static async Task<SeedManifest> SeedAsync(
        DocGeneratorPostgresDbContext db,
        int scale,
        int seed,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var random = new Random(seed);

        var branches = CreateBranches();
        db.Branches.AddRange(branches);
        await db.SaveChangesAsync(ct);

        var sections = CreateSections(branches);
        db.Sections.AddRange(sections);
        await db.SaveChangesAsync(ct);

        var users = CreateUsers(branches, sections);
        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        var admin = users.Single(u => u.Role == UserRole.Admin);
        var heads = users.Where(u => u.Role == UserRole.Head).ToList();
        var lawyers = users.Where(u => u.Role == UserRole.Lawyer).ToList();
        var circuits = CreateCircuits(branches, sections, heads);
        db.ExecutionCircuits.AddRange(circuits);
        await db.SaveChangesAsync(ct);

        var groups = CreatePublicEntityGroups();
        db.PublicEntityGroups.AddRange(groups);
        await db.SaveChangesAsync(ct);

        var entries = CreatePublicEntityEntries(groups, admin);
        db.PublicEntities.AddRange(entries);
        await db.SaveChangesAsync(ct);

        var portalEntryIds = entries
            .Where(e => e.GroupId == groups[0].Id)
            .OrderBy(e => e.Id)
            .Take(20)
            .Select(e => e.Id)
            .ToList();

        progress?.Report($"البذر: {scale:N0} مستندًا…");
        var portalDocumentId = await SeedDocumentsAsync(
            db, scale, circuits, lawyers, entries, random, progress, ct);

        // مؤشر حدود الصفحة 999 من **نفس استعلام القائمة الرئيسية** (بالفلاتر الافتراضية)
        // لا من فهرس البذر: القائمة تخفي المنفذ/المشطوب/المحال، فحدود الإزاحة تختلف.
        var cursorRepository = new DocumentRepository(db);
        var (_, cursorPage, _) = await cursorRepository.SearchListAsync(
            query: null, status: null, applicant: null, court: null, lawyer: null,
            branch: null, administrativeBranch: null, executedEntity: null,
            publicEntityBranch: null, visibleBranchId: null, visibleUserId: null,
            page: 999, perPage: 20, ct: ct);
        var cursorRow = cursorPage[^1];

        progress?.Report("البذر: تنبيهات الرؤساء…");
        var head = heads[0];
        await SeedHeadAlertsAsync(db, Math.Max(200, scale / 10), head, lawyers, random, ct);

        return new SeedManifest(
            BranchIds: branches.Select(b => b.Id).ToList(),
            PortalEntryIds: portalEntryIds,
            PortalDocumentId: portalDocumentId,
            HeadAlertUserId: head.Id,
            HeadAlertBranchId: head.BranchId!.Value,
            TextSearchTerm: TextSearchTerm,
            DocumentCount: scale,
            CursorCreatedAt: cursorRow.CreatedAt,
            CursorId: cursorRow.Id);
    }

    private static List<Branch> CreateBranches()
    {
        var branches = new List<Branch>(BranchNames.Length);
        for (var i = 0; i < BranchNames.Length; i++)
        {
            branches.Add(new Branch
            {
                Name = BranchNames[i],
                Code = BranchCodes[i],
                Governorate = Governorates[i],
                Address = $"مركز {BranchNames[i]}",
                IsActive = true,
                CreatedAt = TimelineStart,
            });
        }

        return branches;
    }

    private static List<Section> CreateSections(List<Branch> branches)
    {
        var sections = new List<Section>();
        foreach (var branch in branches)
        {
            for (var i = 0; i < SectionNames.Length; i++)
            {
                var name = $"{SectionNames[i]} - {branch.Name}";
                sections.Add(new Section
                {
                    BranchId = branch.Id,
                    Name = name,
                    NameNorm = ArabicNameNormalizer.Normalize(name),
                    IsActive = true,
                    CreatedAt = TimelineStart,
                    UpdatedAt = TimelineStart,
                });
            }
        }

        return sections;
    }

    private static List<User> CreateUsers(List<Branch> branches, List<Section> sections)
    {
        // هاش واحد يُعاد استخدامه: القياس لا يسجّل الدخول، والبذر يجب أن يبقى سريعًا وحتميًا.
        var hash = new PasswordHasher().Hash("BenchmarkPassword1!");

        var users = new List<User>
        {
            new()
            {
                Username = "bench-admin",
                FullName = "مدير القياس",
                Role = UserRole.Admin,
                PasswordHash = hash,
                IsActive = true,
                CreatedAt = TimelineStart,
                UpdatedAt = TimelineStart,
            },
            new()
            {
                Username = "bench-manager",
                FullName = "مدير عام القياس",
                Role = UserRole.Manager,
                PasswordHash = hash,
                IsActive = true,
                CreatedAt = TimelineStart,
                UpdatedAt = TimelineStart,
            },
        };

        for (var i = 0; i < branches.Count; i++)
        {
            var branch = branches[i];
            var section = sections.First(s => s.BranchId == branch.Id);
            users.Add(new User
            {
                Username = $"bench-head-{i + 1}",
                FullName = $"رئيس قسم {branch.Name}",
                Role = UserRole.Head,
                BranchId = branch.Id,
                SectionId = section.Id,
                PasswordHash = hash,
                IsActive = true,
                CreatedAt = TimelineStart,
                UpdatedAt = TimelineStart,
            });

            for (var j = 0; j < 2; j++)
            {
                users.Add(new User
                {
                    Username = $"bench-lawyer-{i + 1}-{j + 1}",
                    FullName = $"محامي {branch.Name} {j + 1}",
                    Role = UserRole.Lawyer,
                    BranchId = branch.Id,
                    PasswordHash = hash,
                    IsActive = true,
                    CreatedAt = TimelineStart,
                    UpdatedAt = TimelineStart,
                });
            }
        }

        return users;
    }

    private static List<ExecutionCircuit> CreateCircuits(
        List<Branch> branches, List<Section> sections, List<User> heads)
    {
        var circuits = new List<ExecutionCircuit>();
        foreach (var branch in branches)
        {
            var head = heads.First(h => h.BranchId == branch.Id);
            var section = sections.First(s => s.BranchId == branch.Id);
            for (var i = 0; i < CircuitSuffixes.Length; i++)
            {
                var name = $"دائرة تنفيذ {branch.Governorate} {CircuitSuffixes[i]}";
                circuits.Add(new ExecutionCircuit
                {
                    BranchId = branch.Id,
                    SectionId = section.Id,
                    Name = name,
                    NameNorm = ArabicNameNormalizer.Normalize(name),
                    IsActive = true,
                    CreatedById = head.Id,
                    CreatedAt = TimelineStart,
                    UpdatedAt = TimelineStart,
                });
            }
        }

        return circuits;
    }

    private static List<PublicEntityGroup> CreatePublicEntityGroups()
    {
        var groups = new List<PublicEntityGroup>();
        for (var i = 0; i < BranchNames.Length; i++)
        {
            var name = $"{PublicEntityPrefixes[i % PublicEntityPrefixes.Length]} القياس {i + 1}";
            groups.Add(new PublicEntityGroup
            {
                CanonicalName = name,
                CanonicalNameNorm = ArabicNameNormalizer.Normalize(name),
                EntityType = PublicEntityTypeCatalog.Ministry,
                IsActive = true,
                CreatedAt = TimelineStart,
            });
        }

        return groups;
    }

    private static List<PublicEntity> CreatePublicEntityEntries(
        List<PublicEntityGroup> groups, User admin)
    {
        var entries = new List<PublicEntity>();
        foreach (var group in groups)
        {
            for (var i = 0; i < 20; i++)
            {
                var governorate = Governorates[i % Governorates.Length];
                entries.Add(new PublicEntity
                {
                    GroupId = group.Id,
                    Governorate = governorate,
                    BranchName = $"{group.CanonicalName} - فرع {i + 1}",
                    CoverageLabel = governorate,
                    CitationFormula = CitationFormulaCatalog.AddToJob,
                    Status = EntityStatusCatalog.Final,
                    NeedsReview = false,
                    IsActive = true,
                    CreatedById = admin.Id,
                    CreatedAt = TimelineStart,
                });
            }
        }

        return entries;
    }

    private static async Task<int> SeedDocumentsAsync(
        DocGeneratorPostgresDbContext db,
        int scale,
        List<ExecutionCircuit> circuits,
        List<User> lawyers,
        List<PublicEntity> entries,
        Random random,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        const int batchSize = 500;
        var portalDocumentId = 0;

        for (var start = 0; start < scale; start += batchSize)
        {
            var end = Math.Min(scale, start + batchSize);
            var documents = new List<Document>(end - start);

            for (var i = start; i < end; i++)
            {
                var document = BuildDocument(i, circuits, lawyers, entries, random);
                documents.Add(document);
            }

            db.Documents.AddRange(documents);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            if (portalDocumentId == 0)
            {
                // أول مستند مربوط بقيد البوابة الأولى: يُستخدم لقياس تفاصيل ملف البوابة.
                portalDocumentId = documents[0].Id;
            }

            progress?.Report($"البذر: {end:N0}/{scale:N0}");
        }

        return portalDocumentId;
    }

    private static Document BuildDocument(
        int index,
        List<ExecutionCircuit> circuits,
        List<User> lawyers,
        List<PublicEntity> entries,
        Random random)
    {
        var circuit = circuits[index % circuits.Count];
        var lawyer = lawyers[index % lawyers.Count];
        var entry = entries[index % entries.Count];
        var createdAt = TimelineStart.AddMinutes(index * 30L);
        var firstName = FirstNames[index % FirstNames.Length];
        var father = FatherNames[(index / 2) % FatherNames.Length];
        var family = FamilyNames[(index / 3) % FamilyNames.Length];

        var side = (index % 10) switch
        {
            < 6 => GeneralEntitySideCatalog.Applicant,
            < 8 => GeneralEntitySideCatalog.Executed,
            _ => GeneralEntitySideCatalog.Deposit,
        };

        var isDraft = side == GeneralEntitySideCatalog.Applicant && index % 7 == 0;
        var execStatus = side == GeneralEntitySideCatalog.Applicant && !isDraft
            ? BuildExecStatus(index)
            : ExecutionStatusCatalog.None;
        var execSubStatus = execStatus == ExecutionStatusCatalog.ExecutedForcibly
            ? (index % 3) switch
            {
                0 => ExecutionStatusCatalog.SubPartiallyExecuted,
                1 => ExecutionStatusCatalog.SubFullyExecuted,
                _ => null,
            }
            : null;

        var executedStatus = side switch
        {
            GeneralEntitySideCatalog.Executed => (index % 17) switch
            {
                0 => ExecutedStatusCatalog.StruckOff,
                1 or 2 or 3 => ExecutedStatusCatalog.Executed,
                _ => ExecutedStatusCatalog.None,
            },
            GeneralEntitySideCatalog.Deposit => index % 4 == 0
                ? ExecutedStatusCatalog.Executed
                : ExecutedStatusCatalog.None,
            _ => ExecutedStatusCatalog.None,
        };

        var isBanking = index % 3 != 0;
        var amount = 100_000m + (index % 50) * 1_000m;
        var collected = execStatus is ExecutionStatusCatalog.ExecutedBySettlement
            or ExecutionStatusCatalog.ExecutedForcibly
            ? amount / 2
            : (decimal?)null;

        var registrationDate = createdAt.AddDays(-(index % 20));
        var fileNumber = ((index / 10) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var document = new Document
        {
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            CreatedById = lawyer.Id,
            BranchId = circuit.BranchId,
            ExecutionCircuitId = circuit.Id,
            BranchName = BranchNames[circuit.BranchId % BranchNames.Length],
            IsDeleted = false,
            DocumentType = index % 2 == 0 ? "تنفيذ" : "اعتراض",
            IsDraft = isDraft,
            BorrowerName = firstName,
            BorrowerFather = father,
            BorrowerFamily = family,
            BorrowerNature = PartyNatureCatalog.Natural,
            ContractTypeSelector = isBanking ? ContractTypeCatalog.Banking : ContractTypeCatalog.Ordinary,
            ContractNumber = $"C-{index + 1}",
            ContractDate = $"{registrationDate.Day}/{registrationDate.Month}/{registrationDate.Year}",
            AmountNumeric = isBanking ? amount : 0m,
            InclusionAmountNumeric = isBanking ? 0m : amount,
            Currency = "ليرة سورية",
            InclusionCurrency = "ليرة سورية",
            Court = circuit.Name,
            CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name),
            Applicant = $"جهة طالبة {index % 50}",
            Lawyer = lawyer.FullName,
            FileNumber = fileNumber,
            FileType = "تنفيذ",
            FileYear = "2026",
            ExecStatus = execStatus,
            ExecSubStatus = execSubStatus,
            CollectedAmount = collected,
            GeneralEntitySide = side,
            ExecutedStatus = executedStatus,
            WasDepositExecuted = side == GeneralEntitySideCatalog.Deposit
                && (executedStatus == ExecutedStatusCatalog.Executed || index % 11 == 0),
            FileReceiptDate = side == GeneralEntitySideCatalog.Applicant
                ? null
                : createdAt.AddDays(-(index % 10)),
            FileReceiptNumber = side == GeneralEntitySideCatalog.Applicant ? null : $"R-{index + 1}",
            ExecutedRequiredAmount = side == GeneralEntitySideCatalog.Executed ? amount : null,
            ExecutedPaidAmount = executedStatus == ExecutedStatusCatalog.Executed
                ? amount / 2
                : null,
            SearchText = ArabicNameNormalizer.Normalize(
                $"{firstName} {father} {family} {entry.BranchName} {circuit.Name}"),
            ApplicantRegistryId = side == GeneralEntitySideCatalog.Applicant ? entry.Id : null,
        };

        if (index % 10 == 0)
        {
            document.RegistrationDate = new DocumentRegistrationDate
            {
                Date = $"{registrationDate.Day}/{registrationDate.Month}/{registrationDate.Year}",
                DateParsed = registrationDate,
            };
        }

        document.BaseNumbers.Add(new DocumentBaseNumber
        {
            ExecutionCircuitId = circuit.Id,
            CircuitName = circuit.Name,
            Year = 2026,
            FileType = "تنفيذ",
            TypeKey = ArabicNameNormalizer.Normalize("تنفيذ"),
            BaseNumber = fileNumber,
            NumberKey = ArabicNameNormalizer.Normalize(fileNumber),
            Source = "registration",
            CreatedById = lawyer.Id,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });

        document.Assignments.Add(new DocumentAssignment
        {
            Kind = AssignmentKindCatalog.Create,
            LawyerName = lawyer.FullName,
            AssignedByName = lawyer.FullName,
            AssignedAt = createdAt,
        });

        if (index % 3 == 0)
        {
            document.Guarantors.Add(new Guarantor
            {
                GuarantorNumber = 1,
                GuarantorName = FirstNames[(index + 1) % FirstNames.Length],
                GuarantorFather = FatherNames[(index + 3) % FatherNames.Length],
                GuarantorFamily = FamilyNames[(index + 5) % FamilyNames.Length],
                GuarantorNature = PartyNatureCatalog.Natural,
            });
        }

        if (index % 4 == 0)
        {
            var asset = new Asset
            {
                AssetKind = AssetKindCatalog.RealEstate,
                Property = $"عقار {index + 1}",
                PropertyNumber = $"P-{index + 1}",
                PropertyDistrict = circuit.Name,
                LandRegistry = $"سجل {index % 20}",
                RightType = "ملك",
            };
            asset.Owners.Add(new AssetOwner
            {
                Name = $"{firstName} {family}",
                Order = 1,
            });
            document.Assets.Add(asset);
        }

        if (index % 5 == 0)
        {
            document.Heirs.Add(new Heir
            {
                HeirName = FirstNames[(index + 2) % FirstNames.Length],
                HeirFather = FatherNames[(index + 4) % FatherNames.Length],
                HeirFamily = family,
                HeirCapacity = "ابن",
            });
        }

        document.ExecutionActions.Add(new ExecutionAction
        {
            Type = "action",
            Text = $"إجراء تنفيذي {index + 1}",
            ActionDate = $"{createdAt.Day}/{createdAt.Month}/{createdAt.Year}",
            ActionDateParsed = createdAt,
            CreatedById = lawyer.Id,
            CreatedAt = createdAt,
        });

        if (index % 6 == 0)
        {
            document.Annexes.Add(new DocumentAnnex
            {
                Sequence = 1,
                AnnexType = "ملحق",
                AnnexNumber = $"A-{index + 1}",
                AnnexDate = $"{createdAt.Day}/{createdAt.Month}/{createdAt.Year}",
            });
        }

        if (side == GeneralEntitySideCatalog.Applicant)
        {
            document.ApplicantPublicEntities.Add(new ApplicantPublicEntity
            {
                Name = entry.BranchName,
                Branch = entry.BranchName,
                Governorate = entry.Governorate,
                RegistryId = entry.Id,
            });
        }
        else
        {
            document.ExecutionApplicants.Add(new ExecutionApplicant
            {
                Name = FirstNames[(index + 6) % FirstNames.Length],
                Father = FatherNames[(index + 2) % FatherNames.Length],
                Family = FamilyNames[(index + 7) % FamilyNames.Length],
                ApplicantNature = PartyNatureCatalog.Natural,
                RegistryId = entry.Id,
            });

            document.ExecutedPublicEntities.Add(new ExecutedPublicEntity
            {
                EntityName = entry.BranchName,
                EntityBranch = entry.BranchName,
                Governorate = entry.Governorate,
                RegistryId = entry.Id,
                EntityNature = PartyNatureCatalog.PublicEntity,
            });

            if (index % 2 == 0)
            {
                var person = new ExecutedNaturalPerson
                {
                    Name = FirstNames[(index + 8) % FirstNames.Length],
                    Father = FatherNames[(index + 1) % FatherNames.Length],
                    Family = family,
                };
                document.ExecutedNaturalPersons.Add(person);

                // `ExecutedHeir.DocumentId` إلزامي مستقل عن رابط الشخص المنفذ عليه،
                // فيُضاف إلى مجموعة المستند نفسها مع ربط الشخص (وليس إلى مجموعة الشخص فقط).
                document.ExecutedHeirs.Add(new ExecutedHeir
                {
                    ExecutedNaturalPerson = person,
                    HeirName = FirstNames[(index + 9) % FirstNames.Length],
                    HeirFather = FatherNames[(index + 5) % FatherNames.Length],
                    HeirFamily = family,
                });
            }
        }

        if (index % 10 == 0)
        {
            document.Occurrences.Add(new DocumentOccurrence
            {
                OccurrenceType = OccurrenceTypeCatalog.StruckOff,
                Source = OccurrenceSourceCatalog.System,
                EventDate = createdAt.AddDays(30),
                FileNumber = fileNumber,
                FileType = "تنفيذ",
                Year = 2026,
                CreatedById = lawyer.Id,
                CreatedAt = createdAt.AddDays(30),
                UpdatedAt = createdAt.AddDays(30),
            });

            document.Interventions.Add(new DocumentIntervention
            {
                Nature = PartyNatureCatalog.Natural,
                ReceiptNumber = $"I-{index + 1}",
                ReceiptDate = createdAt,
                AcceptanceDate = createdAt.AddDays(5),
                Subject = "طلب تدخل تجريبي",
                Name = FirstNames[(index + 4) % FirstNames.Length],
                Father = father,
                Family = family,
                Governorate = entry.Governorate,
                CreatedById = lawyer.Id,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
        }

        // مقدار عشوائي محدود لا يغيّر الحتمية: البذرة ثابتة والترتيب ثابت.
        document.PrintCount = random.Next(0, 4);
        return document;
    }

    private static string BuildExecStatus(int index) => (index % 11) switch
    {
        0 or 1 or 2 or 3 or 4 => ExecutionStatusCatalog.None,
        5 => ExecutionStatusCatalog.Deferred,
        6 or 7 => ExecutionStatusCatalog.ExecutedBySettlement,
        8 or 9 => ExecutionStatusCatalog.ExecutedForcibly,
        10 => index % 4 == 0
            ? ExecutionStatusCatalog.DelegationExecuted
            : ExecutionStatusCatalog.Recovered,
        _ => ExecutionStatusCatalog.None,
    };

    private static async Task SeedHeadAlertsAsync(
        DocGeneratorPostgresDbContext db,
        int count,
        User head,
        List<User> lawyers,
        Random random,
        CancellationToken ct)
    {
        const int batchSize = 500;
        for (var start = 0; start < count; start += batchSize)
        {
            var end = Math.Min(count, start + batchSize);
            var alerts = new List<HeadAlert>(end - start);

            for (var i = start; i < end; i++)
            {
                var changedAt = TimelineStart.AddMinutes(i * 45L);
                var message =
                    $"تنبيه قياس رقم {i + 1}: ملف تجريبي لمتابعة إجراءات التنفيذ والتحقق من أداء الملخصات "
                    + string.Join(' ', Enumerable.Repeat("نص", 1 + (i % 40)));

                var alert = new HeadAlert
                {
                    BranchId = head.BranchId!.Value,
                    CreatedById = head.Id,
                    TargetType = HeadAlertTargetType.Head,
                    Kind = HeadAlertKindCatalog.ManualToHead,
                    DedupeKey = $"bench-alert-{i + 1}",
                    Message = message,
                    CreatedAt = changedAt,
                    LastContentChangedAt = changedAt,
                    IsArchived = i % 10 == 0,
                    ArchivedAt = i % 10 == 0 ? changedAt.AddDays(10) : null,
                };

                alert.Recipients.Add(new HeadAlertRecipient
                {
                    UserId = head.Id,
                    IsRead = i % 3 == 0,
                    ReadAt = i % 3 == 0 ? changedAt.AddHours(1) : null,
                });

                var extra = 1 + (i % 2);
                for (var j = 0; j < extra; j++)
                {
                    var lawyer = lawyers[(i + j) % lawyers.Count];
                    if (lawyer.Id == head.Id)
                        continue;
                    alert.Recipients.Add(new HeadAlertRecipient
                    {
                        UserId = lawyer.Id,
                        IsRead = (i + j) % 4 == 0,
                        ReadAt = (i + j) % 4 == 0 ? changedAt.AddHours(1) : null,
                    });
                }

                alerts.Add(alert);
            }

            db.HeadAlerts.AddRange(alerts);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }
}

/// <summary>معرّفات البذر التي تحتاجها الاستعلامات الساخنة (لا قيم عشوائية لاحقة).</summary>
public sealed record SeedManifest(
    IReadOnlyList<int> BranchIds,
    IReadOnlyList<int> PortalEntryIds,
    int PortalDocumentId,
    int HeadAlertUserId,
    int HeadAlertBranchId,
    string TextSearchTerm,
    int DocumentCount,
    DateTime CursorCreatedAt,
    int CursorId);
