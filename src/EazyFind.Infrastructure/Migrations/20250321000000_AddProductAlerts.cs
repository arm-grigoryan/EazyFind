using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EazyFind.Infrastructure.Migrations
{
    public partial class AddProductAlerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_alerts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    store_keys = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: Array.Empty<string>()),
                    min_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    max_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_checked_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_alert_matches",
                columns: table => new
                {
                    alert_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<string>(type: "text", nullable: false),
                    matched_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_alert_matches", x => new { x.alert_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_product_alert_matches_product_alerts_alert_id",
                        column: x => x.alert_id,
                        principalTable: "product_alerts",
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_alert_matches");

            migrationBuilder.DropTable(
                name: "product_alerts");
        }
    }
}
