# Сторонние компоненты

Все используемые компоненты бесплатны и распространяются под открытыми лицензиями.
Платные подписки, платные API, покупные данные и закрытые корпоративные лицензии не используются.

## Runtime и библиотеки

| Компонент | Версия | Лицензия | Назначение |
|---|---|---|---|
| .NET SDK / ASP.NET Core | 10.0 | MIT | Платформа и веб-фреймворк |
| Microsoft.EntityFrameworkCore, .Design | 10.0.13 | MIT | ORM, миграции |
| Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL License | Провайдер PostgreSQL |
| Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.IdentityModel.* | 10.0.13 | MIT | Проверка и выпуск JWT |
| Microsoft.Extensions.Identity.Core | 10.0.13 | MIT | Хеширование паролей (PBKDF2) |
| Microsoft.Extensions.* (Options, Options.DataAnnotations, Configuration, Logging.Abstractions, Http, Hosting.Abstractions) | 10.0.13 | MIT | Конфигурация, опции, логирование, HttpClient, фоновые службы |
| ASP.NET Core SignalR (в составе shared framework) | 10.0 | MIT | Обновления заявок в реальном времени |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.13 | MIT | Health check базы данных |
| Serilog, Serilog.AspNetCore | 10.0.0 | Apache-2.0 | Структурное логирование |
| Swashbuckle.AspNetCore | 10.3.1 | MIT | OpenAPI-документ и Swagger UI |
| xUnit | 2.9.3 | Apache-2.0 | Тесты |
| Shouldly | 4.3.0 | BSD-2-Clause | Утверждения в тестах |
| Testcontainers.PostgreSql | 4.16.0 | MIT | PostgreSQL в контейнере для интеграционных тестов |
| coverlet.collector | 6.0.4 | MIT | Покрытие тестами |
| dotnet-ef (локальный инструмент) | 10.0.13 | MIT | Генерация миграций |

## Образы и сервисы

| Компонент | Версия | Лицензия | Назначение |
|---|---|---|---|
| mcr.microsoft.com/dotnet/sdk, /aspnet | 10.0.402, 10.0.13 | MIT (образ), Debian (базовая ОС) | Сборка и запуск API |
| postgres (Docker Official Image) | 16.15-alpine | PostgreSQL License | База данных |
| GitHub Actions: actions/checkout, actions/setup-dotnet, actions/upload-artifact, docker/setup-buildx-action, docker/build-push-action | v4, v4, v4, v3, v6 | MIT / Apache-2.0 | CI |

## Модели и датасеты

Модели машинного обучения и внешние датасеты не используются. Все клиентские, компании, заявки и
финансовые показатели синтетические и генерируются кодом приложения (seed при старте).

## Смоделированные интеграции

Решение автономно и не обращается к корпоративным системам Альфа-Банка или внешним API.
Следующие интеграции воспроизводятся локальными симуляторами с синтетическими данными:

| Интеграция | Что симулируется |
|---|---|
| ЕГРЮЛ / ЕГРИП | Существование и статус компании, дата регистрации, ОКВЭД, руководитель, адрес |
| ФНС «Прозрачный бизнес» | Налоговая задолженность, массовый адрес, дисквалификация руководителя, численность, выручка |
| Бюро кредитных историй | Активные обязательства, задолженность, просрочки, платёжная дисциплина |
| ФССП | Исполнительные производства |
| Простая электронная подпись | Подписание договора кодом подтверждения, показанным в интерфейсе |
| Выдача средств | Платёжное поручение и перевод на расчётный счёт без реальных операций |

Симуляторы ЕГРЮЛ, ФНС, БКИ и ФССП реализованы в `src/AlfaCredit.Infrastructure/Simulators`: детерминированные
данные по ИНН, настраиваемая задержка и вероятность сбоя. Внешний сервис скоринга вызывается по контракту
`docs/scoring-contract.md` только в режиме `Scoring__Mode=External`; по умолчанию работает встроенная балльная модель.
