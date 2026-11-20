using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for tenant-scoped Vendors (BRD §4.6).
// WHY: Unique NTN per company; keyset indexes; FK RESTRICT to preserve AP ledger trail.
// EXAMPLE: CompanyId=1, NtnNo="12345678901", SubsidiaryAccountId → AP leaf.
// ==========================================================================================
public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.VendorName).HasMaxLength(100).IsRequired();
        // Optional NTN (documents mask 1234567-8 when set).
        builder.Property(x => x.NtnNo).HasMaxLength(9);
        builder.Property(x => x.Address).HasMaxLength(250).IsRequired();
        builder.Property(x => x.VendorPhone).HasMaxLength(15).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.RepresentativeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RepresentativeNo).HasMaxLength(15).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.Id })
            .HasDatabaseName("IX_Vendors_CompanyId_Id");

        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Id })
            .HasDatabaseName("IX_Vendors_CompanyId_IsActive_Id")
            .IncludeProperties(x => new
            {
                x.VendorName,
                x.NtnNo,
                x.VendorPhone,
                x.Email,
            });

        builder.HasIndex(x => new { x.CompanyId, x.NtnNo })
            .IsUnique()
            .HasFilter("[NtnNo] IS NOT NULL AND [NtnNo] <> N''")
            .HasDatabaseName("UX_Vendors_CompanyId_NtnNo");

        builder.HasOne(x => x.Company)
            .WithMany(t => t.Vendors)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubsidiaryAccount)
            .WithMany()
            .HasForeignKey(x => x.SubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
