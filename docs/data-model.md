# Модель данных

PostgreSQL 16, схема управляется миграциями EF Core (`src/AlfaCredit.Infrastructure/Persistence/Migrations`).
Имена таблиц и столбцов в snake_case, перечисления хранятся строками для читаемости в базе.
Идентификаторы: UUID v7 (упорядочены по времени, дружелюбны к индексам).

```mermaid
erDiagram
    users ||--o{ company_members : "участвует"
    companies ||--o{ company_members : "имеет"
    companies ||--o{ credit_applications : "подаёт"
    credit_products ||--o{ credit_applications : "выбран"
    users ||--o{ credit_applications : "создал"
    credit_applications ||--o{ application_status_history : "история"
    credit_applications ||--o{ processing_jobs : "задания конвейера"
    credit_applications ||--o{ external_checks : "проверки"
    credit_applications ||--o{ scoring_results : "скоринг"
    credit_applications ||--o{ decisions : "решения"
    decisions ||--o{ offers : "предложение"
    offers ||--o{ loan_contracts : "договор"
    loan_contracts ||--o{ disbursements : "выдача"
    credit_applications ||--o{ feed_items : "события"
    users ||--o{ feed_items : "лента"

    users {
        uuid id PK
        varchar login UK
        varchar password_hash
        varchar full_name
        varchar role "Client | Manager | Admin"
        bool is_active
        timestamptz created_at
    }
    companies {
        uuid id PK
        varchar inn UK "10 или 12 цифр"
        varchar ogrn
        varchar name
        varchar legal_form "Ooo | Ip"
        varchar okved
        date registered_at
        varchar address
        varchar head_name
        varchar tax_regime
        numeric annual_revenue
        numeric annual_expenses "null, если учёт не ведётся"
        numeric net_profit "null или отрицательная при убытке"
        numeric monthly_debt_payments
        int employees_count
        timestamptz created_at
    }
    company_members {
        uuid id PK
        uuid company_id FK
        uuid user_id FK
        varchar role "Owner | Director | Accountant | Employee"
        timestamptz joined_at
    }
    credit_products {
        uuid id PK
        varchar code UK
        varchar name
        varchar description
        varchar type
        numeric min_amount
        numeric max_amount
        int min_term_months
        int max_term_months
        numeric base_rate
        varchar allowed_purposes "список через запятую"
        int min_business_age_months
        bool requires_collateral
        numeric auto_approval_limit
        bool is_active
        int sort_order
    }
    credit_applications {
        uuid id PK
        varchar number UK "AC-000001, из sequence"
        uuid company_id FK
        uuid product_id FK
        uuid created_by_user_id FK
        numeric amount
        int term_months
        varchar purpose
        varchar purpose_description
        varchar status
        timestamptz created_at
        timestamptz updated_at
        xid xmin "оптимистическая блокировка"
    }
    processing_jobs {
        uuid id PK
        uuid application_id FK
        varchar stage "Validate | Enrich | Score | Decide"
        varchar status "Pending | Running | Done | Failed"
        int attempts
        int max_attempts
        timestamptz next_run_at "задержка между этапами"
        timestamptz lease_expires_at "аренда воркера, для восстановления после сбоя"
        varchar last_error
    }
    external_checks {
        uuid id PK
        uuid application_id FK
        varchar source "Egrul | Fns | CreditBureau | Fssp"
        varchar status "Succeeded | Failed"
        jsonb payload "сырой ответ источника"
        varchar summary
        varchar risk_flags
        int attempts
        timestamptz checked_at
    }
    scoring_results {
        uuid id PK
        uuid application_id FK
        uuid request_id "request_id запроса к сервису скоринга"
        int score "0..100"
        varchar grade "A..E"
        numeric probability_of_default
        jsonb factors "вклад и объяснение"
        varchar warnings "коды через запятую"
        varchar missing_data "пути отсутствовавших входов"
        varchar model_version
        varchar source "Rules | External | RulesFallback"
        int duration_ms "длительность расчёта"
        timestamptz computed_at
    }
    decisions {
        uuid id PK
        uuid application_id FK
        varchar type "Approved | ApprovedWithConditions | Rejected | ManualReview"
        varchar decided_by "Auto | Manager"
        uuid decided_by_user_id
        varchar recommended_type
        numeric approved_amount
        int approved_term_months
        numeric interest_rate
        numeric monthly_payment
        varchar conditions
        jsonb reasons
        bool is_final
        timestamptz decided_at
    }
    offers {
        uuid id PK
        uuid application_id FK
        uuid decision_id FK
        numeric amount
        int term_months
        numeric interest_rate
        numeric monthly_payment
        timestamptz expires_at
        varchar response "Pending | Accepted | Declined | Expired"
        timestamptz responded_at
    }
    loan_contracts {
        uuid id PK
        uuid application_id FK
        uuid offer_id FK
        varchar number UK "KD-000001, из последовательности"
        varchar status "AwaitingSignature | Signed | Expired | Cancelled"
        numeric amount
        int term_months
        numeric interest_rate
        numeric monthly_payment
        numeric total_payment
        numeric total_interest
        varchar lender_name
        varchar borrower_name
        varchar borrower_inn
        varchar disbursement_account "20 цифр, синтетический"
        timestamptz signing_deadline
        timestamptz signed_at
        uuid signed_by_user_id
        varchar signed_by_name
        varchar signature_method
        varchar confirmation_code "действующий код, имитация SMS"
        timestamptz code_expires_at
        int code_attempts
        int code_max_attempts
    }
    disbursements {
        uuid id PK
        uuid application_id FK
        uuid contract_id FK
        numeric amount
        varchar account
        varchar payment_order_number "из симулятора банковского ядра"
        varchar status "Completed | Failed"
        timestamptz requested_at
        timestamptz completed_at
        varchar error
    }
    feed_items {
        uuid id PK
        uuid user_id FK
        uuid application_id FK
        varchar type
        varchar title
        varchar body
        varchar link
        bool is_read
        timestamptz created_at
        timestamptz read_at
    }
    application_status_history {
        uuid id PK
        uuid application_id FK
        varchar from_status "null для создания"
        varchar to_status
        varchar actor_type "Client | Manager | System"
        uuid actor_user_id "null для системы"
        varchar comment
        timestamptz occurred_at
    }
```

## Замечания по проектированию

- **Номер заявки** генерируется базой из последовательности `application_number_seq` при вставке,
  поэтому он уникален при любом числе экземпляров API.
- **Оптимистическая блокировка** через системный столбец `xmin`: при одновременном изменении одной заявки
  второй запрос получает конфликт, а не затирает решение первого.
- **История статусов** пишется при каждом переходе и служит одновременно таймлайном для клиента и аудитом для банка.
- **Цели продукта** хранятся строкой `WorkingCapital,Other`: справочник маленький и стабильный, отдельная таблица не нужна.
- **Индексы**: `credit_applications (company_id, created_at)` для кабинета клиента, `credit_applications (status)` для очереди банка,
  `application_status_history (application_id, occurred_at)` для таймлайна.
- **Очередь конвейера** в базе, а не в памяти: задания переживают рестарт, несколько воркеров берут их через
  `FOR UPDATE SKIP LOCKED` без дублей, поле `next_run_at` задаёт паузу между этапами.
- **Сырые ответы источников** хранятся в `jsonb` для аудита; типизированный снимок собирается из последних успешных проверок.
- **Решения** накапливаются: автоматическое с типом `ManualReview` хранит рекомендацию, менеджер позже добавит окончательное.
- Следующие этапы добавят таблицы документов, запросов на уточнение, договоров и выдач; см. `backend-plan.md`.
