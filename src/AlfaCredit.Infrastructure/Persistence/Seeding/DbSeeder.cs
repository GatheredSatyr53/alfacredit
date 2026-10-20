using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Persistence.Seeding;

/// <summary>Синтетические данные для демонстрации. Идемпотентен: повторный запуск ничего не дублирует.</summary>
public sealed class DbSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Seeding is disabled");
            return;
        }

        var now = clock.GetUtcNow();

        var users = await SeedUsersAsync(now, cancellationToken);
        var products = await SeedProductsAsync(cancellationToken);
        var companies = await SeedCompaniesAsync(users, now, cancellationToken);

        logger.LogInformation(
            "Seeding completed: users +{Users}, products +{Products}, companies +{Companies}",
            users.Added,
            products,
            companies);
    }

    private async Task<(int Added, Dictionary<string, Guid> IdsByLogin)> SeedUsersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.Users
            .Select(u => new { u.Login, u.Id })
            .ToDictionaryAsync(u => u.Login, u => u.Id, cancellationToken);
        var added = 0;

        foreach (var account in SeedData.Accounts)
        {
            if (existing.ContainsKey(account.Login))
            {
                continue;
            }

            var user = User.Create(account.Login, passwordHasher.Hash(options.Value.DemoPassword), account.FullName, account.Role, now);
            db.Users.Add(user);
            existing[user.Login] = user.Id;
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return (added, existing);
    }

    private async Task<int> SeedProductsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await db.CreditProducts.Select(p => p.Code).ToHashSetAsync(cancellationToken);
        var added = 0;

        foreach (var product in SeedData.CreateProducts())
        {
            if (existingCodes.Contains(product.Code))
            {
                continue;
            }

            db.CreditProducts.Add(product);
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }

    private async Task<int> SeedCompaniesAsync((int Added, Dictionary<string, Guid> IdsByLogin) users, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.Companies
            .Include(c => c.Members)
            .ToDictionaryAsync(c => c.Inn, cancellationToken);
        var added = 0;
        var changed = false;

        foreach (var demo in SeedData.Companies)
        {
            if (!users.IdsByLogin.TryGetValue(demo.OwnerLogin, out var ownerId))
            {
                logger.LogWarning("Seed company {Inn} skipped: owner login {Login} not found", demo.Inn, demo.OwnerLogin);
                continue;
            }

            if (!existing.TryGetValue(demo.Inn, out var company))
            {
                company = Company.Create(
                    demo.Inn,
                    demo.Ogrn,
                    demo.Name,
                    demo.LegalForm,
                    demo.Okved,
                    demo.RegisteredAt,
                    demo.Address,
                    demo.HeadName,
                    demo.TaxRegime,
                    demo.AnnualRevenue,
                    demo.AnnualExpenses,
                    demo.NetProfit,
                    demo.MonthlyDebtPayments,
                    demo.EmployeesCount,
                    now);
                db.Companies.Add(company);
                existing[company.Inn] = company;
                added++;
                changed = true;
            }

            if (!company.HasMember(ownerId))
            {
                company.AddMember(ownerId, demo.OwnerRole, now);
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }
}
