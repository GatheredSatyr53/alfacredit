# API кредитного конвейера

Базовый адрес при запуске через Docker Compose: `http://localhost:8080`, при `dotnet run`: `http://localhost:5080`.
Интерактивная документация: `/swagger`, машиночитаемый контракт: `/swagger/v1/swagger.json` (копия в `docs/openapi.json`).
Коллекции запросов: `docs/requests.http`, `docs/postman_collection.json`.
Отдельные контракты: предварительный подбор финансирования `docs/financing-preview.md`, завершение заявки `docs/completion-flow.md`.

## Общие правила

- Формат данных JSON, имена полей в camelCase, перечисления строками (`"purpose": "WorkingCapital"`), даты в ISO 8601 UTC.
- Аутентификация: `POST /api/auth/login` выдаёт JWT, дальше заголовок `Authorization: Bearer <token>`.
  Токен живёт 8 часов. Роли: `Client`, `Manager`, `Admin`.
- Ошибки в формате ProblemDetails (RFC 9457), всегда с `traceId` для поддержки:

| Код | Когда | Тело |
|---|---|---|
| 400 | ошибка валидации | `errors`: словарь «поле → список сообщений», поля в camelCase как в запросе |
| 401 | нет или просрочен токен, неверный пароль | `title` |
| 403 | нет прав: чужая компания, действие не для этой роли | `title`, `detail` |
| 404 | объект не найден | `title`, `detail` |
| 409 | действие недоступно в текущем статусе заявки | `title`, `detail` |
| 429 | больше 20 попыток входа в минуту с одного IP | |
| 500 | внутренняя ошибка | `title`, `traceId` для обращения в поддержку |

Пример ошибки валидации:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Ошибка валидации",
  "status": 400,
  "detail": "Исправьте указанные поля и повторите запрос.",
  "errors": {
    "amount": ["Для продукта «Овердрафт» сумма должна быть от 100 000 ₽ до 5 000 000 ₽."],
    "termMonths": ["Для продукта «Овердрафт» срок должен быть от 1 до 12 мес."],
    "purpose": ["Цель «Покупка оборудования или транспорта» недоступна для продукта «Овердрафт». Доступные цели: «Пополнение оборотных средств»."]
  },
  "traceId": "00-ab35113daa4aa3773f5788eb5d7970b7-47c5d1a0f5e83781-00"
}
```

## Аутентификация

### POST /api/auth/login

```json
{ "login": "client_good", "password": "Demo123!" }
```

Ответ 200:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-10-20T21:40:20.573+00:00",
  "user": { "id": "01a15619-fc62-780c-a87e-62b84281d220", "login": "client_good", "fullName": "Мария Иванова", "role": "Client" }
}
```

### GET /api/auth/me

Профиль по токену, тот же объект `user`.

## Продукты

### GET /api/products

Без аутентификации. Активные продукты в порядке витрины.

```json
[
  {
    "code": "working-capital",
    "name": "Оборотный кредит",
    "description": "Пополнение оборотных средств: закупка товара и сырья, сезонные расходы, кассовые разрывы. ...",
    "type": "WorkingCapital",
    "minAmount": 300000, "maxAmount": 10000000,
    "minTermMonths": 3, "maxTermMonths": 24,
    "baseRate": 19.5,
    "allowedPurposes": [
      { "code": "WorkingCapital", "title": "Пополнение оборотных средств" },
      { "code": "Other", "title": "Другая цель" }
    ],
    "minBusinessAgeMonths": 12,
    "requiresCollateral": false
  }
]
```

Продукты seed: `working-capital`, `express`, `overdraft`, `credit-line`, `investment`.

### GET /api/products/{code}

Один продукт, код в любом регистре. 404 для неизвестного кода.

## Компании

### GET /api/companies

Требует токен. Клиент получает компании, представителем которых является; сотрудник банка все компании.
Значение `id` используется как `companyId` при подаче заявки.

