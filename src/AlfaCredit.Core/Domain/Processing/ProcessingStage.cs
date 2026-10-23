namespace AlfaCredit.Core.Domain.Processing;

/// <summary>Этапы автоматического конвейера обработки заявки.</summary>
public enum ProcessingStage
{
    /// <summary>Проверка полноты и корректности данных.</summary>
    Validate = 1,

    /// <summary>Обогащение данными внешних источников: ЕГРЮЛ, ФНС, БКИ, ФССП.</summary>
    Enrich = 2,

    /// <summary>Скоринг.</summary>
    Score = 3,

    /// <summary>Применение кредитной политики и решение.</summary>
    Decide = 4,

    /// <summary>Выдача средств по подписанному договору.</summary>
    Disburse = 5,

    /// <summary>Демо-режим: смоделированный ответ менеджера по заявке в ручном рассмотрении.</summary>
    ManagerDecision = 6,
}

public enum ProcessingJobStatus
{
    Pending = 1,
    Running = 2,
    Done = 3,
    Failed = 4,
}
