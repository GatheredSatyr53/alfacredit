namespace AlfaCredit.Core.Common;

/// <summary>Кто выполняет действие над заявкой.</summary>
public enum ActorType
{
    /// <summary>Представитель клиента.</summary>
    Client = 1,

    /// <summary>Сотрудник банка (менеджер или администратор).</summary>
    Manager = 2,

    /// <summary>Автоматический конвейер.</summary>
    System = 3,
}
