using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// حدث تغيير على قيد أو هوية أم في سجل الجهات: يُفهرس حسب القيد والهوية والنوع والزمن.
/// </summary>
public class PublicEntityChangeEventConfiguration : IEntityTypeConfiguration<PublicEntityChangeEvent>
{
    public void Configure(EntityTypeBuilder<PublicEntityChangeEvent> builder)
    {
        builder.ToTable("PublicEntityChangeEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ActionKind).HasMaxLength(30).IsRequired();
        builder.Property(e => e.DecreeKind).HasMaxLength(30);
        builder.Property(e => e.DecreeNumber).HasMaxLength(50);
        builder.Property(e => e.PayloadJson).IsRequired();

        builder.HasIndex(e => e.EntryId);
        builder.HasIndex(e => e.GroupId);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => e.ActionKind);

        // القيد المتأثر: يُفكّ بحذفه (SetNull).
        builder.HasOne(e => e.Entry)
            .WithMany()
            .HasForeignKey(e => e.EntryId)
            .OnDelete(DeleteBehavior.SetNull);

        // الهوية الأم المتأثرة: يُفكّ بحذفها (SetNull).
        builder.HasOne(e => e.Group)
            .WithMany()
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // الفاعل: منع حذف المستخدم إن كان لديه أحداث مسجلة.
        builder.HasOne(e => e.ActorUser)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
