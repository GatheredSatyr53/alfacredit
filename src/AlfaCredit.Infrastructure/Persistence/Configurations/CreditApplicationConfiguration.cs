using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class CreditApplicationConfiguration : IEntityTypeConfiguration<CreditApplication>
{
    public void Configure(EntityTypeBuilder<CreditApplication> builder)
    {
        builder.ToTable("credit_applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.Number)
            .HasColumnName("number")
            .HasMaxLength(CreditApplication.NumberMaxLength)
            .IsRequired()
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql($"'AC-' || lpad(nextval('{AppDbContext.ApplicationNumberSequence}')::text, 6, '0')");
        builder.HasIndex(a => a.Number).IsUnique();

        builder.Property(a => a.CompanyId).HasColumnName("company_id").IsRequired();
        builder.HasOne(a => a.Company)
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.ProductId).HasColumnName("product_id").IsRequired();
        builder.HasOne(a => a.Product)
            .WithMany()
            .HasForeignKey(a => a.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.TermMonths).HasColumnName("term_months").IsRequired();
        builder.Property(a => a.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.PurposeDescription).HasColumnName("purpose_description").HasMaxLength(CreditApplication.PurposeDescriptionMaxLength);
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Системный столбец PostgreSQL xmin как токен оптимистической блокировки
        builder.Property(a => a.Version).HasColumnName("xmin").IsRowVersion();

        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(a => new { a.CompanyId, a.CreatedAt });
        builder.HasIndex(a => a.Status);
    }
}
