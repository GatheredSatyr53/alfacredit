using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Infrastructure.Persistence.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Создавать демонстрационные учётные записи и справочники при старте.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Общий пароль всех демо-учёток.</summary>
    [Required]
    [MinLength(6)]
    public string DemoPassword { get; set; } = "Demo123!";
}
