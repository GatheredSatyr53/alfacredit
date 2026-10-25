# AlfaCredit: цифровой кредитный конвейер для малого и микробизнеса

Прототип для кейса «Умный кредитный кабинет предпринимателя» (хакатон Альфа-Банка).
Решение объединяет клиентский путь (выбор продукта, заявка, статус, предложение банка, подписание,
выдача) и внутреннюю обработку банка (проверки, скоринг, кредитное решение, работа менеджера)
в один управляемый процесс.

> Текущее состояние: **этап 2**. Заявка после подачи автоматически проходит конвейер банка: проверка данных,
> обогащение сведениями из ЕГРЮЛ, ФНС, БКИ и ФССП (локальные симуляторы), скоринг, кредитная политика.
> Результат: одобрение с предложением, одобрение с изменёнными условиями, отказ с объяснением причин
> или передача менеджеру. Клиент принимает или отклоняет предложение, видит ленту событий,
> получает обновления в реальном времени через SignalR. Скоринг считает отдельный сервис (контейнер `scoring`,
> контракт в [docs/scoring-contract.md](docs/scoring-contract.md); сервис ML-команды с правилами `rules-0.3`),
> при его недоступности backend считает встроенной моделью по тем же правилам и помечает результат `RulesFallback`.
> После принятия предложения формируется договор с графиком платежей и печатной формой, клиент подписывает его кодом
> подтверждения из ленты событий (имитация SMS), симулятор банковского ядра зачисляет средства, и заявка завершается
> статусом «Средства выданы» ([docs/completion-flow.md](docs/completion-flow.md)). Заявки, переданные менеджеру,
> в демо-режиме получают смоделированный ответ менеджера (50/50 одобрение с условиями или отказ, помечен `isSimulated`).
> Дальше по плану: действия менеджера, запросы уточнений, договор и выдача ([docs/backend-plan.md](docs/backend-plan.md)).

## Архитектура

| Слой | Проект | Содержание |
|---|---|---|
| Web API | `src/AlfaCredit.Api` | ASP.NET Core MVC: контроллеры, DTO, JWT, Swagger, SignalR-хаб, health checks, обработка ошибок |
| Домен и сценарии | `src/AlfaCredit.Core` | Сущности, state machine заявки, конвейер обработки, балльный скоринг, кредитная политика, лента, порты. Без зависимостей от EF и ASP.NET |
| Инфраструктура | `src/AlfaCredit.Infrastructure` | EF Core + PostgreSQL, миграции, репозитории, seed, симуляторы внешних источников, HTTP-клиент внешнего скоринга, фоновый воркер |
| Сервис скоринга | образ `ghcr.io/uandrosova07-arch/scoring` (репозиторий ML-команды) | Правила `rules-0.3` по [контракту](docs/scoring-contract.md): `GET /health`, `POST /api/v1/score`; отдельный контейнер. Те же правила встроены в backend как резервная модель; общий эталон из 63 случаев проходят тесты с обеих сторон |
| Тесты | `tests/AlfaCredit.UnitTests`, `tests/AlfaCredit.IntegrationTests` | xUnit; интеграционные тесты поднимают PostgreSQL через Testcontainers и прогоняют конвейер целиком, в том числе с заглушкой внешнего скоринга |

Зависимости направлены внутрь: `Api → Core ← Infrastructure`.
Стек: .NET 10 LTS, ASP.NET Core MVC, EF Core 10, PostgreSQL 16, SignalR, Serilog, Swashbuckle, xUnit, Docker.

