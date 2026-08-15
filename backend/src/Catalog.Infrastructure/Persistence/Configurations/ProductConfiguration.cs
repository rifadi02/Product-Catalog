namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", AppDbContext.CatalogSchema, t =>
        {
            t.HasCheckConstraint("ck_products_price_non_negative", "price >= 0");
            t.HasCheckConstraint("ck_products_name_not_blank", "length(btrim(name)) > 0");
        });

        b.HasKey(p => p.Id);

        b.Property(p => p.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        b.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(Product.NameMaxLength)
            .IsRequired();

        b.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(Product.DescriptionMaxLength);

        b.Property(p => p.Price)
            .HasColumnName("price")
            .HasPrecision(18, 2)
            .IsRequired();

        b.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        b.Property(p => p.CreatedBy)
            .HasColumnName("created_by");

        b.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        b.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        b.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        b.HasQueryFilter(p => !p.IsDeleted);

        b.HasIndex(p => p.Price)
            .HasDatabaseName("ix_products_price")
            .HasFilter("is_deleted = false");

        b.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_products_created_at")
            .IsDescending()
            .HasFilter("is_deleted = false");

        b.HasIndex(p => p.CreatedBy)
            .HasDatabaseName("ix_products_created_by")
            .HasFilter("created_by is not null");

    }
}
