namespace AlfaCredit.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Применять миграции при старте приложения. Удобно для демо и docker compose; в проде это отдельный шаг деплоя.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}
