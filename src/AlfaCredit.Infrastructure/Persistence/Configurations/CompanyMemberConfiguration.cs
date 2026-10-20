using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("company_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();

        builder.HasIndex(m => new { m.CompanyId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
