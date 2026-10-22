using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaCredit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringDetailsAndJobLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "warnings",
                table: "scoring_results",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<int>(
                name: "duration_ms",
                table: "scoring_results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "missing_data",
                table: "scoring_results",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "request_id",
                table: "scoring_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                table: "processing_jobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "scoring_results");

            migrationBuilder.DropColumn(
                name: "missing_data",
                table: "scoring_results");

            migrationBuilder.DropColumn(
                name: "request_id",
                table: "scoring_results");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "processing_jobs");

            migrationBuilder.AlterColumn<string>(
                name: "warnings",
                table: "scoring_results",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);
        }
    }
}
