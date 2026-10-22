using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.ToTable("processing_jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(j => j.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(j => j.Stage).HasColumnName("stage").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(j => j.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(j => j.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(j => j.MaxAttempts).HasColumnName("max_attempts").IsRequired();
        builder.Property(j => j.NextRunAt).HasColumnName("next_run_at").IsRequired();
        builder.Property(j => j.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(j => j.LastError).HasColumnName("last_error").HasMaxLength(ProcessingJob.ErrorMaxLength);
        builder.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<CreditApplication>()
            .WithMany()
            .HasForeignKey(j => j.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(j => j.CanRetry);

        builder.HasIndex(j => new { j.Status, j.NextRunAt });
        builder.HasIndex(j => j.ApplicationId);
    }
}
