using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// الاستئنافات على الملف التنفيذي: أبناء الملف تُحذف بحذفه وتُخفى عند حذفه منطقيًا،
/// مع فهرسة الحالة والاتجاه والمحامي المسند إليها لتصفيات صفحة «الاستئنافات».
/// </summary>
public class DocumentAppealConfiguration : IEntityTypeConfiguration<DocumentAppeal>
{
    public void Configure(EntityTypeBuilder<DocumentAppeal> builder)
    {
        // `PB-002`: حالة الإحالة مجمدة قاعديًا من الكتالوج نفسه.
        builder.ToTable("DocumentAppeals", table =>
        {
            table.HasCheckConstraint("CK_DocumentAppeals_Status",
                $"\"Status\" IN ({CheckConstraintLists.InList(AppealStatusCatalog.ValidStatuses)})");
            table.HasCheckConstraint("CK_DocumentAppeals_ForwardState",
                $"\"ForwardState\" IN ({CheckConstraintLists.InList(AppealForwardCatalog.ValidStates)})");
        });
        builder.HasKey(a => a.Id);

        // عامل مطابق لقفل الحذف المنطقي للملف الأب.
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);

        // الاستئناف جزء من الملف: يُحذف بحذفه فيزيائيًا ويُخفى عند الحذف المنطقي.
        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Direction).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.Direction);
        builder.Property(a => a.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.Status);
        builder.Property(a => a.AppealTypeLabel).HasMaxLength(100);

        builder.Property(a => a.AppellantsJson).HasColumnType("text").IsRequired();
        builder.Property(a => a.AppelleesJson).HasColumnType("text").IsRequired();

        builder.Property(a => a.AppealedDecisionText).HasMaxLength(2000);
        builder.Property(a => a.AppealedDecisionSummary).HasMaxLength(2000);
        builder.Property(a => a.AppealedDecisionDate).HasColumnType("datetime2");
        builder.Property(a => a.InspectionBookNumber).HasMaxLength(200);
        builder.Property(a => a.InspectionBookDate).HasColumnType("datetime2");
        builder.Property(a => a.GroundsSummary).HasMaxLength(2000);

        builder.Property(a => a.NoticeNumber).HasMaxLength(200);
        builder.Property(a => a.NoticeDate).HasColumnType("datetime2");
        builder.Property(a => a.AppellateCourt).HasMaxLength(300);
        builder.Property(a => a.AppealBaseNumber).HasMaxLength(100);
        builder.Property(a => a.AppealYear).HasMaxLength(50);
        builder.Property(a => a.DepositBookNumber).HasMaxLength(200);
        builder.Property(a => a.DepositBookDate).HasColumnType("datetime2");
        builder.Property(a => a.DefenseOpinion).HasMaxLength(2000);

        builder.Property(a => a.RegistrationDate).HasColumnType("datetime2");

        builder.Property(a => a.DecisionNumber).HasMaxLength(100);
        builder.Property(a => a.DecisionDate).HasColumnType("datetime2");
        builder.Property(a => a.DecisionRuling).HasMaxLength(2000);
        builder.Property(a => a.Outcome).HasMaxLength(20);

        builder.Property(a => a.StruckOffDate).HasColumnType("datetime2");
        builder.Property(a => a.StruckOffDecisionNumber).HasMaxLength(100);

        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasIndex(a => a.DocumentId);
        builder.HasIndex(a => a.AssignedLawyerId);
        builder.HasIndex(a => a.CreatedAt);

        // حقول الإحالة (شعبة → قسم): الافتراضي ملك النطاق؛ التزامن المتفائل
        // لمسارات الإسناد الثلاثة (قرار §2 + §6.7).
        builder.Property(a => a.ForwardState).HasMaxLength(20).IsRequired().HasDefaultValue(AppealForwardCatalog.Owned);
        builder.HasIndex(a => a.ForwardState);
        builder.Property(a => a.ForwardedAt).HasColumnType("datetime2");
        builder.Property(a => a.ForwardReason).HasMaxLength(1000);
        builder.HasIndex(a => a.ForwardedById);
        builder.Property(a => a.Version).IsConcurrencyToken();

        builder.HasOne(a => a.AssignedLawyer)
            .WithMany()
            .HasForeignKey(a => a.AssignedLawyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