Как устроен конвейер: заявка создаёт задание в таблице `processing_jobs`; фоновый воркер забирает задания через
`FOR UPDATE SKIP LOCKED`, поэтому экземпляров API может быть несколько, а рестарт ничего не теряет.
Этапы Validate, Enrich, Score, Decide выполняются по очереди с паузой `Processing__StageDelaySeconds`,
чтобы движение заявки было видно в кабинете. Сбой этапа повторяется три раза, затем заявка уходит менеджеру.
Взятое задание держит аренду, а при старте воркер возвращает в очередь задания, прерванные остановкой
(`Requeued N job(s)` в логе): перезапуск посреди обработки ничего не теряет, заявка доходит до решения.
На этапе Score backend отправляет сервису скоринга вложенный JSON (заявка с расчётным платежом, компания, проверки),
сохраняет балл, грейд, факторы, предупреждения, пропуски в данных, версию модели и время расчёта, а на этапе Decide
применяет кредитную политику: стоп-факторы, лимит автоодобрения, правила по грейдам A–E. Подписание договора ставит
в очередь этап Disburse: симулятор банковского ядра переводит средства, заявка получает статус «Средства выданы».

Документация: [описание API с примерами](docs/api.md), [жизненный цикл заявки](docs/state-machine.md),
[завершение заявки: договор и выдача](docs/completion-flow.md),
[предварительный подбор финансирования](docs/financing-preview.md),
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
| Readiness (проверяет базу и сервис скоринга) | http://localhost:8080/health/ready |
| Сервис скоринга | http://localhost:8000/health |

Миграции и демо-данные применяются автоматически при старте API.
Остановить и удалить данные: `docker compose down -v`.

## Запуск для разработки

```bash
docker compose up -d db scoring              # PostgreSQL и сервис скоринга
dotnet restore
dotnet run --project src/AlfaCredit.Api      # http://localhost:5080/swagger
dotnet test                                  # unit + интеграционные (последние требуют Docker, иначе пропускаются)
# интеграционные тесты без Docker, на отдельной базе внешнего PostgreSQL (нужно право CREATEDB):
ALFACREDIT_TEST_POSTGRES="Host=localhost;Username=alfacredit;Password=alfacredit;Database=postgres" dotnet test tests/AlfaCredit.IntegrationTests
```

При `dotnet run` по умолчанию работает встроенная модель (`Scoring:Mode` = `Rules` в `appsettings.json`).
Чтобы ходить в сервис скоринга из compose: `Scoring__Mode=External Scoring__BaseUrl=http://localhost:8000 dotnet run --project src/AlfaCredit.Api`.

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
| | `RateLimiting__LoginPermitLimit`, `RateLimiting__LoginWindowSeconds` | `20`, `60` | Лимит попыток входа с одного IP за окно |
| `PROCESSING_STAGE_DELAY_SECONDS` | `Processing__StageDelaySeconds` | `2` | Пауза между этапами конвейера, чтобы движение заявки было видно; 0 для максимальной скорости |
| | `Processing__WorkerCount` | `2` | Параллельных циклов воркера |
| `SIMULATORS_FAILURE_RATE` | `Simulators__FailureRate` | `0` | Вероятность сбоя внешнего источника (0..1): показывает повторы и передачу менеджеру |
| | `Processing__LeaseSeconds` | `120` | Аренда задания воркером; по её истечении зависшее задание заберёт другой цикл |
| `SCORING_MODE` | `Scoring__Mode` | `External` (compose), `Rules` (`dotnet run`) | `External`: сервис по [контракту](docs/scoring-contract.md) с откатом на встроенную модель; `Rules`: только встроенная балльная модель |
| `SCORING_BASE_URL` | `Scoring__BaseUrl` | `http://scoring:8000` | Адрес сервиса скоринга (имя сервиса в compose) |
| `SCORING_IMAGE` | | `ghcr.io/uandrosova07-arch/scoring:4ce9e84…` | Образ сервиса скоринга ML-команды (правила `rules-0.3`), тег закреплён на коммите репозитория scoring; сборка из локального checkout: `compose.scoring.yml` |
| `SCORING_TIMEOUT_SECONDS` | `Scoring__TimeoutSeconds` | `5` | Таймаут одного запроса к скорингу |
| `SCORING_RETRIES` | `Scoring__Retries` | `2` | Повторы при 5xx, таймауте и обрыве связи (паузы `Scoring__RetryDelayMs` × номер попытки: 1 с, 2 с); 4xx не повторяется |
| `SCORING_PORT` | | `8000` | Порт сервиса скоринга на хосте, только для отладки |
| | `DecisionPolicy__*` | см. `appsettings.json` | Пороги кредитной политики: срок оффера, доля выручки, стоп-факторы |
| `CONTRACTS_REVEAL_CODE` | `Contracts__RevealCodeInResponse` | `true` | Демо-режим: код подтверждения подписания дублируется в ответ API (в ленту он приходит всегда) |
| `DEMO_MANAGER_ENABLED` | `DemoManager__Enabled` | `true` | Демо-режим: заявки в ручном рассмотрении получают смоделированный ответ менеджера; выключить при появлении настоящей очереди менеджера |
| `DEMO_MANAGER_DELAY_SECONDS`, `DEMO_MANAGER_APPROVAL_PROBABILITY` | `DemoManager__DelaySeconds`, `DemoManager__ApprovalProbability` | `8`, `0.5` | Пауза до ответа смоделированного менеджера и вероятность одобрения (50/50) |
| | `FinancingPreview__AutoDecisionBusinessDays`, `FinancingPreview__ManualReviewBusinessDays`, `FinancingPreview__CollateralBusinessDays` | `1`, `3`, `10` | Ориентир получения денег в рабочих днях для предварительного подбора: автоматическое решение, менеджер, залог |
| | `FinancingPreview__ComfortableDebtLoad`, `FinancingPreview__ElevatedDebtLoad`, `FinancingPreview__HighDebtLoad`, `FinancingPreview__MaxNeedByDays` | `0.30`, `0.50`, `0.70`, `730` | Пороги кредитной нагрузки (доля месячной прибыли, банды фактора `debt_load` правил `rules-0.3`) и горизонт даты `needByDate` |
| | `Contracts__SigningDays`, `Contracts__CodeTtlMinutes`, `Contracts__CodeMaxAttempts`, `Contracts__LenderName` | `5`, `10`, `5`, `AlfaCredit (демонстрационный банк)` | Срок подписания договора, срок и число попыток кода, наименование кредитора в договоре |

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

