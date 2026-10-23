using AlfaCredit.Core.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class DisbursementConfiguration : IEntityTypeConfiguration<Disbursement>
{
    public void Configure(EntityTypeBuilder<Disbursement> builder)
    {
        builder.ToTable("disbursements");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(d => d.ContractId).HasColumnName("contract_id").IsRequired();
        builder.Property(d => d.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.Account).HasColumnName("account").HasMaxLength(20).IsRequired();
        builder.Property(d => d.PaymentOrderNumber).HasColumnName("payment_order_number").HasMaxLength(32);
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(d => d.CompletedAt).HasColumnName("completed_at");
        builder.Property(d => d.Error).HasColumnName("error").HasMaxLength(Disbursement.ErrorMaxLength);

        builder.HasOne<LoanContract>()
            .WithMany()
            .HasForeignKey(d => d.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.ApplicationId);
    }
}
