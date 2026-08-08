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
    public DbSet<RealEstate> RealEstates => Set<RealEstate>();
    public DbSet<RealEstateOwner> RealEstateOwners => Set<RealEstateOwner>();
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
