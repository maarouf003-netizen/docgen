using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserName).HasMaxLength(50);
        builder.Property(a => a.ActionType).HasMaxLength(50);
        builder.Property(a => a.DocumentType).HasMaxLength(200);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.HasIndex(a => a.Timestamp);
    }
}
