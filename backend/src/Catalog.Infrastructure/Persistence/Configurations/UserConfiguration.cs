namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", AppDbContext.IdentitySchema);

        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(u => u.Email)
            .HasColumnName("email")
            .HasConversion(
                email => email.Value,
                value => Email.FromTrusted(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        b.HasIndex(u => u.Email)
            .HasDatabaseName("ux_users_email")
            .IsUnique();

        b.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        b.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(UserRole.User)
            .IsRequired();

        b.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at")
            .HasColumnType("timestamp with time zone");

        b.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata
            .FindNavigation(nameof(User.RefreshTokens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
