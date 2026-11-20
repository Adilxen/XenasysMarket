using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for Roles — CompanyId + module cols first; audit last (T1.28).
// WHY: BRD company-scoped custom roles; unique RoleCode per company (and platform null).
// EXAMPLE: UX_Roles_CompanyId_RoleCode WHERE CompanyId IS NOT NULL.
// ==========================================================================================
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", t =>
            t.HasComment(
                "Module: CompanyId, Name, RoleCode, Description, IsActive + audit (T1.28 BRD company-scoped)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RoleCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.DefaultRoute).HasMaxLength(200);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.RoleCode)
            .IsUnique()
            .HasFilter("[CompanyId] IS NULL")
            .HasDatabaseName("UX_Roles_RoleCode_Platform");

        builder.HasIndex(x => new { x.CompanyId, x.RoleCode })
            .IsUnique()
            .HasFilter("[CompanyId] IS NOT NULL")
            .HasDatabaseName("UX_Roles_CompanyId_RoleCode");

        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Name })
            .HasDatabaseName("IX_Roles_CompanyId_IsActive_Name")
            .IncludeProperties(x => new { x.RoleCode, x.Description });

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