```json
[
  {
    "id": "01a15975-ea5f-7a4f-863b-9035b3b8b2ca",
    "inn": "7701234567", "ogrn": "1157746123456",
    "name": "ООО «Ромашка»", "legalForm": "Ooo", "okved": "47.11",
    "registeredAt": "2019-03-12", "businessAgeMonths": 91,
    "address": "Москва, ул. Садовая, д. 12", "headName": "Иванова Мария Сергеевна",
    "taxRegime": "Osno",
    "annualRevenue": 48000000, "annualExpenses": 40800000, "netProfit": 7200000,
    "monthlyDebtPayments": 150000, "employeesCount": 24,
    "myRole": "Director"
  }
]
```

### GET /api/companies/{id}

Карточка компании. 403, если клиент не её представитель.

## Предварительный подбор

### POST /api/financing/preview

Три вопроса клиента (на что, сколько, к какому дню) и необязательный срок. Возвращает подходящие продукты с платежом,
переплатой, кредитной нагрузкой, ориентиром получения денег и объяснениями, а также продукты, которые не подходят, с причинами.
Заявка не создаётся, расчёт не сохраняется. Клиент считает только для своих компаний (иначе `403`), сотрудник банка для любой.
Полный контракт с примерами: [financing-preview.md](financing-preview.md).

```json
{ "companyId": "01a15975-ea5f-7a4f-863b-9035b3b8b2ca", "amount": 1500000, "purpose": "WorkingCapital", "needByDate": "2026-11-15", "termMonths": 12 }
```

Ответ 200: `calculationDate` (дата расчёта по Москве), `summary`, `disclaimer`, `options[]` (срочные продукты: `productCode`, `termMonths`,
`cost`, `affordability`, `funding`, `suggestedAmount`, `explanations`, `limitations`, `requirements`, `applyWith`), `notCompared[]`
(овердрафт и кредитная линия: по аннуитетной модели не сравниваются), `unsuitable[]` (`productCode`, `reasons[]`, `suggestedAmount`).
Тело `options[].applyWith` отправляется в `POST /api/applications` без изменений. Ошибки `400` по полям `amount`, `purpose`,
`needByDate`, `termMonths`, `companyId`. Расчёт не является одобрением и не гарантирует выдачу.

## Заявки

### POST /api/applications

Роль `Client`. Проверяет поля и соответствие продукту (сумма, срок, цель, возраст бизнеса), сохраняет заявку
и сразу отправляет её в банк.

```json
{
  "companyId": "01a15975-ea5f-7a4f-863b-9035b3b8b2ca",
  "productCode": "working-capital",
  "amount": 1500000,
  "termMonths": 12,
  "purpose": "WorkingCapital",
  "purposeDescription": "Закупка товара к сезону"
}
```

| Поле | Тип | Обязательно | Правила |
|---|---|---|---|
| companyId | uuid | да | своя компания |
| productCode | string | да | код из `GET /api/products` |
| amount | number | да | > 0, в границах продукта |
| termMonths | int | да | 1..120, в границах продукта |
| purpose | enum | да | `WorkingCapital`, `Equipment`, `Expansion`, `Refinancing`, `Other`; допустим для продукта |
| purposeDescription | string | нет | до 1000 символов |
| needByDate | date | нет | к какой дате нужны деньги, `YYYY-MM-DD`: не раньше сегодняшнего дня по Москве и не позже чем через 2 года; хранится как календарная дата без сдвига и показывается, на обработку не влияет. Готовое тело заявки даёт `POST /api/financing/preview` (`options[].applyWith`) |

Ответ 201, заголовок `Location: /api/applications/{id}`:

```json
{ "id": "01a15975-f14b-7636-a356-687331e9eb8d", "number": "AC-000001", "status": "Submitted" }
```

### GET /api/applications/{id}

Клиент видит заявки своих компаний, сотрудник банка любые.

