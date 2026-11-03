using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSimProviderTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sim");

            migrationBuilder.CreateTable(
                name: "provider_transactions",
                schema: "sim",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    order_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    captured_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_transactions_provider_provider_transaction_id",
                schema: "sim",
                table: "provider_transactions",
                columns: new[] { "provider", "provider_transaction_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_transactions",
                schema: "sim");
        }
    }
}
