using AlfaCredit.Core.Domain.Decisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(o => o.DecisionId).HasColumnName("decision_id").IsRequired();
        builder.Property(o => o.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.TermMonths).HasColumnName("term_months").IsRequired();
        builder.Property(o => o.InterestRate).HasColumnName("interest_rate").HasPrecision(5, 2).IsRequired();
        builder.Property(o => o.MonthlyPayment).HasColumnName("monthly_payment").HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(o => o.Response).HasColumnName("response").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(o => o.RespondedAt).HasColumnName("responded_at");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<Decision>()
            .WithMany()
            .HasForeignKey(o => o.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(o => o.IsPending);

        builder.HasIndex(o => o.ApplicationId);
        builder.HasIndex(o => new { o.Response, o.ExpiresAt });
    }
}
