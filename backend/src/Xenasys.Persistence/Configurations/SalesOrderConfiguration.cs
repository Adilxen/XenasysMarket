using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for SalesOrders (Phase-1 ledger).
// WHY: Unique OrderCode per company; keyset + active list indexes.
// EXAMPLE: UX_SalesOrders_CompanyId_OrderCode.
// ==========================================================================================
public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.OrderCode).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(150);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_SalesOrders_CompanyId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Id })
            .HasDatabaseName("IX_SalesOrders_CompanyId_IsActive_Id")
            .IncludeProperties(x => new { x.OrderCode, x.GrossAmount, x.Status, x.OrderDate });

        builder.HasIndex(x => new { x.CompanyId, x.OrderCode })
            .IsUnique()
            .HasDatabaseName("UX_SalesOrders_CompanyId_OrderCode");

        builder.HasOne(x => x.Company)
            .WithMany(t => t.SalesOrders)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(l => l.SalesOrder)
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ==========================================================================================
// WHAT: EF mapping for SalesOrderLines.
// WHY: Product FK + denormalized code/name; company keyset for SA scans.
// ==========================================================================================
public sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.ProductCode).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.SalesOrderId, x.Id })
            .HasDatabaseName("IX_SalesOrderLines_SalesOrderId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_SalesOrderLines_CompanyId_Id");

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
