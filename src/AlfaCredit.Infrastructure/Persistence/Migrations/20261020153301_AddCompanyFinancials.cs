using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaCredit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFinancials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "annual_expenses",
                table: "companies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_profit",
                table: "companies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "annual_expenses",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "net_profit",
                table: "companies");
        }
    }
}
