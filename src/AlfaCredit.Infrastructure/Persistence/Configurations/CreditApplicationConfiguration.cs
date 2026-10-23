using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Scoring;
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

        builder.HasMany(a => a.Checks).WithOne().HasForeignKey(c => c.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Checks).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.ScoringResults).WithOne().HasForeignKey(s => s.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.ScoringResults).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Decisions).WithOne().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Decisions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Offers).WithOne().HasForeignKey(o => o.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Offers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Contracts).WithOne().HasForeignKey(c => c.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Contracts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Disbursements).WithOne().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Disbursements).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Вычисляемые свойства агрегата, не колонки и не навигации
        builder.Ignore(a => a.LatestScoring);
        builder.Ignore(a => a.LatestDecision);
        builder.Ignore(a => a.FinalDecision);
        builder.Ignore(a => a.CurrentOffer);
        builder.Ignore(a => a.CurrentContract);
        builder.Ignore(a => a.CurrentDisbursement);
        builder.Ignore(a => a.ChecksSnapshot);
        builder.Ignore(a => a.IsTerminal);

        builder.HasIndex(a => new { a.CompanyId, a.CreatedAt });
        builder.HasIndex(a => a.Status);
    }
}
