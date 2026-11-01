using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireProviderTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows written before this rule existed would make the CHECK fail to apply.
            // Give them a recognisable placeholder so they stay unique and auditable.
            migrationBuilder.Sql(
                "UPDATE payment_attempts SET provider_transaction_id = CONCAT(N'legacy-', id) " +
                "WHERE status = N'Succeeded' AND provider_transaction_id IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_attempts_succeeded_requires_provider_tx",
                table: "payment_attempts",
                sql: "[status] <> N'Succeeded' OR [provider_transaction_id] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_attempts_succeeded_requires_provider_tx",
                table: "payment_attempts");
        }
    }
}
