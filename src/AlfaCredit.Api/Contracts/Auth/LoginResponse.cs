namespace AlfaCredit.Api.Contracts.Auth;

/// <summary>Результат успешного входа.</summary>
/// <param name="AccessToken">JWT для заголовка Authorization: Bearer.</param>
/// <param name="ExpiresAt">Момент истечения токена (UTC).</param>
/// <param name="User">Профиль вошедшего пользователя.</param>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
