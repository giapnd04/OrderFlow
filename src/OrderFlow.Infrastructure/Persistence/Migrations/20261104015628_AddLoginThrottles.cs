using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginThrottles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_throttles",
                columns: table => new
                {
                    email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    failed_count = table.Column<int>(type: "int", nullable: false),
                    window_started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    locked_until = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_throttles", x => x.email);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_throttles");
        }
    }
}
