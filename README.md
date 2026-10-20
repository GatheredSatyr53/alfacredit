# AlfaCredit: цифровой кредитный конвейер для малого и микробизнеса

Прототип для кейса «Умный кредитный кабинет предпринимателя» (хакатон Альфа-Банка).
Решение объединяет клиентский путь (выбор продукта, заявка, статус, предложение банка, подписание,
выдача) и внутреннюю обработку банка (проверки, скоринг, кредитное решение, работа менеджера)
в один управляемый процесс.

> Текущее состояние: **этап 2**. Заявка после подачи автоматически проходит конвейер банка: проверка данных,
> обогащение сведениями из ЕГРЮЛ, ФНС, БКИ и ФССП (локальные симуляторы), скоринг, кредитная политика.
> Результат: одобрение с предложением, одобрение с изменёнными условиями, отказ с объяснением причин
> или передача менеджеру. Клиент принимает или отклоняет предложение, видит ленту событий,
> получает обновления в реальном времени через SignalR.
> Дальше по плану: действия менеджера, запросы уточнений, договор и выдача ([docs/backend-plan.md](docs/backend-plan.md)).

## Архитектура

| Слой | Проект | Содержание |
|---|---|---|
| Web API | `src/AlfaCredit.Api` | ASP.NET Core MVC: контроллеры, DTO, JWT, Swagger, SignalR-хаб, health checks, обработка ошибок |
| Домен и сценарии | `src/AlfaCredit.Core` | Сущности, state machine заявки, конвейер обработки, балльный скоринг, кредитная политика, лента, порты. Без зависимостей от EF и ASP.NET |
| Инфраструктура | `src/AlfaCredit.Infrastructure` | EF Core + PostgreSQL, миграции, репозитории, seed, симуляторы внешних источников, HTTP-клиент внешнего скоринга, фоновый воркер |
| Тесты | `tests/AlfaCredit.UnitTests`, `tests/AlfaCredit.IntegrationTests` | xUnit; интеграционные тесты поднимают PostgreSQL через Testcontainers и прогоняют конвейер целиком |

Зависимости направлены внутрь: `Api → Core ← Infrastructure`.
Стек: .NET 10 LTS, ASP.NET Core MVC, EF Core 10, PostgreSQL 16, SignalR, Serilog, Swashbuckle, xUnit, Docker.

Как устроен конвейер: заявка создаёт задание в таблице `processing_jobs`; фоновый воркер забирает задания через
`FOR UPDATE SKIP LOCKED`, поэтому экземпляров API может быть несколько, а рестарт ничего не теряет.
Этапы Validate, Enrich, Score, Decide выполняются по очереди с паузой `Processing__StageDelaySeconds`,
чтобы движение заявки было видно в кабинете. Сбой этапа повторяется три раза, затем заявка уходит менеджеру.

Документация: [описание API с примерами](docs/api.md), [жизненный цикл заявки](docs/state-machine.md),
[скоринг и кредитная политика](docs/decision-logic.md), [модель данных](docs/data-model.md),
[контракт с сервисом скоринга](docs/scoring-contract.md), [OpenAPI](docs/openapi.json), [план backend](docs/backend-plan.md).

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
| SignalR-хаб обновлений заявок | ws://localhost:8080/hubs/applications |
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
| `PROCESSING_STAGE_DELAY_SECONDS` | `Processing__StageDelaySeconds` | `2` | Пауза между этапами конвейера, чтобы движение заявки было видно; 0 для максимальной скорости |
| | `Processing__WorkerCount` | `2` | Параллельных циклов воркера |
| `SIMULATORS_FAILURE_RATE` | `Simulators__FailureRate` | `0` | Вероятность сбоя внешнего источника (0..1): показывает повторы и передачу менеджеру |
| `SCORING_MODE` | `Scoring__Mode` | `Rules` | `Rules`: встроенная балльная модель; `External`: сервис по [контракту](docs/scoring-contract.md) с откатом на правила |
| `SCORING_BASE_URL` | `Scoring__BaseUrl` | `http://scoring:8000` | Адрес внешнего сервиса скоринга |
| | `DecisionPolicy__*` | см. `appsettings.json` | Пороги кредитной политики: срок оффера, доля выручки, стоп-факторы |