1. `docker compose up --build`: поднимаются `db`, `scoring` и `api`; дождаться строк `Now listening on: http://[::]:8080`
   и `Processing worker started`.
2. Открыть http://localhost:8080/health/ready: статус `Healthy`, проверки `database` и `scoring` в списке;
   http://localhost:8000/health отвечает 200 с `model_version` = `rules-0.3` (сервис ML-команды).
3. В Swagger выполнить `POST /api/auth/login` с телом `{"login": "client_good", "password": "Demo123!"}`,
   нажать Authorize и вставить `accessToken`.
4. `GET /api/companies`: ООО «Ромашка», скопировать `id`.
5. `POST /api/financing/preview` с телом
   `{"companyId": "<id>", "amount": 1500000, "purpose": "WorkingCapital", "needByDate": "<дата через 3 недели>", "termMonths": 12}`:
   ответ `200` с двумя вариантами (первый `isRecommended`: «Оборотный кредит», платёж 138 593,06 ₽, переплата 163 116,74 ₽,
   при одобрении деньги ориентировочно на следующий рабочий день), «Овердрафт» и «Кредитная линия» в `notCompared` (продукты с лимитом
   не сравниваются по аннуитетной модели), «Инвестиционный кредит» в `unsuitable` с причиной по цели. Заявка при этом не создаётся.
   Без `termMonths` срок подбирается (12 мес.: самый короткий без штрафа за нагрузку в скоринге), под `client_new` ответ без вариантов
   с объяснением «возраст бизнеса», под `client_manual` на 8 000 000 ₽ в `suggestedAmount` приходит 5 000 000 ₽ (лимит автоматического решения).
6. `POST /api/applications` с телом
   `{"companyId": "<id>", "productCode": "working-capital", "amount": 1500000, "termMonths": 12, "purpose": "WorkingCapital", "needByDate": "<та же дата>"}`
   (или `options[].applyWith` из предыдущего шага): ответ `201` с `id` и номером `AC-000001`; `needByDate` необязательно.
