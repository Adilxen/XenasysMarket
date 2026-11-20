using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for COA subsidiary (leaf) accounts per company.
// WHY: Unique codes/account numbers per tenant and links to ControlAccount + Company.
// EXAMPLE: CompanyId=1, SubsidiaryAccountCode="INV-001" under Inventory control.
// ==========================================================================================
public sealed class SubsidiaryAccountConfiguration : IEntityTypeConfiguration<SubsidiaryAccount>
{
// ==========================================================================================
// WHAT: Lengths, unique codes/account numbers, FKs with Restrict.
// WHY: Prevents cross-tenant AccountNo collisions and orphaned COA leaves on parent deletes.
// EXAMPLE: UX on AccountNo + ProductCode as char(16).
// ==========================================================================================
    public void Configure(EntityTypeBuilder<SubsidiaryAccount> builder)
    {
        builder.ToTable("SubsidiaryAccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.SubsidiaryAccountCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SubsidiaryAccountTitle).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AccountNo)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.ProductCode)
            .IsFixedLength()
            .HasMaxLength(16)
            .HasColumnType("char(16)");

        builder.HasIndex(x => new { x.CompanyId, x.Id });
        builder.HasIndex(x => new { x.CompanyId, x.SubsidiaryAccountCode }).IsUnique();
        // Globally unique BRD AccountNo (ACC-yyyy-seq5) — never repeats across companies.
        builder.HasIndex(x => x.AccountNo).IsUnique();

        builder.HasOne(x => x.Company)
            .WithMany(t => t.SubsidiaryAccounts)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ControlAccount)
            .WithMany(c => c.SubsidiaryAccounts)
            .HasForeignKey(x => x.ControlAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.SourceEntityType).HasMaxLength(20);
    }
}
