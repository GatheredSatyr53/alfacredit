using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Api.Contracts.Auth;

/// <summary>Запрос на вход по логину и паролю.</summary>
/// <param name="Login">Логин, например client_good или manager.</param>
/// <param name="Password">Пароль. У демо-учёток общий, см. README.</param>
public sealed record LoginRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Login,
    [Required, StringLength(128, MinimumLength = 1)] string Password);
