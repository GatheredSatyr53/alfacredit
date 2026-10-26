# Сторонние компоненты backend

Общий перечень поставки, frontend, ML, модели, данные и симуляции: [NaOpite/alfacredit](https://github.com/NaOpite/alfacredit/blob/main/THIRD_PARTY_NOTICES.md). Здесь перечислены зависимости и интеграции backend. Лицензии и copyright из дистрибутивов сохраняются; этот перечень не заменяет их тексты.

## Backend: NuGet-библиотеки и инструменты

Включены прямые зависимости приложения/тестов и основные транзитивные библиотеки JWT и логирования. Лицензии в таблице — идентификаторы из метаданных соответствующих версий NuGet.

| Пакет | Версия | Лицензия |
|---|---|---|
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector/6.0.4) | 6.0.4 | MIT |
| [dotnet-ef](https://www.nuget.org/packages/dotnet-ef/10.0.13) | 10.0.13 | MIT |
| [Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/10.0.13) | 10.0.13 | MIT |
| [Microsoft.AspNetCore.Mvc.Testing](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing/10.0.13) | 10.0.13 | MIT |
| [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.13) | 10.0.13 | MIT |
| [Microsoft.EntityFrameworkCore.Design](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Configuration.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Configuration.Binder](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Binder/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/Microsoft.Extensions.Http/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Identity.Core](https://www.nuget.org/packages/Microsoft.Extensions.Identity.Core/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Options.ConfigurationExtensions](https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions/10.0.13) | 10.0.13 | MIT |
| [Microsoft.Extensions.Options.DataAnnotations](https://www.nuget.org/packages/Microsoft.Extensions.Options.DataAnnotations/10.0.13) | 10.0.13 | MIT |
| [Microsoft.IdentityModel.Abstractions](https://www.nuget.org/packages/Microsoft.IdentityModel.Abstractions/8.19.2) | 8.19.2 | MIT |
| [Microsoft.IdentityModel.JsonWebTokens](https://www.nuget.org/packages/Microsoft.IdentityModel.JsonWebTokens/8.19.2) | 8.19.2 | MIT |
| [Microsoft.IdentityModel.Logging](https://www.nuget.org/packages/Microsoft.IdentityModel.Logging/8.19.2) | 8.19.2 | MIT |
| [Microsoft.IdentityModel.Protocols](https://www.nuget.org/packages/Microsoft.IdentityModel.Protocols/8.19.2) | 8.19.2 | MIT |
| [Microsoft.IdentityModel.Protocols.OpenIdConnect](https://www.nuget.org/packages/Microsoft.IdentityModel.Protocols.OpenIdConnect/8.19.2) | 8.19.2 | MIT |
| [Microsoft.IdentityModel.Tokens](https://www.nuget.org/packages/Microsoft.IdentityModel.Tokens/8.19.2) | 8.19.2 | MIT |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1) | 17.14.1 | MIT |
| [Npgsql](https://www.nuget.org/packages/Npgsql/10.0.3) | 10.0.3 | PostgreSQL |
| [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3) | 10.0.3 | PostgreSQL |
| [Serilog](https://www.nuget.org/packages/Serilog/4.3.0) | 4.3.0 | Apache-2.0 |
| [Serilog.AspNetCore](https://www.nuget.org/packages/Serilog.AspNetCore/10.0.0) | 10.0.0 | Apache-2.0 |
| [Serilog.Extensions.Hosting](https://www.nuget.org/packages/Serilog.Extensions.Hosting/10.0.0) | 10.0.0 | Apache-2.0 |
| [Serilog.Extensions.Logging](https://www.nuget.org/packages/Serilog.Extensions.Logging/10.0.0) | 10.0.0 | Apache-2.0 |
| [Serilog.Formatting.Compact](https://www.nuget.org/packages/Serilog.Formatting.Compact/3.0.0) | 3.0.0 | Apache-2.0 |
| [Serilog.Settings.Configuration](https://www.nuget.org/packages/Serilog.Settings.Configuration/10.0.0) | 10.0.0 | Apache-2.0 |
| [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console/6.1.1) | 6.1.1 | Apache-2.0 |
| [Serilog.Sinks.Debug](https://www.nuget.org/packages/Serilog.Sinks.Debug/3.0.0) | 3.0.0 | Apache-2.0 |
| [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0) | 7.0.0 | Apache-2.0 |
| [Shouldly](https://www.nuget.org/packages/Shouldly/4.3.0) | 4.3.0 | BSD-3-Clause |
| [Swashbuckle.AspNetCore](https://www.nuget.org/packages/Swashbuckle.AspNetCore/10.3.1) | 10.3.1 | MIT |
| [System.IdentityModel.Tokens.Jwt](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt/8.19.2) | 8.19.2 | MIT |
| [Testcontainers.PostgreSql](https://www.nuget.org/packages/Testcontainers.PostgreSql/4.16.0) | 4.16.0 | MIT |
| [xunit](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | Apache-2.0 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.4) | 3.1.4 | Apache-2.0 |

Полные графы транзитивных зависимостей: `src/*/packages.lock.json`, `tests/*/packages.lock.json`. Центральные версии — `Directory.Packages.props`; локальный инструмент EF — `.config/dotnet-tools.json`.

## Платформы

.NET SDK 10.0.402 и ASP.NET Core 10.0.13 в Dockerfile; компоненты .NET — MIT со сторонними notices дистрибутива. PostgreSQL 16.15-alpine — PostgreSQL License плюс лицензии пакетов Alpine. CI использует actions из .github/workflows/ci.yml на их собственных лицензиях.

## Модель и интеграции

В backend включён JSON logreg-synthetic-0.1 команды: `src/AlfaCredit.Core/Scoring/logreg-synthetic-0.1.json`. Он используется для локального inference, без scikit-learn. Python и C# загружают одинаковый артефакт. Модель обучена командой на синтетике; происхождение и ограничения описаны в общем THIRD_PARTY_NOTICES и ML-репозитории. rules-0.3 — отдельные совместимые правила команды.

ЕГРЮЛ/ЕГРИП, ФНС, БКИ, ФССП, ответ менеджера, код и выдача смоделированы. Реальных внешних банковских API, SMS и переводов нет. Совместимый backend Compose использует образ Python-сервиса команды; его зависимости описаны в ML notices. Демо-данные синтетические и создаются seed.
