using AlfaCredit.Core.Abstractions;
using AlfaCredit.Infrastructure.Persistence;
using AlfaCredit.Infrastructure.Persistence.Repositories;
using AlfaCredit.Infrastructure.Persistence.Seeding;
using AlfaCredit.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlfaCredit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICreditProductRepository, CreditProductRepository>();
        services.AddScoped<ICreditApplicationRepository, CreditApplicationRepository>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<DbSeeder>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
