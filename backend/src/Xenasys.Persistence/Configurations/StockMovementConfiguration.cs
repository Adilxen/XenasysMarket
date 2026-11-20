using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for StockMovements ledger.
// WHY: M5.3 — seek by (CompanyId, ProductId, OccurredAt) for history grids.
// EXAMPLE: IX_StockMovements_CompanyId_ProductId_OccurredAt
// ==========================================================================================
public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Reason).HasMaxLength(250);

        builder.HasIndex(x => new { x.CompanyId, x.ProductId, x.OccurredAt })
            .HasDatabaseName("IX_StockMovements_CompanyId_ProductId_OccurredAt");

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_StockMovements_CompanyId_Id");

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
