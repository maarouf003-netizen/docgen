using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class HeadSuccessionConfiguration : IEntityTypeConfiguration<HeadSuccession>
{
    public void Configure(EntityTypeBuilder<HeadSuccession> builder)
    {
        // سجل قراءة فقط: كتابة بلا تعديل/حذف خدميًا (بلا قيود قاعدة تتجاوز المراجع).
        builder.ToTable("HeadSuccessions", table =>
        {
            table.HasCheckConstraint("CK_HeadSuccessions_Event",
                $"\"Event\" IN ({CheckConstraintLists.InList(HeadSuccessionEventCatalog.ValidEvents)})");
        });
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Event).HasMaxLength(50).IsRequired();
        builder.Property(h => h.At).HasColumnType("datetime2");
        builder.Property(h => h.ActorName).HasMaxLength(200);
        builder.Property(h => h.Reason).HasMaxLength(1000);

        builder.HasIndex(h => h.BranchId);
        builder.HasIndex(h => h.SectionId);
        builder.HasIndex(h => h.UserId);
        builder.HasIndex(h => h.At);

        builder.HasOne(h => h.Branch)
            .WithMany()
            .HasForeignKey(h => h.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Section)
            .WithMany()
            .HasForeignKey(h => h.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
