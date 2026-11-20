using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for tenant Control Accounts under platform Master roots.
// WHY: Unique (CompanyId, Code); INV/AP unique per company for Product/Vendor automation.
// EXAMPLE: (CompanyId=12, ControlKind=INVENTORY) at most one active automation node.
// ==========================================================================================
public sealed class ControlAccountConfiguration : IEntityTypeConfiguration<ControlAccount>
{
    public void Configure(EntityTypeBuilder<ControlAccount> builder)
    {
        builder.ToTable("ControlAccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ControlKind).HasMaxLength(40).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.Id });
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.HasIndex(x => x.MasterAccountId);

        builder.HasIndex(x => new { x.CompanyId, x.ControlKind })
            .IsUnique()
            .HasFilter(
                $"[ControlKind] IN (N'{ControlAccountKinds.Inventory}', N'{ControlAccountKinds.AccountsPayable}')");

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MasterAccount)
            .WithMany(m => m.ControlAccounts)
            .HasForeignKey(x => x.MasterAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
