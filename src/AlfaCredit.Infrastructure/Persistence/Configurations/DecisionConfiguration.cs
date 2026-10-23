using AlfaCredit.Core.Domain.Decisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> builder)
    {
        builder.ToTable("decisions");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(d => d.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.DecidedBy).HasColumnName("decided_by").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(d => d.RecommendedType).HasColumnName("recommended_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(18, 2);
        builder.Property(d => d.ApprovedTermMonths).HasColumnName("approved_term_months");
        builder.Property(d => d.InterestRate).HasColumnName("interest_rate").HasPrecision(5, 2);
        builder.Property(d => d.MonthlyPayment).HasColumnName("monthly_payment").HasPrecision(18, 2);
        builder.Property(d => d.Conditions).HasColumnName("conditions").HasMaxLength(Decision.ConditionsMaxLength);
        builder.Property(d => d.ReasonsJson).HasColumnName("reasons").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.IsFinal).HasColumnName("is_final").IsRequired();
        builder.Property(d => d.IsSimulated).HasColumnName("is_simulated").IsRequired().HasDefaultValue(false);
        builder.Property(d => d.DecidedAt).HasColumnName("decided_at").IsRequired();

        // Вычисляемое свойство: список причин живёт в reasons (jsonb), это не навигация
        builder.Ignore(d => d.Reasons);

        builder.HasIndex(d => d.ApplicationId);
    }
}
