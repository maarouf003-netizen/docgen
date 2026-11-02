#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""دخان حيّ **قراءة فقط** لمسارات التنبيهات (بعد `PR‑6c`).

يُثبت على البيئة الحيّة ما يُثبته الاختبار محليًا: قوائم `/api/alerts` تعمل،
والمؤشر (`cursor`) يكمل من بعد آخر صف بلا تكرار، والصيغة غير الصالحة تُرفض
بـ`400` عربي **بلا ذيل إطار عمل إنجليزي**، و`cursor` مع `page=0` يُرفض أيضًا.

الخصائص:
- **لا يكتب شيئًا إطلاقًا** (لا `POST`/`PATCH`/`DELETE`؛ `POST /auth/login` وحده).
- كلمة المرور لا تُمرَّر في سطر الأمر افتراضيًا: تُقرأ من `DOCGEN_SMOKE_PASSWORD`
  أو من إدخال مخفي (`getpass`) — فلا تدخل سجل الصدفة ولا المحادثة.
- بلا بيانات اعتماد يكتفي بالفحوص العامة (healthz/SPA/401) ويعيد `0` إن نجحت.

الاستخدام:

    python scripts/alerts-live-smoke.py
    python scripts/alerts-live-smoke.py --username head1
    DOCGEN_SMOKE_PASSWORD=... python scripts/alerts-live-smoke.py --username head1

