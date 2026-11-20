using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for user↔tenant (or platform) role memberships.
// WHY: Enforces one membership per user+company and one NULL-company Super Admin slot.
// EXAMPLE: UserId=5 + CompanyId=NULL → platform Super Admin; CompanyId=1 → tenant role.
// ==========================================================================================
public sealed class UserTenantMembershipConfiguration : IEntityTypeConfiguration<UserTenantMembership>
{
    // ==========================================================================================
    // WHAT: Filtered unique indexes + FKs (User cascade, Company/Role restrict).
    // WHY: Isolates Super Admin (CompanyId NULL) from tenant memberships without overlap.
    // EXAMPLE: Unique UserId WHERE CompanyId IS NULL; unique (UserId, CompanyId) otherwise.
    // ==========================================================================================
    public void Configure(EntityTypeBuilder<UserTenantMembership> builder)
    {
        builder.ToTable("UserTenantMemberships");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        // One tenant membership per user+company
        builder.HasIndex(x => new { x.UserId, x.CompanyId })
            .IsUnique()
            .HasFilter("[CompanyId] IS NOT NULL");

        // At most one platform (NULL company) membership per user — Super Admin isolation
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("[CompanyId] IS NULL");

        builder.HasIndex(x => new { x.CompanyId, x.UserId });
        builder.HasIndex(x => new { x.CompanyId, x.RoleId });

        builder.HasOne(x => x.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(x => x.Role)
            .WithMany(r => r.Memberships)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
