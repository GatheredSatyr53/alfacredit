using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class FeedItemConfiguration : IEntityTypeConfiguration<FeedItem>
{
    public void Configure(EntityTypeBuilder<FeedItem> builder)
    {
        builder.ToTable("feed_items");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.ApplicationId).HasColumnName("application_id");
        builder.Property(f => f.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(f => f.Title).HasColumnName("title").HasMaxLength(FeedItem.TitleMaxLength).IsRequired();
        builder.Property(f => f.Body).HasColumnName("body").HasMaxLength(FeedItem.BodyMaxLength).IsRequired();
        builder.Property(f => f.Link).HasColumnName("link").HasMaxLength(FeedItem.LinkMaxLength);
        builder.Property(f => f.IsRead).HasColumnName("is_read").IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.ReadAt).HasColumnName("read_at");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CreditApplication>()
            .WithMany()
            .HasForeignKey(f => f.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.UserId, f.IsRead, f.CreatedAt });
    }
}
