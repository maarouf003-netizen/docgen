#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""تدقيق بيانات التنبيهات (HeadAlert) — بوابة G0 في خطة إعادة الهيكلة.

الاستخدام (قراءة فقط، لا يكتب في القاعدة إطلاقًا):

    python scripts/head-alert-g0-audit.py path/to/db.sqlite            # تقرير على stdout
    python scripts/head-alert-g0-audit.py path/to/db.sqlite out.md    # تقرير في ملف (UTF-8)

المخرج يطابق بوابة G0 في `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §0.2 و§2.3:
عدد التنبيهات، صفوف `legacy` النشطة، `open_by_status` مقابل `closed_linked`،
تكرارات `(HeadAlertId, UserId)`، مرشحات تكرار `DedupeKey`، أطول `Message`،
وتوزيع الأنواع — إضافة إلى تقرير صفوف المعلقات المتبقية (§16).

الأداة تفتح القاعدة بوضع `mode=ro` عبر URI، فلا يمكنها تعديل أي صف.
"""

from __future__ import annotations

import io
import os
import sqlite3
import sys
from collections import Counter, defaultdict

TARGET_TYPES = {1: "Document", 2: "Lawyer", 3: "Branch", 4: "Head"}

PREFIX_CIRCUIT_PENDING = "أحال لك رئيس القسم ملفات من دائرة "
PREFIX_MANUAL_HEAD = "تنبيه من رئيس القسم:"
PREFIX_MANUAL_SUBHEAD = "تنبيه من رئيس الشعبة:"

# حالات الإنابة المغلقة/المنتهية (انظر CK_DocumentDelegations_Status).
DELEGATION_CLOSED_STATUSES = ("منتهية", "مرفوضة")
# حالة الاستئناف المفتوحة/المغلقة (انظر CK_DocumentAppeals_Status).
APPEAL_OPEN_STATUS = "pending"

DEFAULT_DB = os.path.join("backend", "src", "DocGenerator.Api", "docgen.db")


class Audit:
    def __init__(self, db_path: str) -> None:
        self.db_path = db_path
        self.con = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
        self.con.row_factory = sqlite3.Row
        self.lines: list[str] = []

    # ---------- أدوات ----------
    def rows(self, sql: str, params: tuple = ()) -> list[sqlite3.Row]:
        return self.con.execute(sql, params).fetchall()

    def scalar(self, sql: str, params: tuple = ()):
        row = self.con.execute(sql, params).fetchone()
        return None if row is None else row[0]

    def emit(self, text: str = "") -> None:
        self.lines.append(text)

    def close(self) -> None:
        self.con.close()

    def text(self) -> str:
        return "\n".join(self.lines)

    # ---------- الأقسام ----------
    def header(self) -> None:
        self.emit("### 0) لقطة القاعدة")
        self.emit(f"- المسار: `{self.db_path}`")
        self.emit(f"- `PRAGMA integrity_check`: `{self.scalar('PRAGMA integrity_check')}`")
        self.emit(f"- `PRAGMA foreign_key_check`: {len(self.rows('PRAGMA foreign_key_check'))} صف")
        self.emit()

    def totals(self) -> None:
        total = self.scalar("select count(*) from HeadAlerts")
        self.emit("### 1) أعداد عامة")
        self.emit(f"- إجمالي صفوف `HeadAlerts`: **{total}**")
        self.emit(f"- إجمالي صفوف `HeadAlertRecipients`: **{self.scalar('select count(*) from HeadAlertRecipients')}**")
        self.emit(
            "- صفوف بلا مستلمين: **{}**".format(
                self.scalar(
                    "select count(*) from HeadAlerts a where not exists "
                    "(select 1 from HeadAlertRecipients r where r.HeadAlertId = a.Id)"
                )
            )
        )
        self.emit()

    def target_type_distribution(self) -> None:
        self.emit("### 2) توزيع الأنواع (`TargetType`) — كل الصفوف `legacy` اليوم")
        for row in self.rows("select TargetType, count(*) c from HeadAlerts group by TargetType order by c desc"):
            label = TARGET_TYPES.get(row["TargetType"], str(row["TargetType"]))
            self.emit(f"- `{label}` (= {row['TargetType']}): **{row['c']}**")
        self.emit()

    def structural_links(self) -> None:
        self.emit("### 3) الربط البنيوي")
        for column in (
            "DelegationId",
            "AppealId",
            "ReviewLetterId",
            "PublicEntityId",
            "DocumentId",
            "TargetLawyerId",
        ):
            count = self.scalar(f"select count(*) from HeadAlerts where {column} is not null")
            self.emit(f"- صفوف بـ`{column}` غير فارغ: **{count}**")
        self.emit()

    def lifecycle_split(self) -> None:
        delegation_status = {r["Id"]: r["Status"] for r in self.rows("select Id, Status from DocumentDelegations")}
        appeal_status = {r["Id"]: r["Status"] for r in self.rows("select Id, Status from DocumentAppeals")}
        review_answered = {r["Id"]: r["IsAnswered"] for r in self.rows("select Id, IsAnswered from ReviewLetters")}

        open_ids: list[int] = []
        closed_ids: list[int] = []
        unlinked_ids: list[int] = []
        detail: Counter[str] = Counter()

        for alert in self.rows("select * from HeadAlerts"):
            message = alert["Message"] or ""
            if alert["DelegationId"] is not None:
                status = delegation_status.get(alert["DelegationId"])
                detail[f"delegation:{status}"] += 1
                if status is None:
                    unlinked_ids.append(alert["Id"])
                elif status in DELEGATION_CLOSED_STATUSES:
                    closed_ids.append(alert["Id"])
                else:
                    open_ids.append(alert["Id"])
            elif alert["AppealId"] is not None:
                status = appeal_status.get(alert["AppealId"])
                detail[f"appeal:{status}"] += 1
                if status is None:
                    unlinked_ids.append(alert["Id"])
                elif status == APPEAL_OPEN_STATUS:
                    open_ids.append(alert["Id"])
                else:
                    closed_ids.append(alert["Id"])
            elif alert["ReviewLetterId"] is not None:
                answered = review_answered.get(alert["ReviewLetterId"])
                detail[f"review:answered={answered}"] += 1
                if answered is None:
                    unlinked_ids.append(alert["Id"])
                elif answered:
                    closed_ids.append(alert["Id"])
                else:
                    open_ids.append(alert["Id"])
            elif alert["PublicEntityId"] is not None:
                detail["entity"] += 1
                open_ids.append(alert["Id"])
            elif alert["TargetType"] == 2 and message.startswith(PREFIX_CIRCUIT_PENDING):
                detail["circuit_pending"] += 1
                open_ids.append(alert["Id"])
            elif alert["TargetType"] == 1 and message.startswith((PREFIX_MANUAL_HEAD, PREFIX_MANUAL_SUBHEAD)):
                detail["manual_document"] += 1
                unlinked_ids.append(alert["Id"])
            else:
                detail["unlinked"] += 1
                unlinked_ids.append(alert["Id"])

        self.emit("### 4) `open_by_status` مقابل `closed_linked` مقابل `unlinked` (كل صفوف `HeadAlerts` نشطة)")
        self.emit(f"- **`open_by_status` (تدفقات مفتوحة فعليًا): {len(open_ids)}**")
        self.emit(f"- **`closed_linked` (مرتبطة بدورة مغلقة): {len(closed_ids)}**")
        self.emit(f"- **`unlinked` (بلا رابط بنيوي قابل للتحقق): {len(unlinked_ids)}**")
        self.emit("- تفصيل حسب مصدر الربط:")
        for key, count in sorted(detail.items()):
            self.emit(f"  - `{key}`: **{count}**")
        self.emit()

        if open_ids:
            placeholders = ",".join(str(i) for i in open_ids)
            unread = self.scalar(
                "select count(*) from HeadAlerts a where a.Id in ("
                + placeholders
                + ") and exists (select 1 from HeadAlertRecipients r where r.HeadAlertId = a.Id and r.IsRead = 0)"
            )
        else:
            unread = 0
        self.emit(f"- صفوف `open_by_status` غير المقروءة: **{unread}**")
        self.emit()

    def recipient_duplicates(self) -> None:
        self.emit("### 5) تكرارات `(HeadAlertId, UserId)` (قيد فريد مقترح في M2)")
        duplicates = self.rows(
            "select HeadAlertId, UserId, count(*) c from HeadAlertRecipients "
            "group by HeadAlertId, UserId having c > 1"
        )
        self.emit(f"- أزواج مكررة: **{len(duplicates)}**")
        for row in duplicates:
            self.emit(f"  - HeadAlertId={row['HeadAlertId']} UserId={row['UserId']} count={row['c']}")
        self.emit()

    def message_lengths(self) -> None:
        self.emit("### 6) أطول رسالة")
        longest = self.scalar("select max(length(Message)) from HeadAlerts")
        self.emit(f"- أقصى `length(Message)` (نقاط Unicode كما يحسبه SQLite): **{longest}**")
        row = self.con.execute("select Id, Message from HeadAlerts order by length(Message) desc limit 1").fetchone()
        if row is not None:
            self.emit(f"- أطول رسالة (المعرّف {row['Id']}): `{row['Message'][:200]}`")
        self.emit()

        self.emit("### 7) توزيع أطوال الرسائل")
        for band in self.rows(
            """
            select case when length(Message) < 100 then '1) <100'
                        when length(Message) < 500 then '2) 100-499'
                        when length(Message) < 2000 then '3) 500-1999'
                        else '4) >=2000' end band, count(*) c
            from HeadAlerts group by band order by band
            """
        ):
            self.emit(f"- {band['band']}: **{band['c']}**")
        self.emit()

    def dedupe_candidates(self) -> None:
        self.emit("### 8) مرشحات تكرار `DedupeKey` (وفق قوالب §6.4)")
        keys: dict[str, list[int]] = defaultdict(list)
        for alert in self.rows("select * from HeadAlerts"):
            message = alert["Message"] or ""
            users = [r["UserId"] for r in self.rows("select UserId from HeadAlertRecipients where HeadAlertId = ?", (alert["Id"],))]
            if alert["DelegationId"] is not None:
                for user in users:
                    keys[f"delegation:{alert['DelegationId']}:recipient:{user}:<kind>"].append(alert["Id"])
            if alert["AppealId"] is not None:
                for user in users:
                    keys[f"appeal:{alert['AppealId']}:recipient:{user}:<kind>"].append(alert["Id"])
            if alert["ReviewLetterId"] is not None:
                for user in users:
                    keys[f"review:{alert['ReviewLetterId']}:recipient:{user}:<kind>"].append(alert["Id"])
            if alert["TargetType"] == 2 and message.startswith(PREFIX_CIRCUIT_PENDING):
                for user in users:
                    keys[f"circuit-pending:{alert['BranchId']}:recipient:{user}:<kind>"].append(alert["Id"])
            if (
                alert["TargetType"] == 1
                and alert["DocumentId"] is not None
                and not message.startswith((PREFIX_MANUAL_HEAD, PREFIX_MANUAL_SUBHEAD))
            ):
                for user in users:
                    keys[f"document:{alert['DocumentId']}:recipient:{user}:<kind>"].append(alert["Id"])
            if alert["PublicEntityId"] is not None:
                for user in users:
                    keys[f"entity:<op>:{alert['PublicEntityId']}:recipient:{user}:<kind>"].append(alert["Id"])

        collisions = {key: value for key, value in keys.items() if len(value) > 1}
        self.emit(f"- مفاتيح مرشحة للتصادم (بتجاهل `kind` لأن `Kind` غير موجود بعد): **{len(collisions)}**")
        for key, value in sorted(collisions.items()):
            self.emit(f"  - `{key}` → صفوف {value}")
        self.emit()

    def legacy_monitoring_counts(self) -> None:
        self.emit("### 9) صفوف `legacy` نشطة قابلة للمطابقة البنيوية (استعلام مراقبة §16)")
        count = self.scalar(
            """
            select count(*) from HeadAlerts
            where DelegationId is not null or AppealId is not null or ReviewLetterId is not null
               or PublicEntityId is not null
               or (DocumentId is not null and TargetType = 1
                   and Message not like 'تنبيه من رئيس القسم:%'
                   and Message not like 'تنبيه من رئيس الشعبة:%')
            """
        )
        self.emit(f"- **العدّ: {count}** (حجم الجسر التشغيلي المحتمل)")
        self.emit()

        self.emit("### 10) صفوف معلقات بلا رابط بنيوي (تقرير §16)")
        pending = self.scalar(
            """
            select count(*) from HeadAlerts
            where TargetType = 2 and DelegationId is null and AppealId is null and ReviewLetterId is null
              and PublicEntityId is null and Message like 'أحال لك رئيس القسم ملفات من دائرة %'
            """
        )
        self.emit(f"- صفوف `TargetType=Lawyer` ببادئة المعلقات: **{pending}**")
        self.emit()

    def message_prefixes(self) -> None:
        self.emit("### 11) توزيع بادئات الرسائل (45 محرفًا الأولى) — لأغراض المطابقة النصية في أداة الترحيل")
        prefixes: Counter[str] = Counter()
        for row in self.rows("select Message from HeadAlerts"):
            prefixes[(row["Message"] or "").strip()[:45]] += 1
        for prefix, count in prefixes.most_common(40):
            self.emit(f"- ({count}) `{prefix}`")
        self.emit()

    def related_table_sizes(self) -> None:
        self.emit("### 12) حجم الجداول المرتبطة")
        for table in (
            "DocumentDelegations",
            "DocumentAppeals",
            "ReviewLetters",
            "ExecutionCircuits",
            "Documents",
            "Users",
            "Branches",
            "PublicEntities",
        ):
            self.emit(f"- `{table}`: **{self.scalar(f'select count(*) from {table}')}**")
        self.emit()

    def run(self) -> str:
        self.header()
        self.totals()
        self.target_type_distribution()
        self.structural_links()
        self.lifecycle_split()
        self.recipient_duplicates()
        self.message_lengths()
        self.dedupe_candidates()
        self.legacy_monitoring_counts()
        self.message_prefixes()
        self.related_table_sizes()
        return self.text()


def main(argv: list[str]) -> int:
    db_path = argv[1] if len(argv) > 1 else DEFAULT_DB
    out_path = argv[2] if len(argv) > 2 else None

    if not os.path.exists(db_path):
        print(f"[خطأ] ملف القاعدة غير موجود: {db_path}", file=sys.stderr)
        return 2

    audit = Audit(db_path)
    try:
        report = audit.run()
    finally:
        audit.close()

    if out_path:
        with io.open(out_path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(report)
            handle.write("\n")
        print(f"written: {out_path}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
