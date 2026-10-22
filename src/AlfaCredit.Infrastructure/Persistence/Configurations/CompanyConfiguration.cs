using AlfaCredit.Core.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaCredit.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.Inn).HasColumnName("inn").HasMaxLength(Company.InnMaxLength).IsRequired();
        builder.HasIndex(c => c.Inn).IsUnique();

        builder.Property(c => c.Ogrn).HasColumnName("ogrn").HasMaxLength(Company.OgrnMaxLength).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(Company.NameMaxLength).IsRequired();
        builder.Property(c => c.LegalForm).HasColumnName("legal_form").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.Okved).HasColumnName("okved").HasMaxLength(Company.OkvedMaxLength).IsRequired();
        builder.Property(c => c.RegisteredAt).HasColumnName("registered_at").IsRequired();
        builder.Property(c => c.Address).HasColumnName("address").HasMaxLength(Company.AddressMaxLength).IsRequired();
        builder.Property(c => c.HeadName).HasColumnName("head_name").HasMaxLength(Company.HeadNameMaxLength).IsRequired();
        builder.Property(c => c.TaxRegime).HasColumnName("tax_regime").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.AnnualRevenue).HasColumnName("annual_revenue").HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.AnnualExpenses).HasColumnName("annual_expenses").HasPrecision(18, 2);
        builder.Property(c => c.NetProfit).HasColumnName("net_profit").HasPrecision(18, 2);
        builder.Property(c => c.MonthlyDebtPayments).HasColumnName("monthly_debt_payments").HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.EmployeesCount).HasColumnName("employees_count").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(c => c.RegionCode);

        builder.HasMany(c => c.Members)
            .WithOne()
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
