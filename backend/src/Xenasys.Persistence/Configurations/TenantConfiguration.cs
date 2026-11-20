using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for Tenants — business columns then audit trail at the end.
// WHY: Operators read Name/TenantCode/IsActive first; Insert*/Update* stay last in SSMS.
// EXAMPLE: TenantCode="ACME01", IsActive=true.
// ==========================================================================================
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd().HasColumnOrder(0);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired().HasColumnOrder(1);
        builder.Property(x => x.TenantCode)
            .IsFixedLength()
            .HasMaxLength(6)
            .HasColumnType("char(6)")
            .IsRequired()
            .HasColumnOrder(2);
        builder.Property(x => x.IsActive).IsRequired().HasColumnOrder(3);
        builder.Property(x => x.ContactEmail).HasMaxLength(256).HasColumnOrder(4);
        builder.Property(x => x.ContactPhone).HasMaxLength(30).HasColumnOrder(5);
        builder.Property(x => x.Address).HasMaxLength(500).HasColumnOrder(6);

        builder.Property(x => x.InsertDate).IsRequired().HasColumnOrder(7);
        builder.Property(x => x.InsertUserId).IsRequired().HasColumnOrder(8);
        builder.Property(x => x.UpdateDate).HasColumnOrder(9);
        builder.Property(x => x.UpdateUserId).HasColumnOrder(10);

        builder.HasIndex(x => x.TenantCode)
            .IsUnique()
            .HasDatabaseName("UX_Tenants_TenantCode");

        // Hot list: active companies ordered by name (skips inactive 1CR-scale dead weight).
        builder.HasIndex(x => new { x.IsActive, x.Name })
            .HasDatabaseName("IX_Tenants_IsActive_Name")
            .IncludeProperties(x => new { x.TenantCode, x.ContactEmail });
    }
}
