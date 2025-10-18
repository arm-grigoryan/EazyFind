using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EazyFind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductAlertsSchemeFixe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "product_id",
                table: "product_alert_matches",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "ix_product_alert_matches_product_id",
                table: "product_alert_matches",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_product_alert_matches_products_product_id",
                table: "product_alert_matches",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_product_alert_matches_products_product_id",
                table: "product_alert_matches");

            migrationBuilder.DropIndex(
                name: "ix_product_alert_matches_product_id",
                table: "product_alert_matches");

            migrationBuilder.AlterColumn<string>(
                name: "product_id",
                table: "product_alert_matches",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
