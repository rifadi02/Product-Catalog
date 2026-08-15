namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens", AppDbContext.IdentitySchema);

        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(t => t.UserId).HasColumnName("user_id").IsRequired();

        b.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        b.HasIndex(t => t.TokenHash)
            .HasDatabaseName("ux_refresh_tokens_token_hash")
            .IsUnique();

        b.Property(t => t.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(t => t.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");

        b.Property(t => t.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(64);
        b.Property(t => t.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(128);

        b.HasIndex(t => new { t.UserId, t.ExpiresAt })
            .HasDatabaseName("ix_refresh_tokens_user_id_expires_at");
    }
}
