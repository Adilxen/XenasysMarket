using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenasys.Core.Entities;

namespace Xenasys.Persistence.Configurations;

// ==========================================================================================
// WHAT: EF Core mapping for login Users (credentials + UserNn).
// WHY: Unique UserName and optional Email index support auth lookups and seed identities.
// EXAMPLE: UserName="superadmin", UserNn="00", PasswordHash (T0.12 TEMP plaintext / later BCrypt).
// ==========================================================================================
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    // ==========================================================================================
    // WHAT: Required UserName/PasswordHash/UserNn (char(2)), unique UserName, filtered Email.
    // WHY: Login resolves by UserName; UserNn participates in product/company coding schemes.
    // EXAMPLE: HasIndex(UserName).IsUnique(); Email indexed only when not null.
    // ==========================================================================================
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.UserName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UserNn)
            .IsFixedLength()
            .HasMaxLength(2)
            .HasColumnType("char(2)")
            .IsRequired();

        builder.HasIndex(x => x.UserName).IsUnique();
        builder.HasIndex(x => x.Email).HasFilter("[Email] IS NOT NULL");
    }
}
