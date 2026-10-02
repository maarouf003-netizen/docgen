using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class CorrespondenceReceiptConfiguration : IEntityTypeConfiguration<CorrespondenceReceipt>
{
    public void Configure(EntityTypeBuilder<CorrespondenceReceipt> builder)
    {
        builder.ToTable("CorrespondenceReceipts");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserName).HasMaxLength(200).IsRequired();

        // توثيق واحد لكل مطّلع على المراسلة (أول تأكيد هو المرجع).
        builder.HasIndex(r => new { r.CorrespondenceId, r.UserId }).IsUnique();
        builder.HasIndex(r => r.UserId);
    }
}
