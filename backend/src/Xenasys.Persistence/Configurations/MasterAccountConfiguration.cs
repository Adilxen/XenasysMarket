using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for top-level COA master accounts (Assets, Liabilities, …).
// WHY: Unique Code and Prefix lengths keep chart-of-accounts roots consistent for seeding.
// EXAMPLE: Code="1000", Title="Assets", Prefix="AST".
// ==========================================================================================
public sealed class MasterAccountConfiguration : IEntityTypeConfiguration<MasterAccount>
{
    // ==========================================================================================
    // WHAT: Table, IDENTITY PK, required Code/Title/Prefix, unique Code index.
    // WHY: Master nodes are referenced by ControlAccounts; codes must not collide.
    // EXAMPLE: HasIndex(Code).IsUnique() on "1000".
    // ==========================================================================================
    public void Configure(EntityTypeBuilder<MasterAccount> builder)
    {
        builder.ToTable("MasterAccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(5).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Prefix).IsUnique();
    }
}