Секреты в репозитории не хранятся: в `appsettings.json` ключ JWT пустой, для `dotnet run` он берётся из
`appsettings.Development.json`, в compose из `.env`.

## Тестовые данные

Seed повторяем: при каждом старте добавляются только отсутствующие записи (ключи: логин, ИНН, код продукта),
дубликатов не возникает. Пароль у всех учёток: `Demo123!` (меняется через `SEED_DEMO_PASSWORD`).
Симуляторы ЕГРЮЛ, ФНС, БКИ и ФССП отдают детерминированные данные по ИНН, поэтому исход каждого сценария предсказуем.

| Логин | Роль | Компания | Что подать | Исход конвейера |
|---|---|---|---|---|
| `client_good` | Client | ООО «Ромашка», ИНН 7701234567, 7 лет, выручка 48 млн | `working-capital`, 1 500 000 ₽ на 12 мес | грейд A: одобрено на запрошенных условиях под 19.5 % |
| `client_medium` | Client | ИП Кузнецов, ИНН 503212345678, 3 года, выручка 9.6 млн, просрочка 25 дней | `working-capital`, 3 000 000 ₽ на 24 мес | грейд C: одобрено на 2 400 000 ₽ под 22.5 % |
| `client_manual` | Client | ООО «Вектор», ИНН 7812345678, 10 лет, выручка 180 млн | `working-capital`, 8 000 000 ₽ на 24 мес | сумма выше лимита автоодобрения: передана менеджеру с рекомендацией одобрить |
| `client_bad` | Client | ООО «Закат», ИНН 5401234567, налоговый долг, просрочка 95 дней, ФССП | `express`, 500 000 ₽ на 6 мес | отказ по стоп-факторам с объяснением каждого |
| `client_new` | Client | ИП Петрова, ИНН 772812345678, 4 месяца | любой продукт | `400` при подаче: возраст бизнеса меньше минимума продукта |
| `manager` | Manager | | | видит все заявки, скоринг, проверки, ленту очереди |
| `admin` | Admin | | | администратор |

Продукты: `working-capital` (оборотный, 300 тыс–10 млн, 3–24 мес), `express` (100 тыс–3 млн, 3–18 мес, бизнес от 6 мес),
`overdraft` (100 тыс–5 млн, 1–12 мес), `credit-line` (500 тыс–20 млн, 6–36 мес), `investment` (1–50 млн, 12–84 мес, залог, бизнес от 2 лет).

## Сценарий проверки

1. `docker compose up --build`, дождаться строк `Now listening on: http://[::]:8080` и `Processing worker started`.
2. Открыть http://localhost:8080/health/ready: статус `Healthy`, проверка `database` в списке.
3. В Swagger выполнить `POST /api/auth/login` с телом `{"login": "client_good", "password": "Demo123!"}`,
   нажать Authorize и вставить `accessToken`.
4. `GET /api/companies`: ООО «Ромашка», скопировать `id`.
5. `POST /api/applications` с телом
   `{"companyId": "<id>", "productCode": "working-capital", "amount": 1500000, "termMonths": 12, "purpose": "WorkingCapital"}`:
   ответ `201` с `id` и номером `AC-000001`.
6. Несколько раз подряд выполнить `GET /api/applications/{id}`: статус меняется каждые 2 секунды
   «Отправлена» → «Проверка данных» → «Сбор сведений» → «Оценка риска» → «Принятие решения» → «Одобрена».
   В ответе появятся `decision` с причинами простым языком и `offer` с суммой, ставкой и платежом,
   в `availableActions` будут `acceptOffer` и `declineOffer`.
