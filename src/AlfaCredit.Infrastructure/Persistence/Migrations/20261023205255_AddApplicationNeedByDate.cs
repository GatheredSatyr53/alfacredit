using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaCredit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationNeedByDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "need_by_date",
                table: "credit_applications",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "need_by_date",
                table: "credit_applications");
        }
    }
}
