using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Infrastructure.Persistence.Seeding;

/// <summary>Описание демо-учётки: кто это и какой сценарий на ней показывают.</summary>
public sealed record DemoAccount(string Login, string FullName, UserRole Role, string Scenario);
