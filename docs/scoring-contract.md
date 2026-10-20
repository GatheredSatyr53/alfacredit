# Контракт с сервисом скоринга

Сервис скоринга (Python, отдельный контейнер в docker compose) **заменяет расчёт на этапе Scoring**.
Он возвращает оценку риска и объяснение, а кредитное решение остаётся в backend на C#: стоп-факторы,
лимиты продукта, политика по грейдам (`docs/decision-logic.md`). Если сервис недоступен, backend
использует встроенную балльную модель и помечает результат как `fallback-rules`, чтобы демо никогда не блокировалось.

## Как backend вызывает сервис

| Что | Значение |
|---|---|
| Адрес | `POST http://scoring:8000/api/v1/score` (имя сервиса в compose `scoring`, порт `8000`) |
| Health | `GET http://scoring:8000/health` отвечает 200 и `{"status":"ok","model_version":"..."}` |
| Формат | JSON, UTF-8, поля в `snake_case` |
| Таймаут | 5 секунд на запрос, затем 2 повтора с паузой 1 и 2 секунды |
| Ошибки | 400 с описанием, если payload невалиден; 5xx или таймаут означают fallback на встроенные правила |
| Идемпотентность | `request_id` уникален для попытки; повтор с тем же `request_id` можно обслужить из кэша |

Сервис не принимает решений и не читает базу: всё, что ему нужно, приходит в запросе.

## Правила для данных

- **Единицы**: деньги в рублях, числами без форматирования (`1500000`, не `"1 500 000"` и не тысячи).
  Годовые показатели за последние 12 месяцев, месячные за календарный месяц; период всегда в названии поля.
- **Пропуски**: поле присутствует со значением `null`. `NaN` и `Infinity` в JSON недопустимы и не будут отправляться.
  Если внешняя проверка не получена целиком, `null` стоит на месте всего блока (`"credit_bureau": null`).
- **Даты**: ISO 8601, даты без времени `2019-03-12`, метки времени в UTC `2026-10-20T13:17:20Z`.
- **Перечисления**: строки как в API backend (см. `docs/api.md`): `legal_form` = `Ooo | Ip`,
  `tax_regime` = `Osno | UsnIncome | UsnIncomeExpenses | Patent`,
  `product_type` = `WorkingCapital | Investment | Overdraft | CreditLine | Express`,
  `purpose` = `WorkingCapital | Equipment | Expansion | Refinancing | Other`.
- **Оценка**: `score` целое 0..100, чем выше, тем ниже риск; `risk_grade` буква A..E по шкале ниже.
  Backend использует грейд, `score` нужен для сортировки очереди и графиков.

| Грейд | score | Что делает backend |
|---|---|---|
| A | 80..100 | автоодобрение на запрошенных условиях, если сумма в лимите продукта |
| B | 65..79 | автоодобрение, ставка +1.5 п.п. |
| C | 50..64 | одобрение с изменёнными условиями: сумма до 25 % годовой выручки, ставка +3 п.п. |
| D | 35..49 | ручное рассмотрение менеджером |
| E | 0..34 | отказ |

Сервис может вернуть грейд, не совпадающий с таблицей по `score`, если модель так считает; backend доверяет грейду.

## Запрос

```json
{
  "request_id": "01a15975-f14b-7636-a356-687331e9eb8d",
  "application": {
    "id": "01a15975-f14b-7636-a356-687331e9eb8d",
    "number": "AC-000001",
    "created_at": "2026-10-20T13:17:20Z",
    "amount_rub": 1500000,
    "term_months": 12,
    "purpose": "WorkingCapital",
    "estimated_monthly_payment_rub": 138600,
    "product": {
      "code": "working-capital",
      "type": "WorkingCapital",
      "base_rate_percent": 19.5,
      "requires_collateral": false,
      "auto_approval_limit_rub": 5000000
    },
    "collateral_offered": false
  },
  "company": {
    "inn": "7701234567",
    "legal_form": "Ooo",
    "okved": "47.11",
    "region_code": "77",
    "registered_at": "2019-03-12",
    "business_age_months": 91,
    "tax_regime": "Osno",
    "employees_count": 24,
    "financials": {
      "period_months": 12,
      "revenue_rub": 48000000,
      "expenses_rub": 40800000,
      "net_profit_rub": 7200000,
      "monthly_debt_payments_rub": 150000
    },
    "history": {
      "previous_applications": 0,
      "previous_rejections": 0,
      "previous_loans_disbursed": 0
    }
  },
  "checks": {
    "egrul": {
      "status": "Active",
      "head_disqualified": false,
      "mass_address": false
    },
    "fns": {
      "tax_debt_rub": 0,
      "has_blocked_accounts": false,
      "reported_revenue_rub": 47300000
    },
    "credit_bureau": {
      "credit_history_months": 60,
      "active_loans": 1,
      "closed_loans": 3,
      "total_debt_rub": 1200000,
      "monthly_payments_rub": 150000,
      "max_overdue_days_24m": 0,
      "overdue_count_24m": 0,
      "has_write_offs": false
    },
    "fssp": {
      "open_proceedings": 0,
      "open_amount_rub": 0
    }
  }
}
```