```json
{
  "id": "01a15975-f14b-7636-a356-687331e9eb8d",
  "number": "AC-000001",
  "status": {
    "code": "Submitted",
    "title": "Отправлена",
    "description": "Заявка принята банком и ожидает автоматической проверки.",
    "clientNextAction": "Ничего делать не нужно: проверка начнётся автоматически.",
    "bankNextAction": "Ожидает автоматической проверки.",
    "isTerminal": false,
    "progressStep": 1,
    "totalSteps": 6
  },
  "company": { "id": "01a15975-ea5f-7a4f-863b-9035b3b8b2ca", "name": "ООО «Ромашка»", "inn": "7701234567" },
  "product": { "code": "working-capital", "name": "Оборотный кредит" },
  "amount": 1500000,
  "termMonths": 12,
  "purpose": "WorkingCapital",
  "purposeTitle": "Пополнение оборотных средств",
  "purposeDescription": "Закупка товара к сезону",
  "createdByUserId": "01a15619-f9cc-70fe-bad8-2c6a70edfbd5",
  "createdAt": "2026-10-20T13:17:20.12+00:00",
  "updatedAt": "2026-10-20T13:17:20.12+00:00",
  "availableActions": ["cancel"],
  "history": [
    {
      "fromStatus": null,
      "toStatus": "Submitted",
      "toStatusTitle": "Отправлена",
      "actor": "Client",
      "actorUserId": "01a15619-f9cc-70fe-bad8-2c6a70edfbd5",
      "comment": "Заявка создана и отправлена в банк.",
      "occurredAt": "2026-10-20T13:17:20.12+00:00"
    }
  ]
}
```

`availableActions` перечисляет действия, доступные текущему пользователю в этом статусе; кнопки в интерфейсе
строятся по нему: `cancel`, `acceptOffer`, `declineOffer`. Действия менеджера и подписание появятся на следующих этапах.

После подачи заявка автоматически проходит конвейер банка, статус меняется каждые ~2 секунды. Когда решение принято,
в ответе появляются `decision` и, для одобрения, `offer`:

```json
{
  "status": { "code": "Approved", "title": "Одобрена", "clientNextAction": "Ознакомьтесь с предложением и примите или отклоните его.", "progressStep": 4, "totalSteps": 6, "...": "..." },
  "availableActions": ["acceptOffer", "declineOffer"],
  "decision": {
    "type": "Approved",
    "isFinal": true,
    "decidedBy": "Auto",
    "isSimulated": false,
    "recommendedType": null,
    "approvedAmount": 1500000, "approvedTermMonths": 12, "interestRate": 19.5, "monthlyPayment": 138593.06,
    "conditions": null,
    "reasons": [
      { "code": "risk_grade", "title": "Грейд риска A, 95 баллов", "explanation": "Низкий уровень риска: финансовое положение устойчивое." },
      { "code": "factor_credit_history", "title": "Кредитная история", "explanation": "Просрочек по кредитам за два года нет." }
    ],
    "decidedAt": "2026-10-20T17:47:02.1+00:00"
  },
  "offer": {
    "id": "...", "amount": 1500000, "termMonths": 12, "interestRate": 19.5, "monthlyPayment": 138593.06,
    "totalPayment": 1663116.74, "totalInterest": 163116.74,
    "expiresAt": "2026-10-27T17:47:02.1+00:00", "response": "Pending", "respondedAt": null
  },
  "contract": null,
  "disbursement": null,
  "scoring": null,
  "checks": null
}
```

`offer` — фактические условия, которые клиент принимает или отклоняет; карточка решения до принятия строится по нему:

| Поле `offer` | Что означает |
|---|---|
| `amount`, `termMonths`, `interestRate` | одобренная сумма, ₽, срок, мес., и годовая ставка, %; при `ApprovedWithConditions` отличаются от запрошенных |
| `monthlyPayment` | аннуитетный ежемесячный платёж, ₽ |
| `totalPayment`, `totalInterest` | для срочных кредитов (оборотный, экспресс, инвестиционный): сумма всех платежей по аннуитетному графику и переплата за весь срок, ₽; считаются по тем же правилам, что и в договоре, поэтому после принятия `contract.totalPayment` и `contract.totalInterest` равны им. Для овердрафта и кредитной линии `null`: проценты начисляются за дни фактического использования лимита, итог заранее не известен |
| `expiresAt` | до какого момента предложение действует (7 дней); после него `response` = `Expired`, заявка закрывается |
| `response`, `respondedAt` | `Pending`, `Accepted`, `Declined` или `Expired` и момент ответа клиента |

