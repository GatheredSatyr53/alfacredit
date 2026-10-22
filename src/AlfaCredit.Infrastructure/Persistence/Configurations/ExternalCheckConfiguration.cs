using AlfaCredit.Core.Domain.Checks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class ExternalCheckConfiguration : IEntityTypeConfiguration<ExternalCheck>
{
    public void Configure(EntityTypeBuilder<ExternalCheck> builder)
    {
        builder.ToTable("external_checks");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(c => c.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(c => c.Summary).HasColumnName("summary").HasMaxLength(ExternalCheck.SummaryMaxLength).IsRequired();
        builder.Property(c => c.RiskFlags)
            .HasColumnName("risk_flags")
            .HasConversion(StringListConverter.Converter, StringListConverter.Comparer)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(c => c.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(c => c.Error).HasColumnName("error").HasMaxLength(ExternalCheck.ErrorMaxLength);
        builder.Property(c => c.CheckedAt).HasColumnName("checked_at").IsRequired();

        builder.HasIndex(c => c.ApplicationId);
    }
}
