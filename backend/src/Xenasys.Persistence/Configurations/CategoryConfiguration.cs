using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for tenant Categories (BRD §4.2).
// WHY: Unique NameNormalized per company; Code unique per company; keyset indexes.
// EXAMPLE: UX_Categories_CompanyId_NameNormalized.
// ==========================================================================================
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameNormalized).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ImagePath).HasMaxLength(512).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_Categories_CompanyId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Id })
            .HasDatabaseName("IX_Categories_CompanyId_IsActive_Id")
            .IncludeProperties(x => new { x.Name, x.Code });

        builder.HasIndex(x => new { x.CompanyId, x.NameNormalized })
            .IsUnique()
            .HasDatabaseName("UX_Categories_CompanyId_NameNormalized");

        builder.HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique()
            .HasDatabaseName("UX_Categories_CompanyId_Code");

        builder.HasOne(x => x.Company)
            .WithMany(t => t.Categories)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
