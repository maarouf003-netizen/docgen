using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ExecutionCircuitConfiguration : IEntityTypeConfiguration<ExecutionCircuit>
{
    public void Configure(EntityTypeBuilder<ExecutionCircuit> builder)
    {
        builder.ToTable("ExecutionCircuits");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameNorm).HasMaxLength(200).IsRequired();
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.Property(c => c.CreatedAt);
        builder.Property(c => c.UpdatedAt);

        // وحدانية الاسم المعياري داخل الفرع (مع التطبيع خدميًا).
        // مقصود بلا IsActive: الاسم محجوز مدى حياة الصف داخل الفرع — التعطيل
        // لا يحرّره، ولا يُعاد استخدامه إلا بالحذف الفيزيائي للدائرة (S6.f).
        builder.HasIndex(c => new { c.BranchId, c.NameNorm }).IsUnique();
        builder.HasIndex(c => c.BranchId);
        builder.HasIndex(c => c.IsActive);
        builder.HasIndex(c => c.SectionId);

        // التزامن المتفائل (مثل Document.Version).
        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.HasOne(c => c.Branch)
            .WithMany()
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // الشعبة المالكة (null = ملك القسم)؛ اتساق الفرعين فحص خدمي (§4.4) —
        // لا قيد بين جدولين في SQLite/Postgres. حذف الشعبة محظور خدميًا مع
        // وجود دوائر، والقيد Restrict ظهرًا.
        builder.HasOne(c => c.Section)
            .WithMany(s => s.Circuits)
            .HasForeignKey(c => c.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
