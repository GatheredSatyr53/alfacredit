using AlfaCredit.Core.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class CreditProductConfiguration : IEntityTypeConfiguration<CreditProduct>
{
    public void Configure(EntityTypeBuilder<CreditProduct> builder)
    {
        builder.ToTable("credit_products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(CreditProduct.CodeMaxLength).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(CreditProduct.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(CreditProduct.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.MinAmount).HasColumnName("min_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.MaxAmount).HasColumnName("max_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.MinTermMonths).HasColumnName("min_term_months").IsRequired();
        builder.Property(p => p.MaxTermMonths).HasColumnName("max_term_months").IsRequired();
        builder.Property(p => p.BaseRate).HasColumnName("base_rate").HasPrecision(5, 2).IsRequired();
        builder.Property(p => p.MinBusinessAgeMonths).HasColumnName("min_business_age_months").IsRequired();
        builder.Property(p => p.RequiresCollateral).HasColumnName("requires_collateral").IsRequired();
        builder.Property(p => p.AutoApprovalLimit).HasColumnName("auto_approval_limit").HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.SortOrder).HasColumnName("sort_order").IsRequired();

        // Список целей хранится строкой "WorkingCapital,Other": читаемо в базе и не требует отдельной таблицы
        var purposesConverter = new ValueConverter<List<LoanPurpose>, string>(
            purposes => string.Join(',', purposes),
            value => value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => Enum.Parse<LoanPurpose>(item))
                .ToList());

        var purposesComparer = new ValueComparer<List<LoanPurpose>>(
            (left, right) => (left ?? new List<LoanPurpose>()).SequenceEqual(right ?? new List<LoanPurpose>()),
            purposes => purposes.Aggregate(0, (hash, purpose) => HashCode.Combine(hash, purpose)),
            purposes => purposes.ToList());

        builder.Property(p => p.AllowedPurposes)
            .HasColumnName("allowed_purposes")
            .HasConversion(purposesConverter, purposesComparer)
            .HasMaxLength(256)
            .IsRequired();
    }
}
