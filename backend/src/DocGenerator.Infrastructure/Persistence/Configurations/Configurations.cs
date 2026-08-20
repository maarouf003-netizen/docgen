using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
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
        // الاسم الثلاثي فريد ضمن الفرع؛ المستخدمون بلا فرع (مشرف/مدير) يتفردون فيما بينهم منطقياً.
        builder.HasIndex(u => new { u.Username, u.BranchId }).IsUnique();
        // القيد المنطقي المفقود: فهرس فريد جزئي يمنع تكرار اسم الثلاثي بين المستخدمين بلا فرع
        // (BranchId IS NULL) — SQLite وPostgres يدعمان الفهارس الجزئية بهذه الصيغة.
        builder.HasIndex(u => u.Username)
            .HasFilter("\"BranchId\" IS NULL")
            .IsUnique();
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

        builder.Property(d => d.BorrowerRepresentativeName).HasMaxLength(100);
        builder.Property(d => d.BorrowerRepresentativeFather).HasMaxLength(100);
        builder.Property(d => d.BorrowerRepresentativeFamily).HasMaxLength(100);
        builder.Property(d => d.BorrowerRepresentativeCapacity).HasMaxLength(30);
        builder.Property(d => d.BorrowerRepresentativeAddressType).HasMaxLength(50);
        builder.Property(d => d.BorrowerRepresentativeAddress).HasMaxLength(300);

        builder.Property(d => d.BorrowerNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.Natural);
        builder.Property(d => d.BorrowerRegistrationNumber).HasMaxLength(100);
        builder.Property(d => d.BorrowerRepresentedBy).HasMaxLength(200);

        builder.Property(d => d.ContractType).HasMaxLength(100);
        builder.Property(d => d.ContractTypeSelector).HasMaxLength(30);
        builder.Property(d => d.ContractNumber).HasMaxLength(100);
        builder.Property(d => d.ContractDate).HasMaxLength(50);
        builder.Property(d => d.AnnexType).HasMaxLength(100);
        builder.Property(d => d.AnnexNumber).HasMaxLength(100);
        builder.Property(d => d.AnnexDate).HasMaxLength(50);
        builder.Property(d => d.InclusionText).HasMaxLength(1000);

        builder.Property(d => d.AmountNumeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.Amount2Numeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.Amount3Numeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.InclusionAmountNumeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.InclusionAmount2Numeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.InclusionAmount3Numeric).HasColumnType("decimal(20,2)");
        builder.Property(d => d.AmountWords).HasMaxLength(1000);
        builder.Property(d => d.Amount2Words).HasMaxLength(1000);
        builder.Property(d => d.Amount3Words).HasMaxLength(1000);
        builder.Property(d => d.InclusionAmountWords).HasMaxLength(1000);
        builder.Property(d => d.InclusionAmount2Words).HasMaxLength(1000);
        builder.Property(d => d.InclusionAmount3Words).HasMaxLength(1000);
        builder.Property(d => d.Currency).HasMaxLength(50);
        builder.Property(d => d.Currency2).HasMaxLength(50);
        builder.Property(d => d.Currency3).HasMaxLength(50);
        builder.Property(d => d.InclusionCurrency).HasMaxLength(50);
        builder.Property(d => d.InclusionCurrency2).HasMaxLength(50);
        builder.Property(d => d.InclusionCurrency3).HasMaxLength(50);

        builder.Property(d => d.Court).HasMaxLength(200);
        builder.Property(d => d.Applicant).HasMaxLength(200);
        builder.Property(d => d.Lawyer).HasMaxLength(200);
        builder.Property(d => d.ReferredFromLawyer).HasMaxLength(200);
        builder.Property(d => d.ReferredAt).HasColumnType("datetime2");

        builder.Property(d => d.FileNumber).HasMaxLength(100);
        builder.Property(d => d.FileType).HasMaxLength(100);
        builder.Property(d => d.FileYear).HasMaxLength(50);
        builder.Property(d => d.FileIncoming).HasMaxLength(100);
        builder.Property(d => d.FileIncomingDate).HasMaxLength(50);
        builder.Property(d => d.UnderFilingNumber).HasMaxLength(100);
        builder.Property(d => d.FileArrivalNumber).HasMaxLength(100);
        builder.Property(d => d.FileArrivalDate).HasMaxLength(50);
        builder.Property(d => d.BranchName).HasMaxLength(150);

        builder.Property(d => d.ExecStatus).HasMaxLength(30);
        builder.Property(d => d.ExecSubStatus).HasMaxLength(30);
        builder.Property(d => d.CollectedAmount).HasColumnType("decimal(20,2)");
        builder.Property(d => d.CollectedAmount2).HasColumnType("decimal(20,2)");
        builder.Property(d => d.CollectedAmount3).HasColumnType("decimal(20,2)");
        builder.Property(d => d.CollectedCurrency).HasMaxLength(50);
        builder.Property(d => d.CollectedCurrency2).HasMaxLength(50);
        builder.Property(d => d.CollectedCurrency3).HasMaxLength(50);

        builder.Property(d => d.GeneralEntitySide).HasMaxLength(20).IsRequired();
        builder.HasIndex(d => d.GeneralEntitySide);
        builder.Property(d => d.ExecutedStatus).HasMaxLength(30);
        builder.HasIndex(d => d.ExecutedStatus);
        builder.Property(d => d.ExecutedDescription).HasMaxLength(2000);
        builder.Property(d => d.FileReceiptDate).HasColumnType("datetime2");
        builder.Property(d => d.FileReceiptNumber).HasMaxLength(200);
        builder.Property(d => d.RenewalFileReceiptNumber).HasMaxLength(200);
        builder.Property(d => d.RenewalFileReceiptDate).HasColumnType("datetime2");
        builder.Property(d => d.RenewalFileNumber).HasMaxLength(100);
        builder.Property(d => d.RenewalFileType).HasMaxLength(100);
        builder.Property(d => d.RenewalDate).HasColumnType("datetime2");
        builder.Property(d => d.ExecutedRequiredAmount).HasColumnType("decimal(20,2)");
        builder.Property(d => d.ExecutedRequiredCurrency).HasMaxLength(50);
        builder.Property(d => d.ExecutedRequiredAmount2).HasColumnType("decimal(20,2)");
        builder.Property(d => d.ExecutedRequiredCurrency2).HasMaxLength(50);
        builder.Property(d => d.ExecutedRequiredAmount3).HasColumnType("decimal(20,2)");
        builder.Property(d => d.ExecutedRequiredCurrency3).HasMaxLength(50);
        builder.Property(d => d.ExecutedPaidAmount).HasColumnType("decimal(20,2)");
        builder.Property(d => d.ExecutedPaidCurrency).HasMaxLength(50);
        builder.Property(d => d.ExecutedPaidAmount2).HasColumnType("decimal(20,2)");
        builder.Property(d => d.ExecutedPaidCurrency2).HasMaxLength(50);
        builder.Property(d => d.ExecutedPaidAmount3).HasColumnType("decimal(20,2)");
        builder.Property(d => d.ExecutedPaidCurrency3).HasMaxLength(50);
        builder.Property(d => d.ExecutedDepositDate).HasColumnType("datetime2");
        builder.Property(d => d.ExecutedExecutionDate).HasColumnType("datetime2");
        builder.Property(d => d.BaraetNumber).HasMaxLength(100);
        builder.Property(d => d.BaraetDate).HasMaxLength(50);
        builder.Property(d => d.ForcedExecutionDate).HasMaxLength(50);
        builder.Property(d => d.ForcibleTransferDate).HasColumnType("datetime2");
        builder.Property(d => d.ForcibleTransferNoticeNumber).HasMaxLength(100);
        builder.Property(d => d.BaraetRegNumber).HasMaxLength(100);
        builder.Property(d => d.BaraetRegDate).HasMaxLength(50);
        builder.Property(d => d.TarithNumber).HasMaxLength(100);
        builder.Property(d => d.TarithDate).HasMaxLength(50);
        builder.Property(d => d.TarithRegNumber).HasMaxLength(100);
        builder.Property(d => d.TarithRegDate).HasMaxLength(50);
        builder.Property(d => d.SayerNumber).HasMaxLength(100);
        builder.Property(d => d.SayerDate).HasMaxLength(50);
        builder.Property(d => d.SayerRegNumber).HasMaxLength(100);
        builder.Property(d => d.SayerRegDate).HasMaxLength(50);
        builder.Property(d => d.SoldAssetIds).HasColumnType("text");

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

        // الملف المناب: يرتبط بإنابته بمفتاح أجنبي فريد (كل إنابة تُنشئ ملفًا منابًا واحدًا).
        builder.Property(d => d.SourceDelegationId);
        builder.HasIndex(d => d.SourceDelegationId).IsUnique();
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

        builder.Property(g => g.GuarantorNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.Natural);
        builder.Property(g => g.GuarantorRegistrationNumber).HasMaxLength(100);
        builder.Property(g => g.GuarantorRepresentedBy).HasMaxLength(200);

        builder.Property(g => g.RepresentativeName).HasMaxLength(100);
        builder.Property(g => g.RepresentativeFather).HasMaxLength(100);
        builder.Property(g => g.RepresentativeFamily).HasMaxLength(100);
        builder.Property(g => g.RepresentativeCapacity).HasMaxLength(30);
        builder.Property(g => g.RepresentativeAddressType).HasMaxLength(50);
        builder.Property(g => g.RepresentativeAddress).HasMaxLength(300);

        builder.HasOne(g => g.Document)
            .WithMany(d => d.Guarantors)
            .HasForeignKey(g => g.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.AssetKind).HasMaxLength(50).IsRequired();
        builder.Property(a => a.ShareType).HasMaxLength(100);
        // العقار
        builder.Property(a => a.Property).HasMaxLength(200);
        builder.Property(a => a.PropertyNumber).HasMaxLength(100);
        builder.Property(a => a.PropertyDistrict).HasMaxLength(200);
        builder.Property(a => a.LandRegistry).HasMaxLength(200);
        // المركبة
        builder.Property(a => a.VehicleType).HasMaxLength(200);
        builder.Property(a => a.VehicleClass).HasMaxLength(200);
        builder.Property(a => a.PlateNumber).HasMaxLength(100);
        builder.Property(a => a.VehicleGovernorate).HasMaxLength(100);
        // المتجر المسجل
        builder.Property(a => a.RegisterNumber).HasMaxLength(100);
        builder.Property(a => a.RegistrationDate).HasColumnType("datetime2");
        builder.Property(a => a.ShopGovernorate).HasMaxLength(100);
        builder.Property(a => a.ShopDescription).HasMaxLength(300);
        builder.Property(a => a.ShopLocation).HasMaxLength(300);
        // كفالة الرواتب
        builder.Property(a => a.PublicEntity).HasMaxLength(300);
        // المتجر غير المسجل
        builder.Property(a => a.LicenseNumber).HasMaxLength(100);
        builder.Property(a => a.LicenseDate).HasColumnType("datetime2");
        builder.Property(a => a.LicenseIssuer).HasMaxLength(300);
        // الملاحظات
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.Assets)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssetOwnerConfiguration : IEntityTypeConfiguration<AssetOwner>
{
    public void Configure(EntityTypeBuilder<AssetOwner> builder)
    {
        builder.ToTable("AssetOwners");
        builder.HasKey(o => o.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(o => o.Asset == null || o.Asset.Document == null || !o.Asset.Document.IsDeleted);
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(o => o.AssetId);

        builder.HasOne(o => o.Asset)
            .WithMany(a => a.Owners)
            .HasForeignKey(o => o.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HeirConfiguration : IEntityTypeConfiguration<Heir>
{
    public void Configure(EntityTypeBuilder<Heir> builder)
    {
        builder.ToTable("Heirs");
        builder.HasKey(h => h.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(h => h.Document == null || !h.Document.IsDeleted);
        builder.Property(h => h.HeirName).HasMaxLength(200);
        builder.Property(h => h.HeirFather).HasMaxLength(200);
        builder.Property(h => h.HeirFamily).HasMaxLength(200);
        builder.Property(h => h.HeirCapacity).HasMaxLength(30);
        builder.Property(h => h.AddressType).HasMaxLength(50);
        builder.Property(h => h.HeirAddress).HasMaxLength(300);
        builder.HasIndex(h => h.DocumentId);

        builder.HasOne(h => h.Document)
            .WithMany(d => d.Heirs)
            .HasForeignKey(h => h.DocumentId)
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
        builder.Property(a => a.Text).IsRequired();
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

public class DocumentBaseNumberConfiguration : IEntityTypeConfiguration<DocumentBaseNumber>
{
    public void Configure(EntityTypeBuilder<DocumentBaseNumber> builder)
    {
        builder.ToTable("DocumentBaseNumbers");
        builder.HasKey(b => b.Id);
        // سجل واحد لكل (ملف، سنة): يمنع تكرار رقم أساس لنفس السنة، ويحفظ أرقام السنوات السابقة.
        builder.HasIndex(b => new { b.DocumentId, b.Year }).IsUnique();
        builder.HasIndex(b => b.DocumentId);
        builder.Property(b => b.BaseNumber).HasMaxLength(50).IsRequired();
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب.
        builder.HasQueryFilter(b => b.Document == null || !b.Document.IsDeleted);

        builder.HasOne(b => b.Document)
            .WithMany(d => d.BaseNumbers)
            .HasForeignKey(b => b.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.CreatedBy)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
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
        // التاريخ المحلول تُجرى عليه فلترة الفترات في SQL (يُستبدل في سياق Postgres بنوع زمني صالح).
        builder.Property(r => r.DateParsed).HasColumnType("datetime2");
        builder.HasIndex(r => r.DateParsed);

        builder.HasOne(r => r.Document)
            .WithOne(d => d.RegistrationDate)
            .HasForeignKey<DocumentRegistrationDate>(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HeadAlertConfiguration : IEntityTypeConfiguration<HeadAlert>
{
    public void Configure(EntityTypeBuilder<HeadAlert> builder)
    {
        builder.ToTable("HeadAlerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(a => a.BranchId);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.DelegationId);

        builder.HasOne(a => a.Branch)
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.TargetLawyer)
            .WithMany()
            .HasForeignKey(a => a.TargetLawyerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HeadAlertRecipientConfiguration : IEntityTypeConfiguration<HeadAlertRecipient>
{
    public void Configure(EntityTypeBuilder<HeadAlertRecipient> builder)
    {
        builder.ToTable("HeadAlertRecipients");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.HeadAlertId);
        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.HeadAlert)
            .WithMany(a => a.Recipients)
            .HasForeignKey(r => r.HeadAlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExecutionApplicantConfiguration : IEntityTypeConfiguration<ExecutionApplicant>
{
    public void Configure(EntityTypeBuilder<ExecutionApplicant> builder)
    {
        builder.ToTable("ExecutionApplicants");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.Father).HasMaxLength(100);
        builder.Property(a => a.Family).HasMaxLength(100);
        builder.Property(a => a.LegalRepresentative).HasMaxLength(300);
        builder.Property(a => a.RepresentationType).HasMaxLength(30);
        builder.Property(a => a.DeceasedName).HasMaxLength(100);
        builder.Property(a => a.DeceasedFather).HasMaxLength(100);
        builder.Property(a => a.DeceasedFamily).HasMaxLength(100);
        builder.Property(a => a.RepresentativeName).HasMaxLength(100);
        builder.Property(a => a.RepresentativeFather).HasMaxLength(100);
        builder.Property(a => a.RepresentativeFamily).HasMaxLength(100);
        builder.Property(a => a.RepresentativeCapacity).HasMaxLength(30);
        builder.Property(a => a.RepresentativeLegalRepresentative).HasMaxLength(300);

        builder.Property(a => a.ApplicantNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.Natural);
        builder.Property(a => a.ApplicantRegistrationNumber).HasMaxLength(100);
        builder.Property(a => a.ApplicantRepresentedBy).HasMaxLength(200);
        builder.Property(a => a.ApplicantAddressType).HasMaxLength(50);
        builder.Property(a => a.ApplicantAddress).HasMaxLength(300);
        builder.HasIndex(a => a.DocumentId);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.ExecutionApplicants)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExecutedPublicEntityConfiguration : IEntityTypeConfiguration<ExecutedPublicEntity>
{
    public void Configure(EntityTypeBuilder<ExecutedPublicEntity> builder)
    {
        builder.ToTable("ExecutedPublicEntities");
        builder.HasKey(e => e.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(e => e.Document == null || !e.Document.IsDeleted);
        builder.Property(e => e.EntityName).HasMaxLength(200);
        builder.Property(e => e.EntityBranch).HasMaxLength(200);
        builder.Property(e => e.Governorate).HasMaxLength(100);

        builder.Property(e => e.EntityNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.PublicEntity);
        builder.Property(e => e.RegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.RepresentedBy).HasMaxLength(200);
        builder.Property(e => e.AddressType).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(300);
        builder.HasIndex(e => e.DocumentId);

        builder.HasOne(e => e.Document)
            .WithMany(d => d.ExecutedPublicEntities)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExecutedNaturalPersonConfiguration : IEntityTypeConfiguration<ExecutedNaturalPerson>
{
    public void Configure(EntityTypeBuilder<ExecutedNaturalPerson> builder)
    {
        builder.ToTable("ExecutedNaturalPersons");
        builder.HasKey(p => p.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(p => p.Document == null || !p.Document.IsDeleted);
        builder.Property(p => p.Name).HasMaxLength(100);
        builder.Property(p => p.Father).HasMaxLength(100);
        builder.Property(p => p.Family).HasMaxLength(100);
        builder.Property(p => p.AddressType).HasMaxLength(30);
        builder.Property(p => p.AddressOrRepresentative).HasMaxLength(300);
        builder.Property(p => p.RepresentationType).HasMaxLength(30);
        builder.Property(p => p.DeceasedName).HasMaxLength(100);
        builder.Property(p => p.DeceasedFather).HasMaxLength(100);
        builder.Property(p => p.DeceasedFamily).HasMaxLength(100);
        builder.Property(p => p.RepresentativeName).HasMaxLength(100);
        builder.Property(p => p.RepresentativeFather).HasMaxLength(100);
        builder.Property(p => p.RepresentativeFamily).HasMaxLength(100);
        builder.Property(p => p.RepresentativeCapacity).HasMaxLength(30);
        builder.Property(p => p.RepresentativeAddressType).HasMaxLength(50);
        builder.Property(p => p.RepresentativeAddress).HasMaxLength(300);
        builder.HasIndex(p => p.DocumentId);

        builder.HasOne(p => p.Document)
            .WithMany(d => d.ExecutedNaturalPersons)
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExecutedHeirConfiguration : IEntityTypeConfiguration<ExecutedHeir>
{
    public void Configure(EntityTypeBuilder<ExecutedHeir> builder)
    {
        builder.ToTable("ExecutedHeirs");
        builder.HasKey(h => h.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(h => h.Document == null || !h.Document.IsDeleted);
        builder.Property(h => h.HeirName).HasMaxLength(200);
        builder.Property(h => h.HeirFather).HasMaxLength(200);
        builder.Property(h => h.HeirFamily).HasMaxLength(200);
        builder.Property(h => h.AddressType).HasMaxLength(50);
        builder.Property(h => h.HeirAddress).HasMaxLength(300);
        builder.HasIndex(h => h.DocumentId);
        builder.HasIndex(h => h.ExecutionApplicantId);
        builder.HasIndex(h => h.ExecutedNaturalPersonId);

        builder.HasOne(h => h.Document)
            .WithMany(d => d.ExecutedHeirs)
            .HasForeignKey(h => h.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ExecutionApplicant)
            .WithMany(a => a.Heirs)
            .HasForeignKey(h => h.ExecutionApplicantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ExecutedNaturalPerson)
            .WithMany(p => p.Heirs)
            .HasForeignKey(h => h.ExecutedNaturalPersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentOccurrenceConfiguration : IEntityTypeConfiguration<DocumentOccurrence>
{
    public void Configure(EntityTypeBuilder<DocumentOccurrence> builder)
    {
        builder.ToTable("DocumentOccurrences");
        builder.HasKey(o => o.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(o => o.Document == null || !o.Document.IsDeleted);
        builder.Property(o => o.OccurrenceType).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.OccurrenceType);
        builder.Property(o => o.EventDate).HasColumnType("datetime2");
        builder.HasIndex(o => o.EventDate);
        builder.Property(o => o.FileNumber).HasMaxLength(100);
        builder.Property(o => o.FileType).HasMaxLength(100);
        builder.Property(o => o.Year);
        builder.Property(o => o.ReceiptNumber).HasMaxLength(200);
        builder.Property(o => o.ReceiptDate).HasColumnType("datetime2");
        builder.Property(o => o.Details).HasColumnType("text");
        builder.HasIndex(o => o.DocumentId);

        builder.HasOne(o => o.Document)
            .WithMany(d => d.Occurrences)
            .HasForeignKey(o => o.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApplicantPublicEntityConfiguration : IEntityTypeConfiguration<ApplicantPublicEntity>
{
    public void Configure(EntityTypeBuilder<ApplicantPublicEntity> builder)
    {
        builder.ToTable("ApplicantPublicEntities");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Branch).HasMaxLength(200);
        builder.Property(a => a.Governorate).HasMaxLength(100);
        builder.HasIndex(a => a.DocumentId);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.ApplicantPublicEntities)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentAssignmentConfiguration : IEntityTypeConfiguration<DocumentAssignment>
{
    public void Configure(EntityTypeBuilder<DocumentAssignment> builder)
    {
        builder.ToTable("DocumentAssignments");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Kind).HasMaxLength(20);
        builder.Property(a => a.LawyerName).HasMaxLength(200);
        builder.Property(a => a.AssignedByName).HasMaxLength(200);
        builder.Property(a => a.AssignedAt).HasColumnType("datetime2");
        builder.HasIndex(a => a.DocumentId);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.Assignments)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentDelegationConfiguration : IEntityTypeConfiguration<DocumentDelegation>
{
    public void Configure(EntityTypeBuilder<DocumentDelegation> builder)
    {
        builder.ToTable("DocumentDelegations");
        builder.HasKey(d => d.Id);

        // الإنابة جزء من الملف المنيب: تُخفى عند الحذف المنطقي للمصدر (مطابق لعوامل الأبناء).
        builder.HasQueryFilter(d => d.SourceDocument == null || !d.SourceDocument.IsDeleted);

        // الإنابة جزء من الملف المنيب: تُحذف بحذفه، وتُخفى عند الحذف المنطقي للمصدر.
        builder.HasOne(d => d.SourceDocument)
            .WithMany(doc => doc.Delegations)
            .HasForeignKey(d => d.SourceDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // الملف المناب: كل إنابة تُنشئ ملفًا منابًا واحدًا (1:1) عبر SourceDelegationId على Document.
        builder.HasOne(d => d.TargetDocument)
            .WithOne(doc => doc.SourceDelegation)
            .HasForeignKey<Document>(doc => doc.SourceDelegationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.DelegatedCourt).HasMaxLength(300);
        builder.Property(d => d.DelegationText).HasMaxLength(2000);
        builder.Property(d => d.DepositBookNumber).HasMaxLength(200);
        builder.Property(d => d.DepositBookDate).HasColumnType("datetime2");
        builder.Property(d => d.DelegationDate).HasColumnType("datetime2");
        builder.Property(d => d.ReturnDate).HasColumnType("datetime2");
        builder.Property(d => d.Status).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.SourceDocumentId);

        builder.HasOne(d => d.ExternalBranch)
            .WithMany()
            .HasForeignKey(d => d.ExternalBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.AssignedLawyer)
            .WithMany()
            .HasForeignKey(d => d.AssignedLawyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DelegationAssetConfiguration : IEntityTypeConfiguration<DelegationAsset>
{
    public void Configure(EntityTypeBuilder<DelegationAsset> builder)
    {
        builder.ToTable("DelegationAssets");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssetKind).HasMaxLength(50).IsRequired();
        builder.Property(a => a.AssetLabel).HasMaxLength(300).IsRequired();
        builder.Property(a => a.SalePrice).HasColumnType("decimal(20,2)");
        builder.Property(a => a.SnapshotAdjusted).IsRequired().HasDefaultValue(false);
        builder.HasIndex(a => a.DelegationId);

        // مطابق لعامل الحذف المنطقي للإنابة (وأصلها): تُخفى الأصول التابعة بحذف المصدر.
        builder.HasQueryFilter(a => a.Delegation == null
            || a.Delegation.SourceDocument == null
            || !a.Delegation.SourceDocument.IsDeleted);

        builder.HasOne(a => a.Delegation)
            .WithMany(d => d.Assets)
            .HasForeignKey(a => a.DelegationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
