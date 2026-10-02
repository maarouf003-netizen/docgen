using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// اقتراح تعديل الجهة الأم لرئيس القسم: فهرس فريد جزئي يمنع تكرار اقتراح معلّق
/// للهوية نفسها من الفرع نفسه (GroupId × CreatedBranchId)، وأنسب تبعية للقيد/الهوية
/// تُفكّ بحذفها، والفاعل/المراجع يُقيَّد حذفهما.
/// </summary>
public class ParentEditSuggestionConfiguration : IEntityTypeConfiguration<ParentEditSuggestion>
{
    public void Configure(EntityTypeBuilder<ParentEditSuggestion> builder)
    {
        builder.ToTable("ParentEditSuggestions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ProposedCanonicalName).HasMaxLength(200);
        builder.Property(s => s.ProposedEntityType).HasMaxLength(30);
        builder.Property(s => s.ProposedCitationFormula).HasMaxLength(20);
        builder.Property(s => s.Reason).HasMaxLength(500).IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.ReviewReason).HasMaxLength(500);

        // منع مكرر معلّق للهوية نفسها من الفرع نفسه، مع السماح بتكرار السبب/الاقتراحات
        // المنتهية (مقبولة/مرفوضة/مسحوبة) من الفرع نفسه.
        builder.HasIndex(s => new { s.GroupId, s.CreatedBranchId })
            .HasFilter("\"Status\" = 'pending'")
            .IsUnique();

        builder.HasIndex(s => s.EntryId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.CreatedBranchId);
        builder.HasIndex(s => s.CreatedById);

        // الهوية الأم المتأثرة: يُفكّ الارتباط بحذفها.
        builder.HasOne(s => s.Group)
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // قيد الجهة الأم المتأثر: يُفكّ الارتباط بحذفه.
        builder.HasOne(s => s.Entry)
            .WithMany()
            .HasForeignKey(s => s.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // صاحب الاقتراح: منع حذف حسابه ما دام له اقتراح مسجل.
        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // المراجع: يُفكّ الارتباط بحذف حسابه دون المساس بالاقتراح.
        builder.HasOne(s => s.ReviewedBy)
            .WithMany()
            .HasForeignKey(s => s.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