7. Несколько раз подряд выполнить `GET /api/applications/{id}`: статус меняется каждые 2 секунды
   «Отправлена» → «Проверка данных» → «Сбор сведений» → «Оценка риска» → «Принятие решения» → «Одобрена».
   В ответе появятся `decision` с причинами простым языком и `offer` с суммой, ставкой, платежом, общей выплатой и переплатой,
   в `availableActions` будут `acceptOffer` и `declineOffer`.
8. `POST /api/applications/{id}/offer/accept`: статус «Подписание договора», `offer.response` = `Accepted`, в ответе `contract`
   со статусом `AwaitingSignature`, номером `KD-000001`, суммой, ставкой, платежом, переплатой и `documentUrl`;
   `availableActions` = `signContract`, `cancel`. Повторный вызов даёт `409`.
9. `GET /api/applications/{id}/contract`: стороны, условия и график из 12 платежей с остатком 0 в последней строке;
   `GET /api/applications/{id}/contract/document` открывает печатную форму договора (HTML).
10. `POST /api/applications/{id}/contract/sign/request-code`: в ответе `demoCode` и срок действия кода, тот же код
   в `GET /api/feed` (событие `SigningCode`). Сначала отправить неверный код в `POST /api/applications/{id}/contract/sign`
   с телом `{"code": "000000"}`: ответ `400` с `errors.code` «Осталось попыток: 4». Затем верный код: статус «Договор подписан».
11. Через ~2 секунды `GET /api/applications/{id}`: статус «Средства выданы» (терминальный), `disbursement`
    с номером платёжного поручения `PO-…`, суммой и счётом; в `history` девять записей от «Отправлена» до «Средства выданы».
12. `GET /api/feed`: события «Заявка принята», «Заявка одобрена», «Договор готов к подписанию», «Код подтверждения»,
    «Договор подписан», «Средства выданы»; `GET /api/feed/unread-count`.
13. Войти как `client_medium` и подать `working-capital` на 3 000 000 ₽ на 24 мес: через ~10 секунд
   статус «Одобрена с изменением условий», в `decision.conditions` объяснение, сумма 2 400 000 ₽, ставка 22.5 %.
14. Войти как `client_bad` и подать `express` на 500 000 ₽ на 6 мес: статус «Отказ», в `decision.reasons`
    четыре стоп-фактора с объяснениями.
15. Войти как `client_manual` и подать `working-capital` на 8 000 000 ₽: статус «Рассмотрение менеджером»,
    `decision.recommendedType` = `Approved`. Через ~8 секунд смоделированный менеджер (демо-режим) отвечает 50/50:
    «Одобрена с изменением условий» с предложением 8 000 000 ₽ под 21 % (клиент может принять его и дойти до выдачи)
    или «Отказ» с объяснением. В `decision` будет `decidedBy` = `Manager`, `isSimulated` = `true`, в `history` запись
    «Смоделированный ответ менеджера (демо-режим)». После `docker compose restart api` решение не меняется.
16. Войти как `manager`: `GET /api/applications/{id}` любой заявки показывает `scoring` с баллом, грейдом, факторами,
    `source` = `External`, `modelVersion` = `rules-0.3`, `missingData`, `requestId` и `durationMs`,
    а также `checks` с результатами четырёх источников; `GET /api/feed` содержит событие о новой заявке в очереди.
    В логе `scoring` виден запрос с тем же `request_id`.
17. Недоступность скоринга: `docker compose stop scoring`, затем подать заявку от `client_good` (`express`, 300 000 ₽ на 6 мес).
    Заявка не зависает: через несколько секунд (таймаут и два повтора) она одобрена, у менеджера в `scoring`
    `source` = `RulesFallback`, `modelVersion` = `rules-0.3` (встроенная модель считает по тем же правилам, балл и грейд те же)
    и предупреждение `external_scoring_unavailable`, в `/health/ready` проверка `scoring`
    в статусе `Degraded`. `docker compose start scoring` возвращает внешний скоринг.
