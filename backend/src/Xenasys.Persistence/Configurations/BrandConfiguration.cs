using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for tenant Brands (BRD §4.4).
// WHY: Unique NameNormalized per company; keyset indexes for listing.
// EXAMPLE: UX_Brands_CompanyId_NameNormalized.
// ==========================================================================================
public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameNormalized).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_Brands_CompanyId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Id })
            .HasDatabaseName("IX_Brands_CompanyId_IsActive_Id")
            .IncludeProperties(x => new { x.Name });

        builder.HasIndex(x => new { x.CompanyId, x.NameNormalized })
            .IsUnique()
            .HasDatabaseName("UX_Brands_CompanyId_NameNormalized");

        builder.HasOne(x => x.Company)
            .WithMany(t => t.Brands)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
