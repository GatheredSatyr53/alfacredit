"""Заглушка сервиса скоринга по контракту docs/scoring-contract.md.

Зачем: стенд (db + api + scoring) поднимается одной командой уже сейчас, backend проверяет интеграцию
с настоящим HTTP-сервисом, а жюри видит полный путь заявки. Логика повторяет встроенную балльную модель
backend (rules-v1), поэтому демо-сценарии дают те же грейды, что и без сервиса.

Заменяется сервисом ML-команды: достаточно сохранить порт 8000, GET /health и формат POST /api/v1/score.
Только стандартная библиотека Python, внешних зависимостей нет.
"""

from __future__ import annotations

import json
import logging
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any

MODEL_VERSION = "stub-rules-1.0"
BASE_SCORE = 50
MAX_BODY_BYTES = 1_000_000
REQUIRED_SECTIONS = ("request_id", "application", "company", "checks")

log = logging.getLogger("scoring")


class ValidationError(Exception):
    """Невалидный запрос: details содержит путь поля и описание проблемы."""

    def __init__(self, details: dict[str, str]) -> None:
        super().__init__("validation")
        self.details = details


# ---------- проверка запроса ----------

def _number(value: Any, path: str, details: dict[str, str], *, required: bool = True, minimum: float | None = 0) -> float | None:
    if value is None:
        if required:
            details[path] = "is required"
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        details[path] = "must be a number"
        return None
    if minimum is not None and value < minimum:
        details[path] = f"must be >= {minimum:g}"
        return None
    return float(value)


def validate(payload: Any) -> dict[str, Any]:
    """Проверяет структуру и обязательные поля запроса. Возвращает payload или бросает ValidationError."""
    if not isinstance(payload, dict):
        raise ValidationError({"body": "must be a JSON object"})

    details: dict[str, str] = {key: "is required" for key in REQUIRED_SECTIONS if key not in payload}
    if details:
        raise ValidationError(details)

    for key in ("application", "company", "checks"):
        if not isinstance(payload[key], dict):
            details[key] = "must be an object"
    if details:
        raise ValidationError(details)

    application = payload["application"]
    company = payload["company"]
    financials = company.get("financials")
    if not isinstance(financials, dict):
        raise ValidationError({"company.financials": "must be an object"})

    _number(application.get("amount_rub"), "application.amount_rub", details, minimum=1)
    _number(application.get("term_months"), "application.term_months", details, minimum=1)
    _number(application.get("estimated_monthly_payment_rub"), "application.estimated_monthly_payment_rub", details)
    _number(company.get("business_age_months"), "company.business_age_months", details)
    _number(financials.get("revenue_rub"), "company.financials.revenue_rub", details)
    _number(financials.get("expenses_rub"), "company.financials.expenses_rub", details, required=False)
    _number(financials.get("net_profit_rub"), "company.financials.net_profit_rub", details, required=False, minimum=None)
    _number(financials.get("monthly_debt_payments_rub"), "company.financials.monthly_debt_payments_rub", details)
    if details:
        raise ValidationError(details)
    return payload


def missing_data(payload: dict[str, Any]) -> list[str]:
    """Пути входов, которые пришли как null: backend показывает их менеджеру."""
    financials = payload["company"].get("financials") or {}
    checks = payload["checks"]
    missing: list[str] = []
    if financials.get("expenses_rub") is None:
        missing.append("company.financials.expenses_rub")
    if financials.get("net_profit_rub") is None:
        missing.append("company.financials.net_profit_rub")
    for block in ("egrul", "fns", "credit_bureau", "fssp"):
        if checks.get(block) is None:
            missing.append(f"checks.{block}")
    fns = checks.get("fns")
    if isinstance(fns, dict) and fns.get("reported_revenue_rub") is None:
        missing.append("checks.fns.reported_revenue_rub")
    return missing


# ---------- модель ----------

def factor(code: str, title: str, impact: int, value: str, explanation: str) -> dict[str, Any]:
    return {"code": code, "title": title, "impact": impact, "value": value, "client_explanation": explanation}


def format_months(months: int) -> str:
    years, rest = divmod(months, 12)
    if years == 0:
        return f"{rest} мес."
    return f"{years} г." if rest == 0 else f"{years} г. {rest} мес."


