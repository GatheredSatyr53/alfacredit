namespace AlfaCredit.Api.Auth;

/// <summary>Короткие имена claim'ов в JWT. Маппинг на длинные .NET-типы отключён.</summary>
public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string FullName = "full_name";
}
