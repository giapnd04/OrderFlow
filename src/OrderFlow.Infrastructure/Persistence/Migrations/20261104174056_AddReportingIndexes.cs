using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payment_attempts_succeeded_created_at",
                table: "payment_attempts",
                column: "created_at",
                filter: "[status] = N'Succeeded'")
                .Annotation("SqlServer:Include", new[] { "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_attempts_succeeded_order_id",
                table: "payment_attempts",
                column: "order_id",
                filter: "[status] = N'Succeeded'")
                .Annotation("SqlServer:Include", new[] { "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_pending_created_at",
                table: "orders",
                column: "created_at",
                filter: "[status] = N'PendingPayment'")
                .Annotation("SqlServer:Include", new[] { "customer_id", "total_amount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_attempts_succeeded_created_at",
                table: "payment_attempts");

            migrationBuilder.DropIndex(
                name: "ix_payment_attempts_succeeded_order_id",
                table: "payment_attempts");

            migrationBuilder.DropIndex(
                name: "ix_orders_pending_created_at",
                table: "orders");
        }
    }
}
