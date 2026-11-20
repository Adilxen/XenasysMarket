using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for platform Units (system UoM).
// WHY: Code UNIQUE; not tenant-scoped — shared across all companies.
// EXAMPLE: Code="Kg", Name="Kilogram".
// ==========================================================================================
public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Units_Code");

        builder.HasIndex(x => new { x.IsActive, x.Id })
            .HasDatabaseName("IX_Units_IsActive_Id");
    }
}
