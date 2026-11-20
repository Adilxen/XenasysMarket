using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF mapping for NotificationOutbox (async alert queue).
// WHY: M5.2 — Pending low-stock rows drained by background worker (not HTTP).
// EXAMPLE: Status=Pending, EventType=LOW_STOCK, ProductId=1006
// ==========================================================================================
public sealed class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationOutbox> builder)
    {
        builder.ToTable("NotificationOutbox");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.EventType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PayloadJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(x => new { x.CompanyId, x.Status, x.Id })
            .HasDatabaseName("IX_NotificationOutbox_CompanyId_Status_Id");

        builder.HasIndex(x => new { x.CompanyId, x.ProductId, x.EventType, x.Status })
            .HasDatabaseName("IX_NotificationOutbox_Company_Product_Event_Status");

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
