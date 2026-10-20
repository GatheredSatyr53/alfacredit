# AlfaCredit: цифровой кредитный конвейер для малого и микробизнеса

Прототип для кейса «Умный кредитный кабинет предпринимателя» (хакатон Альфа-Банка).
Решение объединяет клиентский путь (выбор продукта, заявка, документы, статус, оффер, подписание,
выдача) и внутреннюю обработку банка (проверки, скоринг, кредитное решение, работа менеджера)
в один управляемый процесс.

> Текущее состояние: **этап 1**. Готовы аутентификация с ролями, каталог продуктов, компании клиентов,
> подача и просмотр заявок с историей статусов и понятными ошибками по полям, PostgreSQL с миграциями
> и повторяемыми демо-данными, Swagger, health checks, Docker Compose и CI.
> Следующие этапы (конвейер обработки, решения банка, оффер, договор, выдача): [docs/backend-plan.md](docs/backend-plan.md).

## Архитектура

| Слой | Проект | Содержание |
|---|---|---|
| Web API | `src/AlfaCredit.Api` | ASP.NET Core MVC: контроллеры, DTO, JWT, Swagger, health checks, обработка ошибок, composition root |
| Домен и сценарии | `src/AlfaCredit.Core` | Сущности (пользователь, компания, продукт, заявка), state machine заявки, каталог статусов, сценарии, порты. Без зависимостей от EF и ASP.NET |
| Инфраструктура | `src/AlfaCredit.Infrastructure` | EF Core + PostgreSQL, миграции, репозитории, seed-данные, хеширование паролей |
| Тесты | `tests/AlfaCredit.UnitTests`, `tests/AlfaCredit.IntegrationTests` | xUnit; интеграционные тесты поднимают PostgreSQL через Testcontainers |

Зависимости направлены внутрь: `Api → Core ← Infrastructure`.
Стек: .NET 10 LTS, ASP.NET Core MVC, EF Core 10, PostgreSQL 16, Serilog, Swashbuckle, xUnit, Docker.

Документация: [описание API с примерами](docs/api.md), [жизненный цикл заявки](docs/state-machine.md),
[модель данных](docs/data-model.md), [OpenAPI](docs/openapi.json), [контракт с сервисом скоринга](docs/scoring-contract.md),
[план backend](docs/backend-plan.md).

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

Экспорт актуального контракта в репозиторий: `curl http://localhost:8080/swagger/v1/swagger.json > docs/openapi.json`.

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

## Тестовые данные

Seed повторяем: при каждом старте добавляются только отсутствующие записи (ключи: логин, ИНН, код продукта),
дубликатов не возникает. Пароль у всех учёток: `Demo123!` (меняется через `SEED_DEMO_PASSWORD`).

| Логин | Роль | Компания | Сценарий |
|---|---|---|---|
| `client_good` | Client | ООО «Ромашка», ИНН 7701234567, 7 лет, выручка 48 млн | стабильный бизнес: ожидается автоодобрение |
| `client_medium` | Client | ИП Кузнецов, ИНН 503212345678, 3 года, заметная долговая нагрузка | одобрение с изменёнными условиями |
| `client_manual` | Client | ООО «Вектор», ИНН 7812345678, 10 лет, выручка 180 млн | крупная сумма: ручное рассмотрение менеджером |
| `client_bad` | Client | ООО «Закат», ИНН 5401234567, 6 лет | просрочки и налоговая задолженность: отказ с объяснением |
| `client_new` | Client | ИП Петрова, ИНН 772812345678, 4 месяца | молодой бизнес: продукты недоступны, объяснение в ошибке |
| `manager` | Manager | | кредитный менеджер: все заявки, ручные решения |
| `admin` | Admin | | администратор: продукты, параметры политики, аудит |

Продукты: `working-capital` (оборотный, 300 тыс–10 млн, 3–24 мес), `express` (100 тыс–3 млн, 3–18 мес, бизнес от 6 мес),
`overdraft` (100 тыс–5 млн, 1–12 мес), `credit-line` (500 тыс–20 млн, 6–36 мес), `investment` (1–50 млн, 12–84 мес, залог, бизнес от 2 лет).

## Сценарий проверки

