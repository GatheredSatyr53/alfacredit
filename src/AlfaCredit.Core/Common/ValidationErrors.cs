namespace AlfaCredit.Core.Common;

/// <summary>Накопитель ошибок валидации по полям: позволяет вернуть клиенту все проблемы сразу, а не по одной.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool HasErrors => _errors.Count > 0;

    public ValidationErrors Add(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        if (!messages.Contains(message))
        {
            messages.Add(message);
        }

        return this;
    }

    public IReadOnlyDictionary<string, string[]> ToDictionary() =>
        _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);

    public void ThrowIfAny()
    {
        if (HasErrors)
        {
            throw new ValidationException(ToDictionary());
        }
    }
}
