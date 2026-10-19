namespace AlfaCredit.IntegrationTests.Infrastructure;

/// <summary>Интеграционные тесты поднимают PostgreSQL в контейнере. Без Docker они пропускаются, а не падают.</summary>
internal static class DockerAvailability
{
    public static bool IsAvailable { get; } = Detect();

    public static string SkipReason => "Docker недоступен: интеграционные тесты требуют контейнер PostgreSQL (Testcontainers).";

    private static bool Detect()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTCONTAINERS_HOST_OVERRIDE")))
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        return File.Exists("/var/run/docker.sock")
            || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "run", "docker.sock"));
    }
}

/// <summary>[Fact], который пропускается без Docker.</summary>
internal sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = DockerAvailability.SkipReason;
        }
    }
}
