using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for PlatformAuditLogs (append-only Super Admin ops trail).
// WHY: BRD §3.1 central audit; PascalCase SQL naming aligned with platform tables.
// EXAMPLE: Index on InsertDate DESC for recent SA activity lists.
// ==========================================================================================
public sealed class PlatformAuditLogConfiguration : IEntityTypeConfiguration<PlatformAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        builder.ToTable("PlatformAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorUserName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Summary)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasMaxLength(4000);

        builder.Property(x => x.InsertDate)
            .IsRequired();

        builder.HasIndex(x => x.InsertDate)
            .HasDatabaseName("IX_PlatformAuditLogs_InsertDate");

        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .HasDatabaseName("IX_PlatformAuditLogs_Entity");
    }
}
