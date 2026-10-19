# AlfaCredit: цифровой кредитный конвейер для малого и микробизнеса

Прототип для кейса «Умный кредитный кабинет предпринимателя» (хакатон Альфа-Банка).
Решение объединяет клиентский путь (выбор продукта, заявка, документы, статус, оффер, подписание,
выдача) и внутреннюю обработку банка (проверки, скоринг, кредитное решение, работа менеджера)
в один управляемый процесс.

> Текущее состояние: **этап 0, скелет backend**. Готовы аутентификация с ролями, PostgreSQL с
> миграциями и демо-данными, Swagger, health checks, Docker Compose и CI.
> План следующих этапов: [docs/backend-plan.md](docs/backend-plan.md).

## Архитектура

| Слой | Проект | Содержание |
|---|---|---|
| Web API | `src/AlfaCredit.Api` | ASP.NET Core MVC: контроллеры, JWT, Swagger, health checks, composition root |
| Домен и сценарии | `src/AlfaCredit.Core` | Сущности, state machine заявки, скоринг, кредитная политика, порты (интерфейсы). Без зависимостей от EF и ASP.NET |
| Инфраструктура | `src/AlfaCredit.Infrastructure` | EF Core + PostgreSQL, миграции, seed-данные, хеширование паролей, симуляторы внешних сервисов |
| Тесты | `tests/AlfaCredit.UnitTests`, `tests/AlfaCredit.IntegrationTests` | xUnit; интеграционные тесты поднимают PostgreSQL через Testcontainers |

Зависимости направлены внутрь: `Api → Core ← Infrastructure`.
Стек: .NET 10 LTS, ASP.NET Core MVC, EF Core 10, PostgreSQL 16, Serilog, Swashbuckle, xUnit, Docker.

## Требования к окружению

Для запуска: Docker 24+ с Compose v2. Больше ничего ставить не нужно.

Для разработки: .NET SDK 10.0.100 или новее (версия зафиксирована в `global.json`),
PostgreSQL 16 локально или в контейнере.

## Запуск одной командой

```bash
docker compose up --build
```

После старта:

| Что | Адрес |
|---|---|
| Swagger UI | http://localhost:8080/swagger |
| OpenAPI-документ | http://localhost:8080/swagger/v1/swagger.json |
| Liveness | http://localhost:8080/health/live |
| Readiness (проверяет базу) | http://localhost:8080/health/ready |

Миграции и демо-данные применяются автоматически при старте API.
Остановить и удалить данные: `docker compose down -v`.

## Запуск для разработки

```bash
docker compose up -d db                      # только PostgreSQL
dotnet restore
dotnet run --project src/AlfaCredit.Api      # http://localhost:5080/swagger
dotnet test                                  # unit + интеграционные (последние требуют Docker, иначе пропускаются)
```

Новая миграция после изменения модели:

```bash
dotnet ef migrations add <Name> --project src/AlfaCredit.Infrastructure --startup-project src/AlfaCredit.Api --output-dir Persistence/Migrations
```

`dotnet-ef` подключён как локальный инструмент (`.config/dotnet-tools.json`), при первом запуске выполните `dotnet tool restore`.

## Конфигурация и переменные окружения

Compose читает `.env` из корня (образец: `.env.example`). У всех переменных есть значения по умолчанию,
для демо `.env` не обязателен.

| Переменная (.env) | Ключ конфигурации API | По умолчанию | Назначение |
|---|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `ConnectionStrings__Default` | `alfacredit` | Подключение к PostgreSQL |
| `API_PORT` | | `8080` | Порт API на хосте |
| `JWT_KEY` | `Jwt__Key` | демо-ключ | Секрет подписи JWT, не короче 32 символов. Для внешнего стенда задайте свой |
| | `Jwt__LifetimeMinutes` | `480` | Время жизни токена |
| | `Database__MigrateOnStartup` | `true` | Применять миграции при старте |
| | `Seed__Enabled` | `true` | Создавать демо-данные |
| `SEED_DEMO_PASSWORD` | `Seed__DemoPassword` | `Demo123!` | Пароль демо-учёток |
| `CORS_ORIGINS` | `Cors__Origins` | `http://localhost:5173,http://localhost:3000` | Разрешённые origin фронтенда, через запятую |
| | `Swagger__Enabled` | `true` | Включить Swagger UI |
| `PROCESSING_STAGE_DELAY_SECONDS` | `Processing__StageDelaySeconds` | `2` | Задержка этапов конвейера для наглядности демо (используется со следующего этапа) |

