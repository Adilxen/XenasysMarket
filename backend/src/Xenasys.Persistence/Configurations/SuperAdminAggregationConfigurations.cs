using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

public sealed class SuperAdminInventorySummaryRowConfiguration
    : IEntityTypeConfiguration<SuperAdminInventorySummaryRow>
{
    public void Configure(EntityTypeBuilder<SuperAdminInventorySummaryRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_super_admin_inventory_summary");
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.TenantCode).HasMaxLength(6).IsFixedLength();
    }
}

public sealed class SuperAdminSalesByTenantRowConfiguration
    : IEntityTypeConfiguration<SuperAdminSalesByTenantRow>
{
    public void Configure(EntityTypeBuilder<SuperAdminSalesByTenantRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_super_admin_sales_by_tenant");
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.TenantCode).HasMaxLength(6).IsFixedLength();
        builder.Property(x => x.GrossSales).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(40);
    }
}
