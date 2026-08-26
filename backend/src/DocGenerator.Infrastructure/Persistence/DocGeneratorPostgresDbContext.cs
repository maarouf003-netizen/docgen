using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// سياق بيانات مخصص لـ Postgres (يُستخدم كهوية لمجموعة هجرات منفصلة عن هجرات SQLite).
/// التسجيل عبر AddDbContext&lt;DocGeneratorDbContext, DocGeneratorPostgresDbContext&gt;
/// يجعل كل التبعيات القائمة تتعامل معه وهو النوع الفعلي عند استخدام Postgres.
/// </summary>
public class DocGeneratorPostgresDbContext : DocGeneratorDbContext
{
    public DocGeneratorPostgresDbContext(DbContextOptions<DocGeneratorPostgresDbContext> options)
        : base((DbContextOptions)options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // «datetime2» نوع خاص بـ SQLite وغير معروف في Postgres؛ نستبدله بنوع زمني صالح
        // أثناء بناء نموذج سياق Postgres فقط، دون المساس بنموذج SQLite وهجراته.
        modelBuilder.Entity<Document>()
            .Property(d => d.FileReceiptDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Document>()
            .Property(d => d.ExecutedDepositDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Document>()
            .Property(d => d.ExecutedExecutionDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Document>()
            .Property(d => d.ForcibleTransferDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Document>()
            .Property(d => d.ReferredAt)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Document>()
            .Property(d => d.RenewalFileReceiptDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Document>()
            .Property(d => d.RenewalDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentRegistrationDate>()
            .Property(r => r.DateParsed)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentOccurrence>()
            .Property(o => o.EventDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentOccurrence>()
            .Property(o => o.ReceiptDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAssignment>()
            .Property(a => a.AssignedAt)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Asset>()
            .Property(a => a.RegistrationDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<Asset>()
            .Property(a => a.LicenseDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentDelegation>()
            .Property(d => d.DelegationDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentDelegation>()
            .Property(d => d.DepositBookDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentDelegation>()
            .Property(d => d.ReturnDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.AppealedDecisionDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.InspectionBookDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.NoticeDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.DepositBookDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.RegistrationDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.DecisionDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.StruckOffDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<DocumentAppeal>()
            .Property(a => a.AssignedAt)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<ReviewLetter>()
            .Property(l => l.LetterDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<ReviewLetterMessage>()
            .Property(m => m.MessageDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<PublicEntityChangeEvent>()
            .Property(e => e.DecreeDate)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<PublicEntityChangeEvent>()
            .Property(e => e.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");
    }
}
