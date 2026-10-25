# Сторонние компоненты, модели, данные и сервисы

Перечень для демонстрационной поставки АльфаКредит. Версии сверены 26 октября 2026 года с lock-файлами, requirements и метаданными NuGet/PyPI. Платные API, покупные банковские данные и внешние веса моделей для основного сценария не используются.

Этот перечень не заменяет LICENSE, copyright и notices, включённые авторами в дистрибутивы пакетов и базовые образы. У системных пакетов внутри контейнеров могут быть разные лицензии. Условия внешних инструментов установки и хостинга определяют их поставщики.

## Собственные компоненты команды

Backend: [GatheredSatyr53/probiinik](https://github.com/GatheredSatyr53/probiinik). Frontend: [exsyyy/alfacredit-frontend](https://github.com/exsyyy/dimadimalohloh). ML: [uandrosova07-arch/scoring](https://github.com/uandrosova07-arch/scoring). Версии frontend и ML, используемые полным демо, закреплены в `scripts/start-demo.ps1`. Собственный код команды и её модель не приписываются лицензиям сторонних библиотек.

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

## Frontend: npm-библиотеки и инструменты

| Пакет | Версия | Лицензия по package-lock.json | Использование |
|---|---|---|---|
| [react](https://www.npmjs.com/package/react/v/19.3.0) | 19.3.0 | MIT | интерфейс |
| [react-dom](https://www.npmjs.com/package/react-dom/v/19.3.0) | 19.3.0 | MIT | интерфейс |
| [react-router-dom](https://www.npmjs.com/package/react-router-dom/v/7.18.4) | 7.18.4 | MIT | интерфейс |
| [@testing-library/jest-dom](https://www.npmjs.com/package/@testing-library/jest-dom/v/7.0.1) | 7.0.1 | MIT | сборка / типы / тесты |
| [@testing-library/react](https://www.npmjs.com/package/@testing-library/react/v/16.3.3) | 16.3.3 | MIT | сборка / типы / тесты |
| [@types/node](https://www.npmjs.com/package/@types/node/v/24.13.5) | 24.13.5 | MIT | сборка / типы / тесты |
| [@types/react](https://www.npmjs.com/package/@types/react/v/19.3.0) | 19.3.0 | MIT | сборка / типы / тесты |
| [@types/react-dom](https://www.npmjs.com/package/@types/react-dom/v/19.3.0) | 19.3.0 | MIT | сборка / типы / тесты |
| [@vitejs/plugin-react](https://www.npmjs.com/package/@vitejs/plugin-react/v/6.1.1) | 6.1.1 | MIT | сборка / типы / тесты |
| [jsdom](https://www.npmjs.com/package/jsdom/v/29.1.1) | 29.1.1 | MIT | сборка / типы / тесты |
| [typescript](https://www.npmjs.com/package/typescript/v/6.0.3) | 6.0.3 | Apache-2.0 | сборка / типы / тесты |
| [vite](https://www.npmjs.com/package/vite/v/8.3.0) | 8.3.0 | MIT | сборка / типы / тесты |
| [vitest](https://www.npmjs.com/package/vitest/v/5.0.3) | 5.0.3 | MIT | сборка / типы / тесты |

Полный граф зависимостей и сведения о лицензиях — в `package-lock.json` frontend. Системные шрифты Arial/Segoe UI запрашиваются CSS браузера; файлы шрифтов в поставку проекта не включены.

## Python: сервис, тесты и обучение

| Пакет | Версия | Лицензия / метаданные PyPI | Использование |
|---|---|---|---|
| [fastapi](https://pypi.org/project/fastapi/0.115.6/) | 0.115.6 | MIT (классификатор PyPI) | сервис |
| [httpx](https://pypi.org/project/httpx/0.28.1/) | 0.28.1 | BSD-3-Clause | тесты |
| [numpy](https://pypi.org/project/numpy/2.5.3/) | 2.5.3 | BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0 | генерация / обучение |
| [pandas](https://pypi.org/project/pandas/3.0.5/) | 3.0.5 | BSD-3-Clause (текст лицензии в метаданных пакета) | генерация / обучение |
| [pydantic](https://pypi.org/project/pydantic/2.10.4/) | 2.10.4 | MIT (классификатор PyPI) | сервис |
| [pytest](https://pypi.org/project/pytest/8.3.4/) | 8.3.4 | MIT | тесты |
| [scikit-learn](https://pypi.org/project/scikit-learn/1.9.1/) | 1.9.1 | BSD-3-Clause | генерация / обучение |
| [uvicorn](https://pypi.org/project/uvicorn/0.34.0/) | 0.34.0 | BSD-3-Clause | сервис |

`uvicorn[standard]` устанавливает дополнительные зависимости, указанные автором этого extra. Их licenses/notices сохраняются в установленных Python-пакетах. Исходные списки: `requirements.txt`, `requirements-dev.txt`, `requirements-train.txt` в ML-репозитории. Runtime загружает JSON и не требует NumPy, pandas или scikit-learn.

## Платформы, контейнеры и CI

| Компонент | Выбранная версия / тег | Где указан | Лицензии |
|---|---|---|---|
| .NET SDK / ASP.NET Core и SignalR | сборка 10.0.402 / runtime 10.0.13; разработка от SDK 10.0.100 | Dockerfile, global.json | компоненты .NET — MIT; notices сторонних компонентов — в дистрибутиве .NET |
| PostgreSQL | postgres:16.15-alpine | docker-compose.yml, compose.demo.yaml | PostgreSQL License; пакеты Alpine имеют собственные лицензии |
| Python | python:3.12-slim в сервисе; обучающий артефакт записывает Python 3.13.17 | ML Dockerfile и JSON модели | PSF и notices дистрибутива; пакеты базовой ОС имеют собственные лицензии |
| Node.js | node:24-alpine, engines >=24 <25 | frontend Dockerfile и package.json | MIT и сторонние notices Node.js; пакеты Alpine имеют собственные лицензии |
| nginx | nginx:stable-alpine | frontend Dockerfile | BSD-2-Clause для nginx; пакеты базового образа имеют собственные лицензии |
| Старый образ скоринга команды | ghcr.io/uandrosova07-arch/scoring:4ce9e8444e24b856a404753b5218cd00fc7bea19 | docker-compose.yml | образ команды на Python с перечисленными библиотеками; используется только совместимым режимом rules-0.3 |
| GitHub Actions | checkout@v4, setup-dotnet@v4, upload-artifact@v4, setup-buildx-action@v3, build-push-action@v6 | .github/workflows/ci.yml | LICENSE в репозиториях соответствующих actions; исполняются в CI |

Полное демо собирает выбранные исходники ML локально и явно использует `logreg-synthetic-0.1`. Отдельный базовый Compose backend сохраняет `rules-0.3`. Теги базовых Node/Python/nginx-образов содержат совместимую ветку, поэтому обновление upstream может изменить системные пакеты; точный состав конкретного образа определяется его digest и лицензиями внутри него.

## Модель и данные

Используется **`logreg-synthetic-0.1` — логистическая регрессия команды**, обученная scikit-learn на синтетических данных, генерируемых `ml/generate.py`. Внешние банковские датасеты и сторонние предобученные веса не используются.

- В обучающем наборе 20 000 синтетических компаний и 29 886 заявок; seed `20261025`. Целевая переменная — синтетический `default_12m`.
- Источники: [описание модели](https://github.com/uandrosova07-arch/scoring/blob/main/docs/model.md), [генератор](https://github.com/uandrosova07-arch/scoring/blob/main/ml/generate.py), [обучение](https://github.com/uandrosova07-arch/scoring/blob/main/ml/train.py).
- JSON: `models/logreg-synthetic-0.1.json` в ML и `src/AlfaCredit.Core/Scoring/logreg-synthetic-0.1.json` в backend. SHA256 одинакового артефакта: `4783344256642d0da396a3312a134311f57e6a131ea72770cbde78c4baf18777`.
- Отпечаток наблюдаемых обучающих данных: `89fba5f6d12ac01c6eb37fb80f1a083c424969a91a6e7dede512220197dc3620`, формат `observed-columns-json-v1`.
- Качество на синтетике не подтверждает качество на реальных заёмщиках. PD — оценка модели на синтетике, а не вероятность одобрения заявки.
- `rules-0.3` — отдельный совместимый набор правил команды, а не сторонняя обученная модель.
- Демо-компании и реквизиты создаются seed backend; финансовые сведения и заявки синтетические. Скриншоты PRODUCT.md получены в этом прототипе. Качественные выводы опроса 10 предпринимателей предоставлены командой; персональные данные участников в поставку не включены.

## Смоделированные интеграции

| Интеграция | Реализация в прототипе |
|---|---|
| ЕГРЮЛ / ЕГРИП, ФНС, БКИ, ФССП | локальные синтетические результаты, детерминированные по профилям, с настраиваемой задержкой и сбоями |
| Рассмотрение менеджером | автоматический демо-ответ с явной отметкой isSimulated; не действие настоящего сотрудника |
| SMS и подписание | демо-код внутри кабинета и подтверждение договора; реальное SMS не отправляется |
| Банковское ядро и выдача | синтетическое платёжное поручение и запись выдачи, без реального перевода |

Рабочий сценарий не обращается к корпоративным API Альфа-Банка. Python-скоринг вызывается по локальной сети Compose; при недоступности сервиса backend рассчитывает **ту же выбранную модель** локально и отмечает источник `RulesFallback`. Интернет нужен при первом скачивании исходников, образов и пакетов.

Справочный список закреплённых npm/NuGet-пакетов и прямых Python-зависимостей: [docs/dependency-inventory.json](docs/dependency-inventory.json).
