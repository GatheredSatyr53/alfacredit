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
- Следующие этапы добавят таблицы документов, запросов на уточнение, внешних проверок, скоринга, решений, договоров,
  выдач, ленты событий и очереди заданий конвейера; см. `backend-plan.md`.
