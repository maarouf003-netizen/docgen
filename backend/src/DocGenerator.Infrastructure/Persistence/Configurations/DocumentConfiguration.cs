using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

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
        // نسخة تسريع لفلترة جهة الطالب في البوابة — تُحدَّث عند الحفظ من صفوف الجهات.
        builder.Property(d => d.ApplicantRegistryId);
        builder.HasIndex(d => d.ApplicantRegistryId);
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
        builder.Property(d => d.NoFundsDemandNumber).HasMaxLength(100);
        builder.Property(d => d.NoFundsDemandDate).HasColumnType("datetime2");
        builder.Property(d => d.StartReferralNumber).HasMaxLength(100);
        builder.Property(d => d.StartReferralDate).HasColumnType("datetime2");
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

        // RF-009 (INT-001): وحدانية رقم الأساس الفعّالة (الدائرة + الرقم + النوع + السنة)
        // للملفات الظاهرة فقط — المحذوف منطقيًا خارج القيد (رقمه قابل لإعادة الاستعمال
        // عمدًا، والاستعادة تفحص خدميًا)، والمسودات بلا رقم خارج القيد (FileNumber IS NOT NULL).
        // الصيغة المقتبسة صالحة لـ SQLite وPostgres معًا (نفس اصطلاح ":19-21" القائم).
        // ملاحظة: Court نص حر فيقارن القيد نصًا دقيقًا؛ الخدمة تُطبِّع (Trim) وتفحص.
        builder.HasIndex(d => new { d.Court, d.FileNumber, d.FileType, d.FileYear })
            .HasFilter("NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL")
            .IsUnique();

        // RF-010 (INT-002/INT-012): عدّاد التزامن المتفائل — عمود جديد بلا مساس
        // بالصفوف القائمة (الافتراضي 0 يُملأ تلقائيًا عند التطبيق)؛ يُزاد خدميًا
        // عند كل حفظ محمي ويُفحص في `WHERE` (محمول `SQLite`/`Postgres` بلا توليد مخزني).
        builder.Property(d => d.Version).IsConcurrencyToken();

        builder.HasOne(d => d.Branch)
            .WithMany(b => b.Documents)
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // الملف المناب: يرتبط بإنابته بمفتاح أجنبي فريد (كل إنابة تُنشئ ملفًا منابًا واحدًا).
        builder.Property(d => d.SourceDelegationId);
        builder.HasIndex(d => d.SourceDelegationId).IsUnique();
    }
}
