# API кредитного конвейера

Базовый адрес при запуске через Docker Compose: `http://localhost:8080`, при `dotnet run`: `http://localhost:5080`.
Интерактивная документация: `/swagger`, машиночитаемый контракт: `/swagger/v1/swagger.json` (копия в `docs/openapi.json`).
Коллекции запросов: `docs/requests.http`, `docs/postman_collection.json`.

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
строятся по нему. Сейчас реализовано `cancel`, остальные появятся вместе с конвейером и решениями банка.

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

Роль `Client`. Доступно, пока банк не принял решение. Тело необязательно:

```json
{ "reason": "Нашли другой источник" }
```

Ответ 200: заявка в формате `GET /api/applications/{id}` со статусом `Cancelled` и новой записью в `history`.
Повторная отмена даёт 409.

### GET /api/applications/statuses

Без аутентификации. Справочник всех статусов с текстами, см. `docs/state-machine.md`.

## Служебные

| Метод | Путь | Назначение |
|---|---|---|
| GET | /health/live | процесс жив |
| GET | /health/ready | база отвечает, JSON с проверками |
| GET | /swagger | Swagger UI |
| GET | /swagger/v1/swagger.json | OpenAPI 3.0 |
