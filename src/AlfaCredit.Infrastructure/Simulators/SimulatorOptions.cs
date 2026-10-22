using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Infrastructure.Simulators;

/// <summary>Поведение симуляторов внешних источников.</summary>
public sealed class SimulatorOptions
{
    public const string SectionName = "Simulators";

    /// <summary>Вероятность, что обращение к источнику завершится ошибкой: демонстрирует ретраи и маршрут в ручное рассмотрение.</summary>
    [Range(0, 1)]
    public double FailureRate { get; set; }

    /// <summary>Имитация сетевой задержки.</summary>
    [Range(0, 10_000)]
    public int LatencyMs { get; set; } = 150;
}
