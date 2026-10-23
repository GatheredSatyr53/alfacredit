using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaCredit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractsAndDisbursements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "contract_number_seq");

            migrationBuilder.CreateTable(
                name: "loan_contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValueSql: "'KD-' || lpad(nextval('contract_number_seq')::text, 6, '0')"),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    term_months = table.Column<int>(type: "integer", nullable: false),
                    interest_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    monthly_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_interest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    lender_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    borrower_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    borrower_inn = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    disbursement_account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signing_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    signature_method = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    confirmation_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    code_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code_attempts = table.Column<int>(type: "integer", nullable: false),
                    code_max_attempts = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_loan_contracts_credit_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loan_contracts_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "disbursements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_order_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disbursements", x => x.id);
                    table.ForeignKey(
                        name: "FK_disbursements_credit_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "credit_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_disbursements_loan_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "loan_contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_application_id",
                table: "disbursements",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_disbursements_contract_id",
                table: "disbursements",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_application_id",
                table: "loan_contracts",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_number",
                table: "loan_contracts",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_offer_id",
                table: "loan_contracts",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_status_signing_deadline",
                table: "loan_contracts",
                columns: new[] { "status", "signing_deadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disbursements");

            migrationBuilder.DropTable(
                name: "loan_contracts");

            migrationBuilder.DropSequence(
                name: "contract_number_seq");
        }
    }
}
