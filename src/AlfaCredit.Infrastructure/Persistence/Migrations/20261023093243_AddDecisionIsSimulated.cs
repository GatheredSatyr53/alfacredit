using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaCredit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionIsSimulated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_simulated",
                table: "decisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_simulated",
                table: "decisions");
        }
    }
}
