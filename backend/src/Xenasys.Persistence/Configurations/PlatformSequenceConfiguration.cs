using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for PlatformSequences (atomic code counters).
// WHY: Single-row-per-key UPDLOCK path keeps code issuance O(1) under load.
// EXAMPLE: SequenceKey="TENANT_CODE", LastValue=7.
// ==========================================================================================
public sealed class PlatformSequenceConfiguration : IEntityTypeConfiguration<PlatformSequence>
{
    public void Configure(EntityTypeBuilder<PlatformSequence> builder)
    {
        builder.ToTable("PlatformSequences");
        builder.HasKey(x => x.SequenceKey);

        // CAT_CODE_{companyId} / SUB_CODE_{companyId} need headroom beyond TENANT_CODE.
        builder.Property(x => x.SequenceKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.LastValue)
            .IsRequired();
    }
}
