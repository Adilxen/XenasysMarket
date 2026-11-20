using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for Forms — Name, Path, IsActive + audit.
// WHY: Active-only unique Path (navigation key); list covering index.
// EXAMPLE: UX_Forms_Path_Active WHERE IsActive = 1.
// ==========================================================================================
public sealed class AppFormConfiguration : IEntityTypeConfiguration<AppForm>
{
    public void Configure(EntityTypeBuilder<AppForm> builder)
    {
        builder.ToTable("Forms", t =>
            t.HasComment("Module: Name, Path, IsActive + audit (T1.27) — app navigation catalog"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Path)
            .IsUnique()
            .HasFilter("[IsActive] = CAST(1 AS bit)")
            .HasDatabaseName("UX_Forms_Path_Active");

        builder.HasIndex(x => new { x.IsActive, x.Name })
            .HasDatabaseName("IX_Forms_IsActive_Name")
            .IncludeProperties(x => x.Path);
    }
}