Цифры `cost` из предварительного подбора (`POST /api/financing/preview`) — ориентир по базовой ставке продукта до проверки
заявки; фактические условия только здесь, в `offer`: ставка зависит от оценки риска, сумма может быть снижена.

После принятия предложения в ответе появляется `contract` (краткая карточка договора), после выдачи `disbursement`;
поля и сценарий описаны в [completion-flow.md](completion-flow.md).

| Тип решения | Статус заявки | Что в ответе |
|---|---|---|
| `Approved` | Approved | `offer` на запрошенных условиях |
| `ApprovedWithConditions` | ApprovedWithConditions | `offer` с новыми условиями, `decision.conditions` объясняет изменения, в `reasons` есть `amount_reduced` |
| `Rejected` | Rejected | `offer` = null, `reasons` перечисляют стоп-факторы или причину по грейду |
| `ManualReview` | ManualReview | `isFinal` = false, `recommendedType` показывает рекомендацию автоматики менеджеру. В демо-режиме через несколько секунд появляется второе решение с `decidedBy` = `Manager` и `isSimulated` = `true` (ответ менеджера смоделирован, причина `demo_manager`): одобрение с предложением или окончательный отказ, см. `decision-logic.md` |

Сотрудник банка в том же методе дополнительно получает `scoring` и `checks` (результаты ЕГРЮЛ, ФНС, БКИ, ФССП
с краткими выводами и флагами риска). Клиенту эти поля приходят как `null`.

```json
{
  "scoring": {
    "score": 95, "grade": "A", "probabilityOfDefault": 0.035,
    "factors": [
      { "code": "credit_history", "title": "Кредитная история", "impact": 15, "value": "без просрочек", "clientExplanation": "Просрочек по кредитам за два года нет." },
      { "code": "revenue_to_amount", "title": "Выручка к сумме кредита", "impact": 15, "value": "32.0x", "clientExplanation": "Запрошенная сумма небольшая относительно годовой выручки." }
    ],
    "warnings": [],
    "missingData": [],
    "modelVersion": "rules-0.3",
    "source": "External",
    "requestId": "01a1a7e9-5c0b-7d4e-9b2a-3f1c2d4e5f60",
    "durationMs": 48,
    "computedAt": "2026-10-22T08:15:31+00:00"
  }
}
```

| Поле | Что означает |
|---|---|
| `source` | `External`: посчитал сервис скоринга; `RulesFallback`: сервис был недоступен, посчитала встроенная модель; `Rules`: режим без внешнего сервиса |
| `modelVersion` | версия правил из ответа (`rules-0.3`: сервис и встроенная модель считают по одним правилам), сохраняется для воспроизводимости |
| `warnings` | предупреждения модели: `expenses_missing`, `net_profit_missing`, `credit_bureau_missing`, `external_scoring_unavailable` и т.п. |
| `missingData` | каких входов не хватало, пути полей запроса к скорингу: `company.financials.net_profit_rub`, `checks.credit_bureau` |
| `requestId`, `durationMs`, `computedAt` | идентификатор запроса к сервису (по нему расчёт ищется в логах сервиса), длительность и момент расчёта |

### POST /api/applications/{id}/offer/accept

Роль `Client`. Доступно в статусах Approved и ApprovedWithConditions, пока предложение действует (7 дней).
Тело не нужно. Ответ 200: заявка со статусом `ContractSigning`, `offer.response` = `Accepted`. Повтор даёт 409.

### POST /api/applications/{id}/offer/decline

Роль `Client`. Тело необязательно: `{ "reason": "Нашли предложение с меньшей ставкой" }`.
Ответ 200: заявка со статусом `OfferDeclined` (терминальный). Истёкшее предложение закрывается системой автоматически.

### Договор и выдача

Полный контракт в [completion-flow.md](completion-flow.md). Кратко:

