using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DelegationAssetReservationConfiguration : IEntityTypeConfiguration<DelegationAssetReservation>
{
    public void Configure(EntityTypeBuilder<DelegationAssetReservation> builder)
    {
        builder.ToTable("DelegationAssetReservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DelegationId).IsRequired();
        builder.Property(r => r.SourceDocumentId).IsRequired();
        builder.Property(r => r.AssetId).IsRequired();
        // نقطة التسلسل (B3): أصل فيزيائي واحد في منيب واحد لا تحجزه إنابتان معًا —
        // إدراجان متزامنان لنفس الزوج يتعارضان هنا حتمًا على أي مزود.
        builder.HasIndex(r => new { r.SourceDocumentId, r.AssetId }).IsUnique();
        builder.HasIndex(r => r.DelegationId);

        // إخفاء تبعًا للحذف المنطقي للمنيب — كاللقطات.
        builder.HasQueryFilter(r => r.Delegation == null
            || r.Delegation.SourceDocument == null
            || !r.Delegation.SourceDocument.IsDeleted);

        builder.HasOne(r => r.Delegation)
            .WithMany()
            .HasForeignKey(r => r.DelegationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