def format_rub(value: float) -> str:
    return f"{int(round(value)):,}".replace(",", " ") + " ₽"


def grade_for(score: int) -> str:
    if score >= 80:
        return "A"
    if score >= 65:
        return "B"
    if score >= 50:
        return "C"
    if score >= 35:
        return "D"
    return "E"


def score(payload: dict[str, Any]) -> dict[str, Any]:
    """Балльная модель: старт 50, вклады факторов, итог 0..100. Каждому фактору соответствует фраза для клиента."""
    application = payload["application"]
    company = payload["company"]
    financials = company["financials"]
    checks = payload["checks"]
    factors: list[dict[str, Any]] = []
    warnings: list[str] = []

    # 1. Возраст бизнеса
    age = int(company["business_age_months"])
    age_impact = -20 if age < 6 else -10 if age < 12 else 0 if age < 36 else 10
    factors.append(factor(
        "business_age", "Возраст бизнеса", age_impact, format_months(age),
        "Компания работает больше трёх лет, это снижает риск." if age_impact >= 10
        else "Компания работает от года до трёх лет." if age_impact == 0
        else "Бизнес молодой, банк относится к таким заявкам осторожнее."))

    # 2. Выручка к запрошенной сумме
    amount = float(application["amount_rub"])
    revenue = float(financials["revenue_rub"])
    ratio = revenue / amount if amount > 0 else 0.0
    ratio_impact = 15 if ratio >= 4 else 5 if ratio >= 2 else -5 if ratio >= 1 else -20
    factors.append(factor(
        "revenue_to_amount", "Выручка к сумме кредита", ratio_impact, f"{ratio:.1f}x",
        "Запрошенная сумма небольшая относительно годовой выручки." if ratio_impact > 0
        else "Запрошенная сумма велика относительно годовой выручки."))

    # 3. Долговая нагрузка: платежи по кредитам к месячной прибыли
    net_profit = financials.get("net_profit_rub")
    if net_profit is None:
        monthly_profit = revenue * 0.10 / 12
        warnings.append("net_profit_missing")
    else:
        monthly_profit = float(net_profit) / 12
    payments = float(financials["monthly_debt_payments_rub"]) + float(application["estimated_monthly_payment_rub"])
    load = 9.99 if monthly_profit <= 0 else payments / monthly_profit
    load_impact = 10 if load < 0.3 else 0 if load < 0.5 else -10 if load < 0.7 else -25
    factors.append(factor(
        "debt_load", "Долговая нагрузка", load_impact, "прибыль отсутствует" if load >= 9.99 else f"{load:.2f}",
        "Платежи по кредитам занимают небольшую часть прибыли." if load_impact > 0
        else "Платежи по кредитам занимают около половины месячной прибыли." if load_impact == 0
        else "Платежи по кредитам с учётом нового займа занимают слишком большую часть прибыли."))

    # 4. Кредитная история
    bureau = checks.get("credit_bureau")
    if isinstance(bureau, dict):
        overdue = int(bureau.get("max_overdue_days_24m") or 0)
        no_history = not bureau.get("credit_history_months") and not bureau.get("active_loans") and not bureau.get("closed_loans")
        if no_history:
            factors.append(factor("credit_history", "Кредитная история", 0, "нет истории", "Кредитной истории пока нет, это не плюс и не минус."))
        elif bureau.get("has_write_offs") or overdue > 90:
            factors.append(factor("credit_history", "Кредитная история", -30, f"просрочка {overdue} дн.", "В кредитной истории есть длительные просрочки или списания."))
        else:
            history_impact = 15 if overdue == 0 else -5 if overdue <= 30 else -15
            factors.append(factor(
                "credit_history", "Кредитная история", history_impact,
                "без просрочек" if overdue == 0 else f"просрочка до {overdue} дн.",
                "Просрочек по кредитам за два года нет." if history_impact > 0 else "За последние два года были просрочки по кредитам."))
    else:
        warnings.append("credit_bureau_missing")

    # 5. Налоговая задолженность и блокировки
    fns = checks.get("fns")
    if isinstance(fns, dict):
        tax_debt = float(fns.get("tax_debt_rub") or 0)
        tax_impact = 5 if tax_debt <= 0 else -5 if tax_debt < 50_000 else -15
        blocked = bool(fns.get("has_blocked_accounts"))
        if blocked:
            tax_impact -= 15
        factors.append(factor(
            "tax_debt", "Налоговая задолженность", tax_impact, "нет" if tax_debt <= 0 else format_rub(tax_debt),
            "Задолженности перед налоговой нет." if tax_impact > 0
            else "Есть налоговая задолженность и заблокированные счета." if blocked
            else "Есть задолженность перед налоговой."))
    else:
        warnings.append("fns_missing")

    # 6. Исполнительные производства
    fssp = checks.get("fssp")
    if isinstance(fssp, dict):
        open_proceedings = int(fssp.get("open_proceedings") or 0)
        fssp_impact = -10 if open_proceedings > 0 else 0
        factors.append(factor(
            "enforcement", "Исполнительные производства", fssp_impact,
            "нет" if open_proceedings == 0 else f"{open_proceedings} на {format_rub(float(fssp.get('open_amount_rub') or 0))}",
            "Открытых исполнительных производств нет." if fssp_impact == 0 else "Есть открытые исполнительные производства."))
    else:
        warnings.append("fssp_missing")

    # 7. Залог
    if application.get("collateral_offered"):
        factors.append(factor("collateral", "Обеспечение", 10, "предложено", "Предложенный залог снижает риск банка."))

    total = max(0, min(100, BASE_SCORE + sum(f["impact"] for f in factors)))
    factors.sort(key=lambda f: (-abs(f["impact"]), f["code"]))
    return {
        "request_id": payload["request_id"],
        "model_version": MODEL_VERSION,
        "score": total,
        "risk_grade": grade_for(total),
        "probability_of_default": round(0.02 + (100 - total) / 100 * 0.30, 3),
        "factors": factors[:7],
        "warnings": warnings,
        "missing_data": missing_data(payload),
    }


