using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// §2.3.3. Written by hand: a GIN trigram index cannot be expressed through the fluent API,
    /// and without it <c>WHERE name ILIKE '%term%'</c> is a sequential scan — fine at 60 rows,
    /// a problem at a million.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> <c>CREATE INDEX CONCURRENTLY</c>: EF wraps each migration in a
    /// transaction and <c>CONCURRENTLY</c> cannot run inside one. A zero-downtime build would
    /// need <c>migrationBuilder.Sql(..., suppressTransaction: true)</c>.
    ///
    /// The model is unchanged by this migration, which is why the scaffolded body was empty —
    /// the index lives entirely in raw SQL and therefore not in the model snapshot.
    /// </remarks>
    public partial class AddProductNameTrigramIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pg_trgm is declared on the model (HasPostgresExtension) so migration 1 creates it;
            // this guard keeps the migration safe against a database where it already exists.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // Enables an index-backed plan for   WHERE name ILIKE '%term%'
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_products_name_trgm
                    ON catalog.products
                 USING gin (name gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.ix_products_name_trgm;");
    }
}
