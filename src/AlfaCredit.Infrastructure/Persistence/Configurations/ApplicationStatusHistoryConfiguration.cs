using AlfaCredit.Core.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("application_status_history");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(32);
        builder.Property(h => h.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(h => h.ActorType).HasColumnName("actor_type").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(h => h.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(h => h.Comment).HasColumnName("comment").HasMaxLength(ApplicationStatusHistory.CommentMaxLength);
        builder.Property(h => h.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(h => new { h.ApplicationId, h.OccurredAt });
    }
}
