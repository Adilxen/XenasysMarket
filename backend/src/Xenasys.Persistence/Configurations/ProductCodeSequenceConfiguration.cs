using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for per-company product code sequence counters.
// WHY: One row per CompanyId safely tracks LastValue for sequential ProductCode generation.
// EXAMPLE: CompanyId=1, LastValue=42 → next product code increments from 43.
// ==========================================================================================
public sealed class ProductCodeSequenceConfiguration : IEntityTypeConfiguration<ProductCodeSequence>
{
    // ==========================================================================================
    // WHAT: PK = CompanyId, FK to Tenant with Restrict delete.
    // WHY: Sequence lifetime is tied to the company; deleting tenant must not cascade silently.
    // EXAMPLE: HasKey(CompanyId) + HasOne(Company).OnDelete(Restrict).
    // ==========================================================================================
    public void Configure(EntityTypeBuilder<ProductCodeSequence> builder)
    {
        builder.ToTable("ProductCodeSequences");
        builder.HasKey(x => x.CompanyId);

        builder.Property(x => x.CompanyId).ValueGeneratedNever();
        builder.Property(x => x.LastValue).IsRequired();

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