7. `POST /api/applications/{id}/offer/accept`: статус «Подписание договора», `offer.response` = `Accepted`.
   Повторный вызов даёт `409`.
8. `GET /api/feed`: события «Заявка принята», «Заявка одобрена», «Условия приняты»; `GET /api/feed/unread-count`.
9. Войти как `client_medium` и подать `working-capital` на 3 000 000 ₽ на 24 мес: через ~10 секунд
   статус «Одобрена с изменением условий», в `decision.conditions` объяснение, сумма 2 400 000 ₽, ставка 22.5 %.
10. Войти как `client_bad` и подать `express` на 500 000 ₽ на 6 мес: статус «Отказ», в `decision.reasons`
    четыре стоп-фактора с объяснениями.
11. Войти как `client_manual` и подать `working-capital` на 8 000 000 ₽: статус «Рассмотрение менеджером»,
    `decision.recommendedType` = `Approved`.
12. Войти как `manager`: `GET /api/applications/{id}` любой заявки показывает `scoring` с баллом, грейдом и факторами
    и `checks` с результатами четырёх источников; `GET /api/feed` содержит событие о новой заявке в очереди.
13. Перезапустить API (`docker compose restart api`) и повторить `GET /api/applications/{id}`: всё на месте,
    в логе `Seeding completed: users +0, products +0, companies +0`.
14. Для демонстрации отказоустойчивости задать в `.env` `SIMULATORS_FAILURE_RATE=0.5` и перезапустить:
    в логе видны повторы запросов к источникам, а при трёх сбоях критичного источника заявка уходит менеджеру.

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
| GET | `/api/products`, `/api/products/{code}` | все | Каталог кредитных продуктов |
| GET | `/api/companies`, `/api/companies/{id}` | по токену | Компании клиента (банк видит все) |
| GET | `/api/applications` | по токену | Список заявок с фильтром по статусу и пагинацией |
| POST | `/api/applications` | Client | Подать заявку; запускает конвейер; `400` с ошибками по полям |
| GET | `/api/applications/{id}` | по токену | Заявка: статус, решение, предложение, действия, история; банку также скоринг и проверки |
| POST | `/api/applications/{id}/offer/accept` | Client | Принять предложение банка |
| POST | `/api/applications/{id}/offer/decline` | Client | Отклонить предложение |
| POST | `/api/applications/{id}/cancel` | Client | Отменить заявку до решения банка |
| GET | `/api/applications/statuses` | все | Справочник статусов с текстами для интерфейса |
| GET | `/api/feed`, `/api/feed/unread-count` | по токену | Лента событий и счётчик непрочитанных |
| POST | `/api/feed/{id}/read`, `/api/feed/read-all` | по токену | Отметить прочитанным |
| WS | `/hubs/applications` | по токену | SignalR: событие `applicationChanged` при каждой смене статуса |
| GET | `/health/live`, `/health/ready` | все | Проверки живости и готовности |

## Структура репозитория

```
├─ src/                 Api, Core, Infrastructure
├─ tests/               UnitTests, IntegrationTests
├─ docs/                api.md, state-machine.md, decision-logic.md, data-model.md, scoring-contract.md, openapi.json, requests.http, postman_collection.json, backend-plan.md
├─ .github/workflows/   CI: restore в locked-mode, build, test, docker build
├─ Dockerfile           multi-stage сборка API, non-root runtime
├─ docker-compose.yml   db + api
├─ Directory.Build.props, Directory.Packages.props, global.json   фиксация настроек и версий
└─ THIRD_PARTY_NOTICES.md
```

Версии зафиксированы: SDK в `global.json`, пакеты в `Directory.Packages.props` и `packages.lock.json`
(restore в CI и Docker выполняется в `--locked-mode`), образы в `Dockerfile` и `docker-compose.yml` точными тегами.
