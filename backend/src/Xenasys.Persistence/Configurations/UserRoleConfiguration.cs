using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for UserRoles — multi-role assignments (company via Roles.CompanyId).
// WHY: newSalon UX_UserRoles_User_Role_Active; soft-deactivate instead of hard delete.
// EXAMPLE: Unique (UserId, RoleId) WHERE IsActive = 1.
// ==========================================================================================
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles", t =>
            t.HasComment("Module: User×Role multi-assign (T1.38) — company via Roles.CompanyId"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.RoleId })
            .IsUnique()
            .HasFilter("[IsActive] = CAST(1 AS bit)")
            .HasDatabaseName("UX_UserRoles_User_Role_Active");

        builder.HasIndex(x => new { x.UserId, x.IsActive })
            .HasDatabaseName("IX_UserRoles_UserId_IsActive");
    }
}
