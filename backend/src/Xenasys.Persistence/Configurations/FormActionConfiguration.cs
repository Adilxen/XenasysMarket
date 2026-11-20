using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for FormActions — Form×Action catalog with soft-delete unique.
// WHY: newSalon UX_WebFormsAction_WebForm_Action_Active equivalent.
// ==========================================================================================
public sealed class FormActionConfiguration : IEntityTypeConfiguration<FormAction>
{
    public void Configure(EntityTypeBuilder<FormAction> builder)
    {
        builder.ToTable("FormActions", t =>
            t.HasComment("Module: Form×Action catalog (T1.34) — which actions apply to a form"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.Form)
            .WithMany()
            .HasForeignKey(x => x.FormId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Action)
            .WithMany()
            .HasForeignKey(x => x.ActionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.FormId, x.ActionId })
            .IsUnique()
            .HasFilter("[IsActive] = CAST(1 AS bit)")
            .HasDatabaseName("UX_FormActions_Form_Action_Active");

        builder.HasIndex(x => new { x.IsActive, x.FormId })
            .HasDatabaseName("IX_FormActions_IsActive_FormId");
    }
}
