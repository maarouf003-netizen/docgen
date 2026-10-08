using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentDelegationConfiguration : IEntityTypeConfiguration<DocumentDelegation>
{
    public void Configure(EntityTypeBuilder<DocumentDelegation> builder)
    {
        // `PB-002` (`BQ-035`): حالة الإنابة مجمدة قاعديًا من الكتالوج نفسه.
        builder.ToTable("DocumentDelegations", table =>
        {
            table.HasCheckConstraint("CK_DocumentDelegations_Status",
                $"\"Status\" IN ({CheckConstraintLists.InList(DelegationStatusCatalog.ValidStatuses)})");
        });
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
        // `PB-001`: الدائرة المنابة المعيارية (تضبط خدميًا عند التسطير/التعديل).
        builder.Property(d => d.DelegatedCourtNorm).HasMaxLength(300);
        builder.HasIndex(d => d.DelegatedCircuitId);
        // التوجيه لشعبة + الرفض للتصحيح + التزامن المتفائل لمسار الاعتماد (§7).
        builder.HasIndex(d => d.RedirectedToSectionId);
        builder.Property(d => d.RejectReason).HasMaxLength(1000);
        builder.Property(d => d.Version).IsConcurrencyToken();
        builder.Property(d => d.DelegationText).HasMaxLength(2000);
        builder.Property(d => d.DepositBookNumber).HasMaxLength(200);
        builder.Property(d => d.DepositBookDate).HasColumnType("datetime2");
        builder.Property(d => d.DelegationDate).HasColumnType("datetime2");
        builder.Property(d => d.ReturnDate).HasColumnType("datetime2");
        builder.Property(d => d.SaleCoversFullDebt);
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

        // الدائرة المنابة المرجعية (للداخلية فقط).
        builder.HasOne(d => d.DelegatedCircuit)
            .WithMany()
            .HasForeignKey(d => d.DelegatedCircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        // الشعبة الموجَّه لها طلب الخارجية (زر «توجيه للشعبة»).
        builder.HasOne(d => d.RedirectedToSection)
            .WithMany()
            .HasForeignKey(d => d.RedirectedToSectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
