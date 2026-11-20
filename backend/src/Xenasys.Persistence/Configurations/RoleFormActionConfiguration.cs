using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for RoleFormActions — Role×Form×Action grants (company via Role).
// WHY: newSalon RoleWebFormsAction; unique active (RoleId, FormId, ActionId).
// ==========================================================================================
public sealed class RoleFormActionConfiguration : IEntityTypeConfiguration<RoleFormAction>
{
    public void Configure(EntityTypeBuilder<RoleFormAction> builder)
    {
        builder.ToTable("RoleFormActions", t =>
            t.HasComment("Module: Role×Form×Action grants (T1.34) — company-scoped via Roles.CompanyId"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Form)
            .WithMany()
            .HasForeignKey(x => x.FormId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Action)
            .WithMany()
            .HasForeignKey(x => x.ActionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RoleId, x.FormId, x.ActionId })
            .IsUnique()
            .HasFilter("[IsActive] = CAST(1 AS bit)")
            .HasDatabaseName("UX_RoleFormActions_Role_Form_Action_Active");

        builder.HasIndex(x => new { x.IsActive, x.RoleId })
            .HasDatabaseName("IX_RoleFormActions_IsActive_RoleId");
    }
}
