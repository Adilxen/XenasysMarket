using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for tenant-scoped Products — catalog FKs + SKU unique + price CHECKs.
// WHY: M3 BRD §4.5 — Category/Sub/Brand/Unit required; Retail >= Invoice; keyset indexes.
// EXAMPLE: CompanyId=1, ProductCode="ACM0070100000001", BrandId=5, UnitId=1.
// ==========================================================================================
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_InvoicePrice_Positive", "[InvoicePrice] > 0");
            t.HasCheckConstraint("CK_Products_RetailPrice_Positive", "[RetailPrice] > 0");
            t.HasCheckConstraint(
                "CK_Products_Retail_Gte_Invoice",
                "[RetailPrice] >= [InvoicePrice]");
            t.HasCheckConstraint("CK_Products_MinStock_NonNegative", "[MinimumStockLevel] >= 0");
            t.HasCheckConstraint("CK_Products_QtyOnHand_NonNegative", "[QuantityOnHand] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ProductDescription).HasMaxLength(1000);
        builder.Property(x => x.Packaging).HasMaxLength(50).IsRequired();
        builder.Property(x => x.UnitOfMeasurement).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProductCode)
            .IsFixedLength()
            .HasMaxLength(16)
            .HasColumnType("char(16)")
            .IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(500);
        builder.Property(x => x.ImagePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.InvoicePrice).HasPrecision(10, 2);
        builder.Property(x => x.RetailPrice).HasPrecision(10, 2);
        builder.Property(x => x.QuantityOnHand).HasDefaultValue(0);

        builder.HasIndex(x => new { x.CompanyId, x.Barcode })
            .IsUnique()
            .HasDatabaseName("UX_Products_CompanyId_Barcode")
            .HasFilter("[Barcode] IS NOT NULL AND [Barcode] <> N''");

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_Products_CompanyId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Id })
            .HasDatabaseName("IX_Products_CompanyId_IsActive_Id")
            .IncludeProperties(x => new
            {
                x.ProductCode,
                x.ProductName,
                x.RetailPrice,
                x.MinimumStockLevel,
                x.QuantityOnHand,
            });

        builder.HasIndex(x => new { x.CompanyId, x.ProductCode })
            .IsUnique()
            .HasDatabaseName("UX_Products_CompanyId_ProductCode")
            .IncludeProperties(x => new
            {
                x.ProductName,
                x.RetailPrice,
                x.MinimumStockLevel,
                x.IsActive,
            });

        builder.HasIndex(x => new { x.CompanyId, x.CategoryId })
            .HasDatabaseName("IX_Products_CompanyId_CategoryId");

        builder.HasIndex(x => new { x.CompanyId, x.BrandId })
            .HasDatabaseName("IX_Products_CompanyId_BrandId");

        builder.HasOne(x => x.Company)
            .WithMany(t => t.Products)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubCategory)
            .WithMany()
            .HasForeignKey(x => x.SubCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Brand)
            .WithMany()
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubsidiaryAccount)
            .WithMany()
            .HasForeignKey(x => x.SubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
