using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for tenant SubCategories (BRD §4.3).
// WHY: Unique NameNormalized within parent Category; Code unique per company.
// EXAMPLE: UX_SubCategories_CategoryId_NameNormalized.
// ==========================================================================================
public sealed class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.ToTable("SubCategories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameNormalized).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ImagePath).HasMaxLength(512);

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_SubCategories_CompanyId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.CategoryId, x.IsActive, x.Id })
            .HasDatabaseName("IX_SubCategories_CompanyId_CategoryId_IsActive_Id")
            .IncludeProperties(x => new { x.Name, x.Code });

        builder.HasIndex(x => new { x.CategoryId, x.NameNormalized })
            .IsUnique()
            .HasDatabaseName("UX_SubCategories_CategoryId_NameNormalized");

        builder.HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("UX_SubCategories_CompanyId_Code");

        builder.HasOne(x => x.Company)
            .WithMany(t => t.SubCategories)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