1. `docker compose up --build`, дождаться строки `Now listening on: http://[::]:8080`.
2. Открыть http://localhost:8080/health/ready: статус `Healthy`, проверка `database` в списке.
3. `GET /api/products`: пять продуктов с границами сумм, сроков и допустимыми целями.
4. В Swagger выполнить `POST /api/auth/login` с телом `{"login": "client_good", "password": "Demo123!"}`,
   нажать Authorize и вставить `accessToken`.
5. `GET /api/companies`: ООО «Ромашка», скопировать `id`.
6. `POST /api/applications` с телом
   `{"companyId": "<id>", "productCode": "working-capital", "amount": 1500000, "termMonths": 12, "purpose": "WorkingCapital", "purposeDescription": "Закупка товара"}`:
   ответ `201`, в теле `id`, номер `AC-000001` и статус `Submitted`, в заголовке `Location` адрес заявки.
7. `GET /api/applications/{id}`: статус с названием «Отправлена» и подсказкой, что делать дальше,
   доступное действие `cancel`, история из одной записи.
8. Повторить шаг 6 с `productCode: "overdraft"`, `amount: 50000000`, `termMonths: 60`, `purpose: "Equipment"`:
   ответ `400`, в `errors` сообщения для полей `amount`, `termMonths`, `purpose` с границами продукта.
9. Войти как `client_new` и подать заявку на `express`: `400` с объяснением, что бизнесу 4 месяца, а нужно 6.
10. Войти как `client_bad` и запросить заявку из шага 6: `403`. Войти как `manager`: `200`.
11. `POST /api/applications/{id}/cancel`: статус `Cancelled`, вторая запись в истории; повторная отмена даёт `409`.
12. Перезапустить API (`docker compose restart api`) и повторить `GET /api/applications/{id}`: заявка на месте,
    в логе `Seeding completed: users +0, products +0, companies +0`.

Те же запросы в виде коллекции: [docs/requests.http](docs/requests.http) (VS Code REST Client, Rider)
и [docs/postman_collection.json](docs/postman_collection.json).

## API

Все методы под префиксом `/api`, формат JSON, ошибки в формате ProblemDetails (RFC 9457) с `traceId`.
Защищённые методы принимают `Authorization: Bearer <JWT>`. Роли: `Client`, `Manager`, `Admin`.
Подробности и примеры тел: [docs/api.md](docs/api.md).

| Метод | Путь | Доступ | Назначение |
|---|---|---|---|
| POST | `/api/auth/login` | все | Вход, выдача JWT (лимит 20 попыток в минуту с одного IP) |
| GET | `/api/auth/me` | по токену | Профиль текущего пользователя |
| GET | `/api/products` | все | Каталог кредитных продуктов |
| GET | `/api/products/{code}` | все | Продукт по коду |
| GET | `/api/companies` | по токену | Компании клиента (банк видит все) |
| GET | `/api/companies/{id}` | по токену | Карточка компании |
| GET | `/api/applications` | по токену | Список заявок с фильтром по статусу и пагинацией |
| POST | `/api/applications` | Client | Подать заявку; `400` с ошибками по полям при некорректных данных |
| GET | `/api/applications/{id}` | по токену | Заявка со статусом, доступными действиями и историей |
| POST | `/api/applications/{id}/cancel` | Client | Отменить заявку до решения банка |
| GET | `/api/applications/statuses` | все | Справочник статусов с текстами для интерфейса |
| GET | `/health/live`, `/health/ready` | все | Проверки живости и готовности |

## Структура репозитория

```
├─ src/                 Api, Core, Infrastructure
├─ tests/               UnitTests, IntegrationTests
├─ docs/                api.md, state-machine.md, data-model.md, openapi.json, requests.http, postman_collection.json, backend-plan.md
├─ .github/workflows/   CI: restore в locked-mode, build, test, docker build
├─ Dockerfile           multi-stage сборка API, non-root runtime
├─ docker-compose.yml   db + api
├─ Directory.Build.props, Directory.Packages.props, global.json   фиксация настроек и версий
└─ THIRD_PARTY_NOTICES.md
```

Версии зафиксированы: SDK в `global.json`, пакеты в `Directory.Packages.props` и `packages.lock.json`
(restore в CI и Docker выполняется в `--locked-mode`), образы в `Dockerfile` и `docker-compose.yml` точными тегами.
