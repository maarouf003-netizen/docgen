using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class AppSuggestionConfiguration : IEntityTypeConfiguration<AppSuggestion>
{
    public void Configure(EntityTypeBuilder<AppSuggestion> builder)
    {
        builder.ToTable("AppSuggestions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Message).HasMaxLength(2000).IsRequired();

        builder.HasIndex(s => s.SenderId);
        builder.HasIndex(s => s.CreatedAt);

        // المرسل: منع حذف حسابه ما دام له اقتراح مسجل.
        builder.HasOne(s => s.Sender)
            .WithMany()
            .HasForeignKey(s => s.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