Секреты в репозитории не хранятся: в `appsettings.json` ключ JWT пустой, для `dotnet run` он берётся из
`appsettings.Development.json`, в compose из `.env`.

## Тестовые учётные записи

Пароль у всех: `Demo123!` (меняется через `SEED_DEMO_PASSWORD`).

| Логин | Роль | Сценарий |
|---|---|---|
| `client_good` | Client | ООО «Ромашка», стабильный бизнес: ожидается автоодобрение |
| `client_medium` | Client | ИП Кузнецов, повышенная долговая нагрузка: одобрение с изменёнными условиями |
| `client_manual` | Client | ООО «Вектор», крупная сумма: ручное рассмотрение менеджером |
| `client_bad` | Client | ООО «Закат», просрочки и налоговая задолженность: отказ с объяснением |
| `client_new` | Client | ИП Петрова, бизнесу 4 месяца: подсказка о неподходящих продуктах |
| `manager` | Manager | Кредитный менеджер: очередь заявок и ручные решения |
| `admin` | Admin | Администратор: продукты, параметры политики, аудит |

Сценарии клиентов реализуются на следующих этапах; сейчас учётки используются для входа и проверки ролей.

## Сценарий проверки (этап 0)

1. `docker compose up --build`, дождаться строки `Now listening on: http://[::]:8080`.
2. Открыть http://localhost:8080/health/ready: статус `Healthy`, проверка `database` в списке.
3. В Swagger выполнить `POST /api/v1/auth/login` с телом `{"login": "manager", "password": "Demo123!"}`: в ответе `accessToken` и профиль с ролью `Manager`.
4. Нажать Authorize, вставить токен, выполнить `GET /api/v1/auth/me`: вернётся тот же профиль.
5. Повторить вход с неверным паролем: ответ `401` в формате ProblemDetails с `traceId`.

Те же запросы в виде коллекции: [docs/requests.http](docs/requests.http) (VS Code REST Client, Rider)
и [docs/postman_collection.json](docs/postman_collection.json).

## API

Все методы под префиксом `/api/v1`, формат JSON, ошибки в формате ProblemDetails (RFC 9457).
Защищённые методы принимают `Authorization: Bearer <JWT>`. Роли: `Client`, `Manager`, `Admin`.

| Метод | Путь | Доступ | Назначение |
|---|---|---|---|
| POST | `/api/v1/auth/login` | все | Вход, выдача JWT (лимит 20 попыток в минуту с одного IP) |
| GET | `/api/v1/auth/me` | по токену | Профиль текущего пользователя |
| GET | `/health/live`, `/health/ready` | все | Проверки живости и готовности |

Полное описание: Swagger UI или `swagger/v1/swagger.json`.

## Структура репозитория

```
├─ src/                 Api, Core, Infrastructure
├─ tests/               UnitTests, IntegrationTests
├─ docs/                план, коллекции запросов, (далее) диаграммы и описание API
├─ .github/workflows/   CI: restore в locked-mode, build, test, docker build
├─ Dockerfile           multi-stage сборка API, non-root runtime
├─ docker-compose.yml   db + api
├─ Directory.Build.props, Directory.Packages.props, global.json   фиксация настроек и версий
└─ THIRD_PARTY_NOTICES.md
```

Версии зафиксированы: SDK в `global.json`, пакеты в `Directory.Packages.props` и `packages.lock.json`
(restore в CI и Docker выполняется в `--locked-mode`), образы в `Dockerfile` и `docker-compose.yml` точными тегами.
