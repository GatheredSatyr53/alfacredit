using AlfaCredit.Core.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class ScoringResultConfiguration : IEntityTypeConfiguration<ScoringResult>
{
    public void Configure(EntityTypeBuilder<ScoringResult> builder)
    {
        builder.ToTable("scoring_results");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(s => s.RequestId).HasColumnName("request_id");
        builder.Property(s => s.Score).HasColumnName("score").IsRequired();
        builder.Property(s => s.Grade).HasColumnName("grade").HasConversion<string>().HasMaxLength(2).IsRequired();
        builder.Property(s => s.ProbabilityOfDefault).HasColumnName("probability_of_default").HasPrecision(6, 4);
        builder.Property(s => s.FactorsJson).HasColumnName("factors").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.Warnings)
            .HasColumnName("warnings")
            .HasConversion(StringListConverter.Converter, StringListConverter.Comparer)
            .HasMaxLength(ScoringResult.ListMaxLength)
            .IsRequired();
        builder.Property(s => s.MissingData)
            .HasColumnName("missing_data")
            .HasConversion(StringListConverter.Converter, StringListConverter.Comparer)
            .HasMaxLength(ScoringResult.ListMaxLength)
            .IsRequired();
        builder.Property(s => s.ModelVersion).HasColumnName("model_version").HasMaxLength(ScoringResult.ModelVersionMaxLength).IsRequired();
        builder.Property(s => s.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(s => s.ComputedAt).HasColumnName("computed_at").IsRequired();

        builder.Ignore(s => s.Factors);

        builder.HasIndex(s => s.ApplicationId);
    }
}