18. Перезапуск посреди обработки: подать заявку и в течение пары секунд выполнить `docker compose restart api`.
    После старта в логе `Requeued 1 job(s) interrupted by the previous shutdown`, заявка доходит до решения,
    в `history` каждый статус встречается один раз.
19. Перезапустить API (`docker compose restart api`) и повторить `GET /api/applications/{id}`: всё на месте,
    в логе `Seeding completed: users +0, products +0, companies +0`.
20. Для демонстрации отказоустойчивости задать в `.env` `SIMULATORS_FAILURE_RATE=0.5` и перезапустить:
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
| GET | `/api/products`, `/api/products/{code}` | все | Каталог кредитных продуктов; с `?amount=&termMonths=` — предварительный ежемесячный платёж, общая выплата и переплата по базовой ставке |
| GET | `/api/companies`, `/api/companies/{id}` | по токену | Компании клиента (банк видит все) |
| GET | `/api/applications` | по токену | Список заявок с фильтром по статусу и пагинацией |
| POST | `/api/financing/preview` | по токену | Предварительный подбор «нужны деньги к дате»: срочные продукты с платежом, переплатой, нагрузкой, ориентиром даты и меньшей суммой для пересчёта; продукты с лимитом и неподходящие с причинами; заявку не создаёт ([docs/financing-preview.md](docs/financing-preview.md)) |
| POST | `/api/applications` | Client | Подать заявку (необязательно `needByDate`); запускает конвейер; `400` с ошибками по полям |
| GET | `/api/applications/{id}` | по токену | Заявка: статус, решение, предложение, действия, история; банку также скоринг и проверки |
| POST | `/api/applications/{id}/offer/accept` | Client | Принять предложение банка |
| POST | `/api/applications/{id}/offer/decline` | Client | Отклонить предложение |
| GET | `/api/applications/{id}/contract`, `/contract/document` | по токену | Договор после принятия предложения: условия, график платежей; печатная форма HTML |
| POST | `/api/applications/{id}/contract/sign/request-code`, `/contract/sign` | Client | Подписание кодом подтверждения из ленты (имитация SMS); после подписи воркер выполняет выдачу ([docs/completion-flow.md](docs/completion-flow.md)) |
| POST | `/api/applications/{id}/cancel` | Client | Отменить заявку до решения банка или отказаться от подписания договора |
| GET | `/api/applications/statuses` | все | Справочник статусов с текстами для интерфейса |
| GET | `/api/feed`, `/api/feed/unread-count` | по токену | Лента событий и счётчик непрочитанных |
| POST | `/api/feed/{id}/read`, `/api/feed/read-all` | по токену | Отметить прочитанным |
| WS | `/hubs/applications` | по токену | SignalR: событие `applicationChanged` при каждой смене статуса |
| GET | `/health/live`, `/health/ready` | все | Проверки живости и готовности (база, сервис скоринга) |

## Структура репозитория

```
├─ src/                 Api, Core, Infrastructure
├─ compose.scoring.yml  override: сборка сервиса скоринга из локального checkout репозитория scoring вместо образа из GHCR
├─ tests/               UnitTests, IntegrationTests
├─ docs/                api.md, financing-preview.md, completion-flow.md, state-machine.md, decision-logic.md, data-model.md, scoring-contract.md, openapi.json, requests.http, postman_collection.json, backend-plan.md
├─ .github/workflows/   CI: restore в locked-mode, build, test, docker build
├─ Dockerfile           multi-stage сборка API, non-root runtime
├─ docker-compose.yml   db + scoring (образ ML-команды, правила rules-0.3) + api
├─ Directory.Build.props, Directory.Packages.props, global.json   фиксация настроек и версий
└─ THIRD_PARTY_NOTICES.md
```

Версии зафиксированы: SDK в `global.json`, пакеты в `Directory.Packages.props` и `packages.lock.json`
(restore в CI и Docker выполняется в `--locked-mode`), образы в `Dockerfile` и `docker-compose.yml` точными тегами.


Обученная модель, выбор версии и общий резервный расчёт: [docs/logreg-integration.md](docs/logreg-integration.md).