كود الخروج: `0` نجاح (أو تجاوز مشروع)، `1` فشل فحص.
"""

from __future__ import annotations

import argparse
import getpass
import http.cookiejar
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_BASE_URL = "https://docgen-1-tt3j.onrender.com"

# الرسالة المتوقعة عند مؤشر غير صالح — مطابقة لـ`AlertCursorException` في الخادم.
INVALID_CURSOR_MESSAGE = "قيمة cursor غير صالحة — أعِد تحميل القائمة ثم أعد المحاولة"
INVALID_PAGE_MESSAGE = "رقم الصفحة يبدأ من 1"


class Report:
    """عدّاد نتائج مع طباعة فورية (PASS/FAIL/SKIP) ورمز خروج حاسم."""

    def __init__(self) -> None:
        self.passed = 0
        self.failed = 0
        self.skipped = 0
        self.failures: list[str] = []

    def check(self, name: str, condition: bool, detail: str = "") -> bool:
        if condition:
            self.passed += 1
            print(f"  [PASS] {name}")
            return True
        self.failed += 1
        self.failures.append(f"{name} — {detail}" if detail else name)
        print(f"  [FAIL] {name}" + (f" -- {detail}" if detail else ""))
        return False

    def skip(self, name: str, reason: str) -> None:
        self.skipped += 1
        print(f"  [SKIP] {name} ({reason})")

    def step(self, title: str) -> None:
        print(f"\n== {title}")


def http_call(opener, method: str, url: str, body: dict | None = None) -> tuple[int, str]:
    """ينفّذ طلبًا واحدًا ويعيد (الحالة، النص) — بلا رمي على 4xx/5xx."""
    data = json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None
    request = urllib.request.Request(url, data=data, method=method)
    request.add_header("Accept", "application/json, text/html")
    if data is not None:
        request.add_header("Content-Type", "application/json; charset=utf-8")
    try:
        with opener.open(request, timeout=60) as response:
            return response.status, response.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", "replace")
    except urllib.error.URLError as error:
        return 0, f"URLError: {error.reason}"


def parse_json(text: str) -> dict | list | None:
    try:
        return json.loads(text)
    except (ValueError, TypeError):
        return None


def message_of(text: str) -> str:
    body = parse_json(text)
    return body.get("message", "") if isinstance(body, dict) else ""


def contains_english_suffix(text: str) -> bool:
    """ذيل إطار العمل الذي يجب ألا يصل المستخدم (`PR‑6c`)."""
    lowered = text.lower()
    return "parameter" in lowered or "actual value" in lowered


def public_checks(report: Report, base_url: str, opener) -> None:
    report.step("0) فحوص عامة بلا مصادقة")

    status, body = http_call(opener, "GET", f"{base_url}/healthz")
    report.check("/healthz يعيد 200", status == 200, f"HTTP {status}")

    status, body = http_call(opener, "GET", f"{base_url}/alerts")
    report.check(
        "مسار /alerts يعيد SPA (200 html)",
        status == 200 and "<div id=\"root\"" in body,
        f"HTTP {status}",
    )

    status, body = http_call(opener, "GET", f"{base_url}/api/alerts")
    report.check("/api/alerts محمية (401)", status == 401, f"HTTP {status}")


def login(report: Report, base_url: str, opener, username: str, password: str) -> dict | None:
    report.step("1) المصادقة")
    status, body = http_call(
        opener, "POST", f"{base_url}/api/auth/login", {"username": username, "password": password}
    )
    if not report.check(
        f"دخول «{username}»",
        status == 200,
        f"HTTP {status} ({message_of(body) or 'بلا رسالة'})",
    ):
        return None

    status, body = http_call(opener, "GET", f"{base_url}/api/auth/me")
    me = parse_json(body)
    if not report.check(
        "/api/auth/me يعيد المستخدم",
        status == 200 and isinstance(me, dict) and isinstance(me.get("id"), int),
        f"HTTP {status}",
    ):
        return None

    return me if isinstance(me, dict) else None


def cursor_of(row: dict) -> str:
    return f"{row['lastContentChangedAt']}|{row['id']}"


def alerts_checks(report: Report, base_url: str, opener, me: dict) -> None:
    role = str(me.get("role", "")).lower()

    report.step("2) قائمة التنبيهات والترقيم بالمؤشر")
    status, body = http_call(opener, "GET", f"{base_url}/api/alerts?pageSize=2")
    page1 = parse_json(body)
    ok = report.check(
        "GET /api/alerts?pageSize=2 يعيد 200 بعقد PagedResult",
        status == 200
        and isinstance(page1, dict)
        and isinstance(page1.get("items"), list)
        and isinstance(page1.get("totalCount"), int)
        and page1.get("perPage") == 2,
        f"HTTP {status}",
    )
    if not ok or not isinstance(page1, dict):
        return

    items = page1["items"]
    if not items:
        report.skip("جولة المؤشر", "لا تنبيهات في هذا النطاق — لا يمكن اختبار المؤشر بلا صفوف")
        return

    report.check(
        "كل صف ملخص يحمل الحقول المطلوبة للمؤشر",
        all(isinstance(i.get("id"), int) and isinstance(i.get("lastContentChangedAt"), str) for i in items),
        "صف ناقص الحقول",
    )

    if len(items) < 2:
        report.skip("الصفحة الثانية بالمؤشر", "الصفحة الأولى تحمل صفًا واحدًا أو أقل")

    cursor = urllib.parse.quote(cursor_of(items[-1]), safe="")
    status, body = http_call(opener, "GET", f"{base_url}/api/alerts?pageSize=2&cursor={cursor}")
    page2 = parse_json(body)
    if report.check("طلب المؤشر يعيد 200", status == 200 and isinstance(page2, dict), f"HTTP {status}"):
        assert isinstance(page2, dict)
        report.check("صفحة المؤشر تُعلن page=1", page2.get("page") == 1, f"page={page2.get('page')}")
        report.check(
            "totalCount لا يتغيّر بوجود المؤشر",
            page2.get("totalCount") == page1.get("totalCount"),
            f"{page2.get('totalCount')} != {page1.get('totalCount')}",
        )
        first_ids = {i.get("id") for i in items}
        second_ids = {i.get("id") for i in page2.get("items") or []}
        report.check(
            "لا تكرار بين الصفحتين (keyset)",
            not (first_ids & second_ids),
            f"مشترك: {sorted(first_ids & second_ids)}",
        )

    report.step("3) رفض المؤشر غير الصالح برسالة عربية نظيفة")
    status, body = http_call(opener, "GET", f"{base_url}/api/alerts?cursor=garbage")
    report.check("مؤشر مشوّه يعيد 400", status == 400, f"HTTP {status}")
    message = message_of(body)
    report.check(
        "رسالة المؤشر هي العربية المتوقعة",
        message == INVALID_CURSOR_MESSAGE,
        f"الرسالة: {message!r}",
    )
    report.check(
        "لا ذيل إطار عمل إنجليزي في الرسالة",
        not contains_english_suffix(body),
        "ظهر Parameter/Actual value في جسم الخطأ",
    )

    if items:
        cursor_valid = urllib.parse.quote(cursor_of(items[-1]), safe="")
        status, body = http_call(
            opener, "GET", f"{base_url}/api/alerts?cursor={cursor_valid}&page=0"
        )
        report.check("مؤشر صالح مع page=0 يعيد 400", status == 400, f"HTTP {status}")
        report.check(
            "رسالة page=0 عربية نظيفة",
            message_of(body) == INVALID_PAGE_MESSAGE and not contains_english_suffix(body),
            f"الرسالة: {message_of(body)!r}",
        )

    report.step("4) عدّاد غير المقروء وقائمة الصادر")
    status, body = http_call(opener, "GET", f"{base_url}/api/alerts/unread-count?scope=user")
    count = parse_json(body)
    report.check(
        "scope=user يعيد 200 بعدد صحيح",
        status == 200 and isinstance(count, dict) and isinstance(count.get("count"), int),
        f"HTTP {status}",
    )

    status, body = http_call(opener, "GET", f"{base_url}/api/alerts/sent?pageSize=1")
    if role in ("head", "subhead"):
        sent = parse_json(body)
        report.check(
            "قائمة الصادر للرئيس تعيد 200 بعقد PagedResult",
            status == 200 and isinstance(sent, dict) and isinstance(sent.get("items"), list),
            f"HTTP {status}",
        )
    else:
        report.check(
            "قائمة الصادر مرفوضة لغير الرئيس (403)",
            status == 403,
            f"HTTP {status} (الدور: {role})",
        )


def main() -> int:
    parser = argparse.ArgumentParser(description="دخان حيّ قراءة فقط لمسارات التنبيهات.")
    parser.add_argument("--base-url", default=os.environ.get("DOCGEN_BASE_URL", DEFAULT_BASE_URL))
    parser.add_argument(
        "--username",
        default=os.environ.get("DOCGEN_SMOKE_USERNAME"),
        help="اسم المستخدم (يُفعّل الفحوص المُصادقة). يُقرأ من DOCGEN_SMOKE_USERNAME أيضًا.",
    )
    parser.add_argument(
        "--password",
        help="كلمة المرور (غير مفضّل: تظهر في سجل الصدفة). استخدم DOCGEN_SMOKE_PASSWORD.",
    )
    args = parser.parse_args()

    # طباعة عربية على أي طرفية بلا انهيار ترميز.
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass

    base_url = args.base_url.rstrip("/")
    opener = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
    )
    report = Report()

    print(f"دخان حيّ قراءة فقط: {base_url}")
    public_checks(report, base_url, opener)

    if not args.username:
        report.skip("الفحوص المُصادقة", "لا --username — يُشغَّل بلا مصادقة")
    else:
        password = args.password or os.environ.get("DOCGEN_SMOKE_PASSWORD") or getpass.getpass(
            f"كلمة مرور «{args.username}»: "
        )
        me = login(report, base_url, opener, args.username, password)
        if me:
            alerts_checks(report, base_url, opener, me)

    print("\n" + "-" * 60)
    print(f"النتيجة: {report.passed} ناجح / {report.failed} فاشل / {report.skipped} متجاوز")
    if report.failures:
        print("الفاشل:")
        for failure in report.failures:
            print(f"  - {failure}")
    return 0 if report.failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
