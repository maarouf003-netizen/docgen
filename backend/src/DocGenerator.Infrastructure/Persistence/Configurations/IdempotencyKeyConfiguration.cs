using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Key).HasMaxLength(100).IsRequired();
        builder.Property(k => k.Operation).HasMaxLength(100).IsRequired();
        builder.Property(k => k.UserId).IsRequired();
        builder.Property(k => k.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ResponseBody).HasColumnType("text");
        builder.Property(k => k.CreatedAt).HasColumnType("datetime2");
        builder.Property(k => k.ExpiresAt).HasColumnType("datetime2");

        // نطاق المفتاح: نفس الثلاثية نية واحدة — السباق المتزامن يصطدم هنا.
        builder.HasIndex(k => new { k.Key, k.Operation, k.UserId }).IsUnique();
        builder.HasIndex(k => k.ExpiresAt);
    }
}
