using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EazyFind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDatabaseSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.type);
                });

            migrationBuilder.CreateTable(
                name: "product_alerts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    store_keys = table.Column<string[]>(type: "text[]", nullable: true, defaultValueSql: "'{}'::text[]"),
                    min_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    max_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    last_checked_utc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    website_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stores", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "product_alert_matches",
                columns: table => new
                {
                    alert_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<string>(type: "text", nullable: false),
                    matched_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_alert_matches", x => new { x.alert_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_product_alert_matches_product_alerts_alert_id",
                        column: x => x.alert_id,
                        principalTable: "product_alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    store_key = table.Column<string>(type: "text", nullable: false),
                    original_category_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_categories_categories_category_type",
                        column: x => x.category_type,
                        principalTable: "categories",
                        principalColumn: "type",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_categories_stores_store_key",
                        column: x => x.store_key,
                        principalTable: "stores",
                        principalColumn: "key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    store_category_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: true),
                    image_url = table.Column<string>(type: "TEXT", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deletion_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_products_store_categories_store_category_id",
                        column: x => x.store_category_id,
                        principalTable: "store_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_alerts_active",
                table: "product_alerts",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_product_alerts_chat",
                table: "product_alerts",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_name",
                table: "products",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_products_store_category_id",
                table: "products",
                column: "store_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_url",
                table: "products",
                column: "url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_categories_category_type",
                table: "store_categories",
                column: "category_type");

            migrationBuilder.CreateIndex(
                name: "ix_store_categories_store_key",
                table: "store_categories",
                column: "store_key");

            migrationBuilder.CreateIndex(
                name: "ix_stores_name",
                table: "stores",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_alert_matches");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "product_alerts");

            migrationBuilder.DropTable(
                name: "store_categories");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "stores");
        }
    }
}