| Поле | Тип | Описание |
|---|---|---|
| `application.amount_rub`, `term_months`, `purpose` | int, int, enum | что просит клиент |
| `application.estimated_monthly_payment_rub` | int | аннуитетный платёж по базовой ставке продукта, считает backend |
| `application.product.*` | | параметры продукта: тип, базовая ставка, залог, лимит автоодобрения |
| `application.collateral_offered` | bool | предложил ли клиент залог (пока всегда `false`) |
| `company.region_code` | string | первые две цифры ИНН |
| `company.business_age_months` | int | полных месяцев с даты регистрации |
| `company.financials.*` | int или null | выручка, расходы, чистая прибыль за 12 месяцев, текущие платежи по кредитам в месяц |
| `company.history.*` | int | заявки, отказы и выданные кредиты этого клиента в нашей системе |
| `checks.egrul.status` | enum | `Active`, `Liquidating`, `Terminated` |
| `checks.fns.*` | | налоговая задолженность, блокировка счетов, выручка по отчётности ФНС |
| `checks.credit_bureau.*` | | кредитная история: обязательства, платежи, просрочки за 24 месяца, списания |
| `checks.fssp.*` | | открытые исполнительные производства и их сумма |

Любой блок в `checks` может быть `null`, если источник не ответил. Backend тогда помечает заявку
«проверка недоступна», но скоринг всё равно запрашивает.

## Ответ

```json
{
  "request_id": "01a15975-f14b-7636-a356-687331e9eb8d",
  "model_version": "lgbm-2026.10.21",
  "score": 78,
  "risk_grade": "B",
  "probability_of_default": 0.06,
  "factors": [
    {
      "code": "debt_load",
      "title": "Долговая нагрузка",
      "impact": -8,
      "value": "0.48",
      "client_explanation": "Платежи по кредитам занимают около половины месячной прибыли."
    },
    {
      "code": "business_age",
      "title": "Возраст бизнеса",
      "impact": 10,
      "value": "7 лет 7 мес.",
      "client_explanation": "Компания работает больше трёх лет."
    },
    {
      "code": "credit_history",
      "title": "Кредитная история",
      "impact": 15,
      "value": "без просрочек",
      "client_explanation": "Просрочек по кредитам за два года нет."
    }
  ],
  "warnings": []
}
```

| Поле | Тип | Описание |
|---|---|---|
| `score` | int 0..100 | обязательно |
| `risk_grade` | `A`..`E` | обязательно |
| `probability_of_default` | float 0..1 или null | желательно, показывается менеджеру |
| `factors` | массив | 3..7 главных факторов по убыванию модуля `impact`; `impact` в баллах со знаком, `value` строка для карточки, `client_explanation` фраза для клиента простым языком |
| `warnings` | массив строк | какие входы отсутствовали и чем заменены, например `credit_bureau_missing` |
| `model_version` | string | сохраняется в заявке для воспроизводимости |

Ответ с ошибкой валидации:

```json
{ "error": "validation", "details": { "company.financials.revenue_rub": "must be >= 0" } }
```

## Примеры из демо-данных

Эти пять компаний создаются seed'ом при старте backend; по ним стоит собирать синтетическую выборку и тесты.

| Клиент | Компания | Входы | Ожидаемый результат |
|---|---|---|---|
| `client_good` | ООО «Ромашка», с 2019, выручка 48 млн, прибыль 7.2 млн, платежи 150 тыс/мес | проверки чистые | A или B, автоодобрение |
| `client_medium` | ИП Кузнецов, с 2023, выручка 9.6 млн, прибыль 2.4 млн, платежи 120 тыс/мес | одна просрочка до 30 дней в БКИ | C, одобрение с изменением условий |
| `client_manual` | ООО «Вектор», с 2016, выручка 180 млн, прибыль 16 млн, платежи 1.2 млн/мес | чистые, но сумма заявки выше лимита автоодобрения | B..D, ручное рассмотрение |
| `client_bad` | ООО «Закат», с 2020, выручка 14 млн, прибыль 0.1 млн, платежи 600 тыс/мес | просрочка 95 дней, налоговый долг 700 тыс, ФССП | E; backend откажет по стоп-факторам ещё до вызова |
| `client_new` | ИП Петрова, с июня 2026, выручка 1.2 млн, расходы и прибыль `null` | до скоринга не доходит: не проходит возраст бизнеса продукта | пример пропусков в данных |

Значения проверок (`checks`) для этих компаний выдают симуляторы backend детерминированно по ИНН,
точные цифры появятся вместе с симуляторами на этапе 2 и будут добавлены сюда.

## Что нужно от сервиса для сдачи

- Dockerfile и сервис `scoring` в `docker-compose.yml`, без платных API и закрытых моделей.
- `GET /health` для healthcheck, запуск без GPU на обычном ПК.
- Модель с открытыми весами, если используется LLM; классический ML без ограничений.
- Версии зависимостей зафиксированы (`requirements.txt` с версиями или lock-файл).
- Описание признаков и логики в `README` сервиса: жюри оценивает объяснимость.
