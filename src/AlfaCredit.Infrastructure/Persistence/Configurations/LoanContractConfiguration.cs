using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class LoanContractConfiguration : IEntityTypeConfiguration<LoanContract>
{
    public void Configure(EntityTypeBuilder<LoanContract> builder)
    {
        builder.ToTable("loan_contracts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(c => c.OfferId).HasColumnName("offer_id").IsRequired();

        builder.Property(c => c.Number)
            .HasColumnName("number")
            .HasMaxLength(LoanContract.NumberMaxLength)
            .IsRequired()
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql($"'KD-' || lpad(nextval('{AppDbContext.ContractNumberSequence}')::text, 6, '0')");
        builder.HasIndex(c => c.Number).IsUnique();

        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TermMonths).HasColumnName("term_months").IsRequired();
        builder.Property(c => c.InterestRate).HasColumnName("interest_rate").HasPrecision(5, 2).IsRequired();
        builder.Property(c => c.MonthlyPayment).HasColumnName("monthly_payment").HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalPayment).HasColumnName("total_payment").HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.TotalInterest).HasColumnName("total_interest").HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.LenderName).HasColumnName("lender_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.BorrowerName).HasColumnName("borrower_name").HasMaxLength(300).IsRequired();
        builder.Property(c => c.BorrowerInn).HasColumnName("borrower_inn").HasMaxLength(12).IsRequired();
        builder.Property(c => c.DisbursementAccount).HasColumnName("disbursement_account").HasMaxLength(20).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.SigningDeadline).HasColumnName("signing_deadline").IsRequired();
        builder.Property(c => c.SignedAt).HasColumnName("signed_at");
        builder.Property(c => c.SignedByUserId).HasColumnName("signed_by_user_id");
        builder.Property(c => c.SignedByName).HasColumnName("signed_by_name").HasMaxLength(200);
        builder.Property(c => c.SignatureMethod).HasColumnName("signature_method").HasMaxLength(200);
        builder.Property(c => c.ConfirmationCode).HasColumnName("confirmation_code").HasMaxLength(16);
        builder.Property(c => c.CodeRequestedAt).HasColumnName("code_requested_at");
        builder.Property(c => c.CodeExpiresAt).HasColumnName("code_expires_at");
        builder.Property(c => c.CodeAttempts).HasColumnName("code_attempts").IsRequired();
        builder.Property(c => c.CodeMaxAttempts).HasColumnName("code_max_attempts").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Offer>()
            .WithMany()
            .HasForeignKey(c => c.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.AttemptsLeft);
        builder.Ignore(c => c.IsAwaitingSignature);

        builder.HasIndex(c => c.ApplicationId);
        builder.HasIndex(c => new { c.Status, c.SigningDeadline });
    }
}