| Метод | Путь | Роль | Назначение |
|---|---|---|---|
| GET | `/api/applications/{id}/contract` | все с доступом к заявке | договор: стороны, условия, подпись, график платежей; 404 до принятия предложения |
| GET | `/api/applications/{id}/contract/document` | все с доступом к заявке | печатная форма договора, `text/html` |
| POST | `/api/applications/{id}/contract/sign/request-code` | Client | код подтверждения в ленту событий (имитация SMS) и в `demoCode`; 409, если заявка не на подписании |
| POST | `/api/applications/{id}/contract/sign` | Client | тело `{ "code": "483921" }`; верный код даёт статус `Signed`, затем воркер выполняет выдачу и статус становится `Disbursed`; неверный код даёт 400 с `errors.code` |

### GET /api/applications?status=Submitted&page=1&pageSize=20

Страница заявок от новых к старым. Клиент видит свои компании, банк всё.

```json
{
  "items": [ { "id": "...", "number": "AC-000002", "status": { "code": "Cancelled", "title": "Отменена", "..." : "..." },
               "company": { "...": "..." }, "product": { "...": "..." }, "amount": 2000000, "termMonths": 24,
               "purpose": "Expansion", "purposeTitle": "Расширение бизнеса", "createdAt": "...", "updatedAt": "..." } ],
  "page": 1, "pageSize": 20, "totalCount": 2, "totalPages": 1
}
```

### POST /api/applications/{id}/cancel

Роль `Client`. Доступно, пока банк не принял решение, а также на этапе подписания договора (отказ от подписания). Тело необязательно:

```json
{ "reason": "Нашли другой источник" }
```

Ответ 200: заявка в формате `GET /api/applications/{id}` со статусом `Cancelled` и новой записью в `history`.
Повторная отмена даёт 409.

### GET /api/applications/statuses

Без аутентификации. Справочник всех статусов с текстами, см. `docs/state-machine.md`.

## Лента событий

Единственный канал уведомлений внутри продукта: email и SMS запрещены условиями кейса.
События создаются для всех представителей компании при каждой смене статуса заявки, решении и предложении;
менеджеры получают события о заявках, переданных на ручное рассмотрение.

### GET /api/feed?unreadOnly=false&page=1&pageSize=20

```json
{
  "items": [
    {
      "id": "...", "type": "OfferReady",
      "title": "Заявка AC-000001 одобрена",
      "body": "Банк готов выдать 1 500 000 ₽ на 12 мес. под 19,5 % годовых, платёж 138 593 ₽ в месяц. Предложение действует до 27.10.2026.",
      "applicationId": "...", "link": "/applications/...",
      "isRead": false, "createdAt": "2026-10-20T17:47:02+00:00"
    }
  ],
  "page": 1, "pageSize": 20, "totalCount": 3, "totalPages": 1
}
```

Типы: `StatusChanged`, `ClarificationRequested`, `OfferReady`, `Rejected`, `ManualReview`, `ContractReady`, `Disbursed`, `Recommendation`, `News`.

| Метод | Путь | Назначение |
|---|---|---|
| GET | `/api/feed/unread-count` | `{ "unread": 3 }` для бейджа |
| POST | `/api/feed/{id}/read` | отметить одно событие, возвращает его |
| POST | `/api/feed/read-all` | отметить все, возвращает число отмеченных |

## Обновления в реальном времени

SignalR-хаб `/hubs/applications`. Клиент получает события по заявкам своих компаний, сотрудник банка по всем.
Токен передаётся в query-параметре `access_token`, как принято для SignalR в браузере.

```js
import { HubConnectionBuilder } from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl("http://localhost:8080/hubs/applications", { accessTokenFactory: () => token })
  .withAutomaticReconnect()
  .build();

connection.on("applicationChanged", (e) => {
  // e: { applicationId, number, companyId, status, statusTitle, updatedAt }
  refetchApplication(e.applicationId);
});

await connection.start();
```

Если SignalR не подключён, достаточно опрашивать `GET /api/applications/{id}` раз в 2–3 секунды.

## Служебные

| Метод | Путь | Назначение |
|---|---|---|
| GET | /health/live | процесс жив |
| GET | /health/ready | база отвечает, JSON с проверками |
| GET | /swagger | Swagger UI |
| GET | /swagger/v1/swagger.json | OpenAPI 3.0 |
