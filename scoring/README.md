# Сервис скоринга: заглушка по контракту

Отдельный контейнер `scoring` в `docker-compose.yml`. Реализует контракт [docs/scoring-contract.md](../docs/scoring-contract.md):

- `GET /health` → `{"status": "ok", "model_version": "stub-rules-1.0"}`;
- `POST /api/v1/score` → балл, грейд, факторы с объяснениями, `warnings`, `missing_data`, `model_version`;
- `400 {"error": "validation", "details": {...}}` при невалидном запросе.

Это **заглушка**: логика повторяет встроенную балльную модель backend (`rules-v1`, см. `docs/decision-logic.md`),
поэтому демо-сценарии дают те же грейды. Она нужна, чтобы стенд поднимался одной командой и интеграция
backend ↔ скоринг была проверена до появления ML-модели.

## Как заменить на ML-сервис

Сервис ML-команды кладётся в эту же папку и сохраняет:

- порт `8000`, пути `GET /health` и `POST /api/v1/score`, JSON в `snake_case`;
- формат ответа из контракта (`score`, `risk_grade`, `factors`, `warnings`, `missing_data`, `model_version`);
- `Dockerfile` с точной версией базового образа и `requirements.txt` с зафиксированными версиями;
- запуск без GPU; при использовании LLM только модели с открытыми весами.

Backend ничего менять не нужно: адрес берётся из `Scoring__BaseUrl` (`http://scoring:8000`).

## Запуск

```bash
python3 scoring/app.py                     # локально, порт 8000 (PORT переопределяет)
python3 -m unittest discover -s scoring -p "test_*.py" -v
docker compose up --build scoring          # в составе стенда
```

Проверка вручную:

```bash
curl -s localhost:8000/health
curl -s -X POST localhost:8000/api/v1/score -H 'Content-Type: application/json' -d @- <<'JSON'
{"request_id":"demo-1","application":{"amount_rub":1500000,"term_months":12,"estimated_monthly_payment_rub":138593,"collateral_offered":false},
 "company":{"business_age_months":91,"financials":{"revenue_rub":48000000,"expenses_rub":40800000,"net_profit_rub":7200000,"monthly_debt_payments_rub":150000}},
 "checks":{"egrul":{"status":"Active"},"fns":{"tax_debt_rub":0,"has_blocked_accounts":false,"reported_revenue_rub":47300000},
           "credit_bureau":{"credit_history_months":60,"active_loans":1,"closed_loans":3,"max_overdue_days_24m":0,"has_write_offs":false},"fssp":{"open_proceedings":0,"open_amount_rub":0}}}
JSON
```
