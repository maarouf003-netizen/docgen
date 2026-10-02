using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

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
