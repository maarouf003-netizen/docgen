using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public class DocGeneratorDbContext : DbContext
{
    public DocGeneratorDbContext(DbContextOptions<DocGeneratorDbContext> options)
        : base(options)
    {
    }

    /// <summary>للسياقات المشتقة (مثل سياق Postgres)؛ DbContextOptions&lt;T&gt; ثابتة الأنواع.</summary>
    protected DocGeneratorDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Guarantor> Guarantors => Set<Guarantor>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetOwner> AssetOwners => Set<AssetOwner>();
    public DbSet<Heir> Heirs => Set<Heir>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ExecutionAction> ExecutionActions => Set<ExecutionAction>();
    public DbSet<DocumentBaseNumber> BaseNumbers => Set<DocumentBaseNumber>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<DocumentRegistrationDate> DocumentRegistrationDates => Set<DocumentRegistrationDate>();
    public DbSet<HeadAlert> HeadAlerts => Set<HeadAlert>();
    public DbSet<HeadAlertRecipient> HeadAlertRecipients => Set<HeadAlertRecipient>();
    public DbSet<ExecutionApplicant> ExecutionApplicants => Set<ExecutionApplicant>();
    public DbSet<ExecutedPublicEntity> ExecutedPublicEntities => Set<ExecutedPublicEntity>();
    public DbSet<ExecutedNaturalPerson> ExecutedNaturalPersons => Set<ExecutedNaturalPerson>();
    public DbSet<ExecutedHeir> ExecutedHeirs => Set<ExecutedHeir>();
    public DbSet<DocumentOccurrence> DocumentOccurrences => Set<DocumentOccurrence>();
    public DbSet<ApplicantPublicEntity> ApplicantPublicEntities => Set<ApplicantPublicEntity>();
    public DbSet<DocumentAssignment> DocumentAssignments => Set<DocumentAssignment>();
    public DbSet<DocumentDelegation> DocumentDelegations => Set<DocumentDelegation>();
    public DbSet<DelegationAsset> DelegationAssets => Set<DelegationAsset>();
    public DbSet<DocumentAppeal> DocumentAppeals => Set<DocumentAppeal>();
    public DbSet<AppealAction> AppealActions => Set<AppealAction>();
    public DbSet<AppealBaseNumber> AppealBaseNumbers => Set<AppealBaseNumber>();
    public DbSet<ReviewLetter> ReviewLetters => Set<ReviewLetter>();
    public DbSet<ReviewLetterMessage> ReviewLetterMessages => Set<ReviewLetterMessage>();
    public DbSet<DocumentFieldChange> DocumentFieldChanges => Set<DocumentFieldChange>();
    public DbSet<PublicEntityGroup> PublicEntityGroups => Set<PublicEntityGroup>();
    public DbSet<PublicEntity> PublicEntities => Set<PublicEntity>();
    public DbSet<PublicEntityAlias> PublicEntityAliases => Set<PublicEntityAlias>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocGeneratorDbContext).Assembly);

        // كل ملف يُنشأ بواسطة مستخدم حقيقي: العلاقة إلزامية (NOT NULL) لمنع الملفات اليتيمة،
        // مع منع الحذف المتسلسل لمنشئ السجل.
        modelBuilder.Entity<Document>()
            .HasOne(d => d.CreatedBy)
            .WithMany(u => u.Documents)
            .HasForeignKey(d => d.CreatedById)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