# ---------- HTTP ----------

class Handler(BaseHTTPRequestHandler):
    server_version = "scoring-stub/1.0"
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt: str, *args: Any) -> None:  # noqa: A003 - сигнатура базового класса
        log.info("%s %s", self.address_string(), fmt % args)

    def do_GET(self) -> None:  # noqa: N802 - имя задаёт http.server
        if self.path == "/health":
            self._json(200, {"status": "ok", "model_version": MODEL_VERSION})
        else:
            self._json(404, {"error": "not_found"})

    def do_POST(self) -> None:  # noqa: N802
        if self.path != "/api/v1/score":
            self._json(404, {"error": "not_found"})
            return
        try:
            raw = self._read_body()
            payload = json.loads(raw.decode("utf-8"))
            result = score(validate(payload))
        except ValidationError as error:
            self._json(400, {"error": "validation", "details": error.details})
            return
        except (ValueError, UnicodeDecodeError):
            self._json(400, {"error": "validation", "details": {"body": "must be valid JSON"}})
            return
        log.info("request %s: %s points, grade %s", result["request_id"], result["score"], result["risk_grade"])
        self._json(200, result)

    def _read_body(self) -> bytes:
        if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower():
            chunks: list[bytes] = []
            total = 0
            while True:
                size = int(self.rfile.readline().split(b";")[0].strip() or b"0", 16)
                if size == 0:
                    while self.rfile.readline() not in (b"\r\n", b"\n", b""):
                        pass
                    return b"".join(chunks)
                total += size
                if total > MAX_BODY_BYTES:
                    raise ValueError("body too large")
                chunks.append(self.rfile.read(size))
                self.rfile.readline()
        length = int(self.headers.get("Content-Length") or 0)
        if length <= 0 or length > MAX_BODY_BYTES:
            raise ValueError("body is required")
        return self.rfile.read(length)

    def _json(self, status: int, body: dict[str, Any]) -> None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


def main() -> None:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    port = int(os.environ.get("PORT", "8000"))
    server = ThreadingHTTPServer(("0.0.0.0", port), Handler)
    log.info("scoring stub %s listening on :%d", MODEL_VERSION, port)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
