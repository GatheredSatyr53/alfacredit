using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Секрет подписи HMAC-SHA256. Только из переменных окружения или secret store, не из репозитория.</summary>
    [Required]
    [MinLength(32)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "AlfaCredit";

    [Required]
    public string Audience { get; set; } = "AlfaCredit";

    [Range(5, 24 * 60)]
    public int LifetimeMinutes { get; set; } = 480;
}
