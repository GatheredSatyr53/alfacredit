"""Тесты заглушки: python3 -m unittest discover -s scoring -p "test_*.py" -v"""

import copy
import json
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer

import app

GOOD_REQUEST = {
    "request_id": "01a15975-f14b-7636-a356-687331e9eb8d",
    "application": {
        "id": "01a15975-f14b-7636-a356-687331e9eb8d",
        "number": "AC-000001",
        "created_at": "2026-10-20T13:17:20Z",
        "amount_rub": 1500000,
        "term_months": 12,
        "purpose": "WorkingCapital",
        "estimated_monthly_payment_rub": 138593,
        "product": {"code": "working-capital", "type": "WorkingCapital", "base_rate_percent": 19.5, "requires_collateral": False, "auto_approval_limit_rub": 5000000},
        "collateral_offered": False,
    },
    "company": {
        "inn": "7701234567", "legal_form": "Ooo", "okved": "47.11", "region_code": "77", "registered_at": "2019-03-12",
        "business_age_months": 91, "tax_regime": "Osno", "employees_count": 24,
        "financials": {"period_months": 12, "revenue_rub": 48000000, "expenses_rub": 40800000, "net_profit_rub": 7200000, "monthly_debt_payments_rub": 150000},
        "history": {"previous_applications": 0, "previous_rejections": 0, "previous_loans_disbursed": 0},
    },
    "checks": {
        "egrul": {"status": "Active", "head_disqualified": False, "mass_address": False},
        "fns": {"tax_debt_rub": 0, "has_blocked_accounts": False, "reported_revenue_rub": 47300000},
        "credit_bureau": {"credit_history_months": 60, "active_loans": 1, "closed_loans": 3, "total_debt_rub": 1200000, "monthly_payments_rub": 150000, "max_overdue_days_24m": 0, "overdue_count_24m": 0, "has_write_offs": False},
        "fssp": {"open_proceedings": 0, "open_amount_rub": 0},
    },
}


class ModelTests(unittest.TestCase):
    def test_clean_company_gets_grade_a(self):
        result = app.score(app.validate(copy.deepcopy(GOOD_REQUEST)))
        self.assertEqual(result["score"], 95)
        self.assertEqual(result["risk_grade"], "A")
        self.assertEqual(result["model_version"], app.MODEL_VERSION)
        self.assertEqual(result["request_id"], GOOD_REQUEST["request_id"])
        self.assertEqual(result["missing_data"], [])
        self.assertEqual(result["warnings"], [])
        codes = [f["code"] for f in result["factors"]]
        self.assertIn("credit_history", codes)
        self.assertTrue(all(f["client_explanation"] for f in result["factors"]))
        impacts = [abs(f["impact"]) for f in result["factors"]]
        self.assertEqual(impacts, sorted(impacts, reverse=True))

    def test_hard_profile_gets_grade_e(self):
        payload = copy.deepcopy(GOOD_REQUEST)
        payload["application"]["amount_rub"] = 2000000
        payload["company"]["financials"].update({"revenue_rub": 14000000, "net_profit_rub": 100000, "monthly_debt_payments_rub": 600000})
        payload["checks"]["fns"] = {"tax_debt_rub": 700000, "has_blocked_accounts": True, "reported_revenue_rub": 13200000}
        payload["checks"]["credit_bureau"].update({"max_overdue_days_24m": 95, "overdue_count_24m": 3})
        payload["checks"]["fssp"] = {"open_proceedings": 2, "open_amount_rub": 1400000}
        result = app.score(app.validate(payload))
        self.assertEqual(result["risk_grade"], "E")
        by_code = {f["code"]: f for f in result["factors"]}
        self.assertEqual(by_code["credit_history"]["impact"], -30)
        self.assertEqual(by_code["tax_debt"]["impact"], -30)

    def test_missing_blocks_become_warnings_and_missing_data(self):
        payload = copy.deepcopy(GOOD_REQUEST)
        payload["checks"]["credit_bureau"] = None
        payload["company"]["financials"]["net_profit_rub"] = None
        result = app.score(app.validate(payload))
        self.assertIn("credit_bureau_missing", result["warnings"])
        self.assertIn("net_profit_missing", result["warnings"])
        self.assertIn("checks.credit_bureau", result["missing_data"])
        self.assertIn("company.financials.net_profit_rub", result["missing_data"])
        self.assertTrue(0 <= result["score"] <= 100)

    def test_validation_error_lists_fields(self):
        payload = copy.deepcopy(GOOD_REQUEST)
        payload["company"]["financials"]["revenue_rub"] = -1
        del payload["application"]["term_months"]
        with self.assertRaises(app.ValidationError) as ctx:
            app.validate(payload)
        self.assertIn("company.financials.revenue_rub", ctx.exception.details)
        self.assertIn("application.term_months", ctx.exception.details)
        with self.assertRaises(app.ValidationError) as ctx:
            app.validate({"application": {}})
        self.assertEqual(set(ctx.exception.details), {"request_id", "company", "checks"})


class HttpTests(unittest.TestCase):
    def setUp(self):
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), app.Handler)
        self.base = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()

    def test_health(self):
        with urllib.request.urlopen(self.base + "/health", timeout=5) as response:
            body = json.load(response)
        self.assertEqual(body["status"], "ok")
        self.assertEqual(body["model_version"], app.MODEL_VERSION)

    def test_score_roundtrip(self):
        request = urllib.request.Request(self.base + "/api/v1/score", data=json.dumps(GOOD_REQUEST).encode(), headers={"Content-Type": "application/json"}, method="POST")
        with urllib.request.urlopen(request, timeout=5) as response:
            body = json.load(response)
        self.assertEqual(body["risk_grade"], "A")
        self.assertEqual(body["request_id"], GOOD_REQUEST["request_id"])

    def test_invalid_json_and_unknown_path(self):
        request = urllib.request.Request(self.base + "/api/v1/score", data=b"{", headers={"Content-Type": "application/json"}, method="POST")
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            urllib.request.urlopen(request, timeout=5)
        self.assertEqual(ctx.exception.code, 400)
        self.assertEqual(json.load(ctx.exception)["error"], "validation")
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            urllib.request.urlopen(self.base + "/nope", timeout=5)
        self.assertEqual(ctx.exception.code, 404)


if __name__ == "__main__":
    unittest.main()
