namespace AlfaCredit.Core.Common;

/// <summary>Действие недопустимо в текущем состоянии объекта: отвечаем 409.</summary>
public sealed class ConflictException(string message) : DomainException(message);
