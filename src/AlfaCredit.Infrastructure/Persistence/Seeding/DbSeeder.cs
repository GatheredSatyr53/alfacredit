using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Persistence.Seeding;

/// <summary>Синтетические данные для демонстрации. Идемпотентен: уже существующие записи не трогает.</summary>
public sealed class DbSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<DbSeeder> logger)
{
    public static readonly IReadOnlyList<DemoAccount> Accounts =
    [
        new("client_good", "Мария Иванова", UserRole.Client, "ООО «Ромашка», стабильный бизнес: ожидается автоодобрение"),
        new("client_medium", "Сергей Кузнецов", UserRole.Client, "ИП Кузнецов, повышенная долговая нагрузка: одобрение с изменёнными условиями"),
        new("client_manual", "Анна Смирнова", UserRole.Client, "ООО «Вектор», крупная сумма: ручное рассмотрение менеджером"),
        new("client_bad", "Дмитрий Волков", UserRole.Client, "ООО «Закат», просрочки и налоговая задолженность: отказ с объяснением"),
        new("client_new", "Елена Петрова", UserRole.Client, "ИП Петрова, бизнесу 4 месяца: подсказка о неподходящих продуктах"),
        new("manager", "Алексей Петров", UserRole.Manager, "Кредитный менеджер: очередь заявок и ручные решения"),
        new("admin", "Ольга Соколова", UserRole.Admin, "Администратор: продукты, параметры политики, аудит"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Seeding is disabled");
            return;
        }

        var existingLogins = await db.Users.Select(u => u.Login).ToHashSetAsync(cancellationToken);
        var added = 0;

        foreach (var account in Accounts)
        {
            if (existingLogins.Contains(account.Login))
            {
                continue;
            }

            var passwordHash = passwordHasher.Hash(options.Value.DemoPassword);
            db.Users.Add(User.Create(account.Login, passwordHash, account.FullName, account.Role, clock.GetUtcNow()));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Seeding completed: {AddedAccounts} demo accounts added, {ExistingAccounts} already existed", added, existingLogins.Count);
    }
}
