using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for Actions — Name + IsActive + audit.
// WHY: Active-only unique Name (filtered index); list covering index.
// EXAMPLE: UX_Actions_Name_Active WHERE IsActive = 1.
// ==========================================================================================
public sealed class ActionItemConfiguration : IEntityTypeConfiguration<ActionItem>
{
    public void Configure(EntityTypeBuilder<ActionItem> builder)
    {
        builder.ToTable("Actions", t =>
            t.HasComment("Module: Name, IsActive + audit (T1.26)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasFilter("[IsActive] = CAST(1 AS bit)")
            .HasDatabaseName("UX_Actions_Name_Active");

        builder.HasIndex(x => new { x.IsActive, x.Name })
            .HasDatabaseName("IX_Actions_IsActive_Name");
    }
}
