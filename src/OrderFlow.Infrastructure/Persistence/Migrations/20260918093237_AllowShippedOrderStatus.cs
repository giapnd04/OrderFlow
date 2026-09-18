using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowShippedOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "[status] COLLATE Latin1_General_CS_AS IN (N'PendingPayment', N'Paid', N'Confirmed', N'Cancelled', N'Shipped')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "[status] COLLATE Latin1_General_CS_AS IN (N'PendingPayment', N'Paid', N'Confirmed', N'Cancelled')");
        }
    }
}
