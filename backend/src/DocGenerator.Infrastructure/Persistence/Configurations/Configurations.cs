using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.LockoutEndUtc);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(150).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
        builder.Property(b => b.Address).HasMaxLength(300);
        builder.Property(b => b.Phone).HasMaxLength(30);
    }
}

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        // الحذف المنطقي: يُخفى المحذوف تلقائياً من كل الاستعلامات
        builder.Property(d => d.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(d => !d.IsDeleted);

        builder.Property(d => d.DocumentType).HasMaxLength(200);
        builder.HasIndex(d => d.DocumentType);

        builder.Property(d => d.BorrowerName).HasMaxLength(100);
        builder.Property(d => d.BorrowerFather).HasMaxLength(100);
        builder.Property(d => d.BorrowerFamily).HasMaxLength(100);
        builder.Property(d => d.BorrowerMother).HasMaxLength(100);
        builder.Property(d => d.BorrowerBirth).HasMaxLength(50);
        builder.Property(d => d.BorrowerRegister).HasMaxLength(100);
        builder.Property(d => d.BorrowerNationalId).HasMaxLength(50);
        builder.Property(d => d.BorrowerAddress).HasMaxLength(300);
        builder.Property(d => d.BorrowerAddressType).HasMaxLength(50);

        builder.Property(d => d.ContractType).HasMaxLength(100);
        builder.Property(d => d.ContractTypeSelector).HasMaxLength(30);
        builder.Property(d => d.ContractNumber).HasMaxLength(100);
        builder.Property(d => d.ContractDate).HasMaxLength(50);
        builder.Property(d => d.InclusionText).HasMaxLength(1000);

        builder.Property(d => d.AmountNumeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.Amount2Numeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.InclusionAmountNumeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.AmountWords).HasMaxLength(1000);
        builder.Property(d => d.Amount2Words).HasMaxLength(1000);
        builder.Property(d => d.InclusionAmountWords).HasMaxLength(1000);
        builder.Property(d => d.Currency).HasMaxLength(50);
        builder.Property(d => d.Currency2).HasMaxLength(50);
        builder.Property(d => d.InclusionCurrency).HasMaxLength(50);

        builder.Property(d => d.Court).HasMaxLength(200);
        builder.Property(d => d.Applicant).HasMaxLength(200);
        builder.Property(d => d.Lawyer).HasMaxLength(200);

        builder.Property(d => d.FileNumber).HasMaxLength(100);
        builder.Property(d => d.FileType).HasMaxLength(100);
        builder.Property(d => d.FileYear).HasMaxLength(50);
        builder.Property(d => d.FileIncoming).HasMaxLength(100);
        builder.Property(d => d.FileIncomingDate).HasMaxLength(50);
        builder.Property(d => d.BranchName).HasMaxLength(150);

        builder.Property(d => d.ExecStatus).HasMaxLength(30);
        builder.Property(d => d.ExecSubStatus).HasMaxLength(30);
        builder.Property(d => d.CollectedAmount).HasColumnType("decimal(20,2)");
        builder.Property(d => d.BaraetNumber).HasMaxLength(100);
        builder.Property(d => d.BaraetDate).HasMaxLength(50);
        builder.Property(d => d.BaraetRegNumber).HasMaxLength(100);
        builder.Property(d => d.BaraetRegDate).HasMaxLength(50);
        builder.Property(d => d.TarithNumber).HasMaxLength(100);
        builder.Property(d => d.TarithDate).HasMaxLength(50);
        builder.Property(d => d.TarithRegNumber).HasMaxLength(100);
        builder.Property(d => d.TarithRegDate).HasMaxLength(50);

        builder.Property(d => d.SeizureDate).HasMaxLength(50);
        builder.Property(d => d.ImmediateActions).HasMaxLength(1000);
        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.Property(d => d.FullData).HasColumnType("text");
        builder.Property(d => d.SearchText).HasMaxLength(1000);
        builder.HasIndex(d => d.SearchText);
        builder.Property(d => d.FilePath).HasMaxLength(500);

        builder.HasIndex(d => d.CreatedAt);
        builder.HasIndex(d => d.BranchId);
        builder.HasIndex(d => d.CreatedById);

        builder.HasOne(d => d.Branch)
            .WithMany(b => b.Documents)
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GuarantorConfiguration : IEntityTypeConfiguration<Guarantor>
{
    public void Configure(EntityTypeBuilder<Guarantor> builder)
    {
        builder.ToTable("Guarantors");
        builder.HasKey(g => g.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(g => g.Document == null || !g.Document.IsDeleted);
        builder.Property(g => g.GuarantorName).HasMaxLength(100);
        builder.Property(g => g.GuarantorFather).HasMaxLength(100);
        builder.Property(g => g.GuarantorFamily).HasMaxLength(100);
        builder.Property(g => g.GuarantorMother).HasMaxLength(100);
        builder.Property(g => g.GuarantorBirth).HasMaxLength(50);
        builder.Property(g => g.GuarantorRegister).HasMaxLength(100);
        builder.Property(g => g.GuarantorNationalId).HasMaxLength(50);
        builder.Property(g => g.GuarantorAddress).HasMaxLength(300);
        builder.Property(g => g.AddressType).HasMaxLength(50);

        builder.HasOne(g => g.Document)
            .WithMany(d => d.Guarantors)
            .HasForeignKey(g => g.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RealEstateConfiguration : IEntityTypeConfiguration<RealEstate>
{
    public void Configure(EntityTypeBuilder<RealEstate> builder)
    {
        builder.ToTable("RealEstates");
        builder.HasKey(r => r.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(r => r.Document == null || !r.Document.IsDeleted);
        builder.Property(r => r.Owner).HasMaxLength(200);
        builder.Property(r => r.Property).HasMaxLength(200);
        builder.Property(r => r.PropertyNumber).HasMaxLength(100);
        builder.Property(r => r.PropertyDistrict).HasMaxLength(200);
        builder.Property(r => r.LandRegistry).HasMaxLength(200);
        builder.Property(r => r.ShareType).HasMaxLength(100);

        builder.HasOne(r => r.Document)
            .WithMany(d => d.RealEstates)
            .HasForeignKey(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserName).HasMaxLength(50);
        builder.Property(a => a.ActionType).HasMaxLength(50);
        builder.Property(a => a.DocumentType).HasMaxLength(200);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.HasIndex(a => a.Timestamp);
    }
}

public class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LoginAttempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(a => a.Key);
    }
}

public class ExecutionActionConfiguration : IEntityTypeConfiguration<ExecutionAction>
{
    public void Configure(EntityTypeBuilder<ExecutionAction> builder)
    {
        builder.ToTable("ExecutionActions");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Type).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Text).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.ActionDate).HasMaxLength(50);
        builder.Property(a => a.ReminderDuration).HasMaxLength(20);
        builder.Property(a => a.ReminderColor).HasMaxLength(20);
        builder.HasIndex(a => a.DocumentId);
        builder.HasIndex(a => a.CreatedAt);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.ExecutionActions)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DocumentRegistrationDateConfiguration : IEntityTypeConfiguration<DocumentRegistrationDate>
{
    public void Configure(EntityTypeBuilder<DocumentRegistrationDate> builder)
    {
        builder.ToTable("DocumentRegistrationDates");
        builder.HasKey(r => r.DocumentId);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(r => r.Document == null || !r.Document.IsDeleted);
        builder.Property(r => r.Date).HasMaxLength(50);

        builder.HasOne(r => r.Document)
            .WithOne(d => d.RegistrationDate)
            .HasForeignKey<DocumentRegistrationDate>(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
