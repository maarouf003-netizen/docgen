using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class PersonalReminderConfiguration : IEntityTypeConfiguration<PersonalReminder>
{
    public void Configure(EntityTypeBuilder<PersonalReminder> builder)
    {
        builder.ToTable("PersonalReminders");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title)
            .HasMaxLength(PersonalReminderCatalog.TitleMaxLength)
            .IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(PersonalReminderCatalog.NotesMaxLength);
        builder.Property(r => r.DueDate).HasColumnType("datetime2");
        builder.Property(r => r.Color).HasMaxLength(20);
        builder.Property(r => r.Recurrence).HasMaxLength(20).IsRequired();
        builder.Property(r => r.RecurrenceEnd).HasColumnType("datetime2");
        builder.Property(r => r.CompletedOccurrenceKeys)
            .HasMaxLength(PersonalReminderCatalog.CompletedKeysMaxLength)
            .IsRequired();

        // استعلام التقويم والقوائم دومًا بمالك التذكير (ومرتبًا بتاريخ الاستحقاق).
        builder.HasIndex(r => r.LawyerId);
        builder.HasIndex(r => new { r.LawyerId, r.DueDate });
        builder.HasIndex(r => r.IsArchived);

        // مالك التذكير: منع حذف حسابه ما دام له تذكير مسجل (لا حذف صامت للسجل).
        builder.HasOne(r => r.Lawyer)
            .WithMany()
            .HasForeignKey(r => r.LawyerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
