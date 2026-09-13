using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReservedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "reserved_quantity",
                table: "products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_reserved_not_exceed_stock",
                table: "products",
                sql: "[reserved_quantity] <= [stock_quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_reserved_quantity",
                table: "products",
                sql: "[reserved_quantity] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_products_reserved_not_exceed_stock",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_reserved_quantity",
                table: "products");

            migrationBuilder.DropColumn(
                name: "reserved_quantity",
                table: "products");
        }
    }
}
