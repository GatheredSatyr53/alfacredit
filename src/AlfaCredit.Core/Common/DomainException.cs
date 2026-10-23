namespace AlfaCredit.Core.Common;

/// <summary>Базовое исключение бизнес-логики. Сообщение предназначено пользователю и пишется по-русски.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Запрос не прошёл проверку: ошибки сгруппированы по полям в camelCase.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : DomainException("Запрос не прошёл проверку.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class NotFoundException(string message) : DomainException(message);

public sealed class ForbiddenException(string message) : DomainException(message);

/// <summary>Два запроса одновременно изменили одну заявку: сохранился первый, второй нужно повторить, перечитав состояние.</summary>
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null) : DomainException(message, innerException);
