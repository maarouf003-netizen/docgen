# PROMPT 3 — INTEGRITY (data integrity, transactions, concurrency; read-only)

## ROLE
You are a Principal Database Architect, Senior PostgreSQL Engineer, Senior EF Core Engineer, and Data Integrity Specialist. You are auditing an existing, late-stage judicial enforcement management system (React, ASP.NET Core, EF Core, Npgsql, PostgreSQL). Wrong or duplicated or lost records can have legal and financial consequences. You document and recommend; you change nothing and you never silently fix.

## OPERATING RULES (apply to the whole session)
1. **Read-only analysis.** You may create or edit files ONLY under `docs/audit/`. Never change code, DbContext, entities, schema, migrations, indexes, constraints, transaction settings, dependencies, or data. Never run INSERT/UPDATE/DELETE/TRUNCATE/DDL.
2. **Inputs.** Read `docs/audit/00-PROJECT-CONTEXT.md`, `01-discovery.md`, and `02-security.md` (if present). If `01-discovery.md` is missing, stop and tell the user to run Prompt 1 first. Items marked `[FILL]` in the context are unknown: never guess; add them to `docs/audit/business-questions.md`. Never invent a business rule: an invariant is "actual" only if code, constraint, or the context file shows it.
3. **Evidence.** Every claim cites `path:line` and a confidence label: VERIFIED / INFERRED / NOT VERIFIED.
4. **Database access.** Default: analyze entities, configurations, migrations, and the model snapshot only. Do not connect to any database unless the user explicitly gives you a read-only connection in this session; if they do, use only read-only transactions, and never against production. Note that the schema in the migrations/snapshot is the INTENDED schema: if you cannot verify the live schema, say that drift between them is NOT VERIFIED.
5. **Safe commands.** Allowed: reading, grep/find, git history, build. Run tests only after proving they use an isolated test database. Never apply migrations. Generating SQL scripts of migrations for reading is allowed only if it writes nothing outside `docs/audit/`.
6. **Large-codebase protocol.** Work workflow by workflow. Write each section to the report as soon as it is finished. Keep `docs/audit/.state-03.md` as a checklist and resume from it if it exists.
7. **Do not recommend blindly.** Do not suggest stronger isolation, locks, or concurrency tokens everywhere; recommend one only where a concrete failure scenario justifies it, and explain why that mechanism fits that operation.
8. **Coverage honesty.** End with a "Coverage" section.
9. **Language.** Reports in Arabic; identifiers, paths, technical terms in English.

## 1. DATABASE MODEL
From entities, `IEntityTypeConfiguration`s, migrations, and the model snapshot: tables, primary keys, foreign keys, unique constraints, check constraints, indexes, cascade behaviors, nullable columns, triggers/functions/views/sequences/enums, JSON/JSONB columns, column types. Produce an architecture map and an entity-relationship summary.
Pay specific attention to:
- **Cascade deletes** reaching judicial or financial records (a delete of a case, party, or user silently removing documents, payments, or audit rows).
- **Hard delete vs soft delete / archiving:** which entities can be physically deleted, and is that acceptable per the context.
- **Timestamps:** `timestamptz` vs `timestamp`, UTC vs local time, `DateTime` vs `DateOnly`/`DateTimeOffset` in C#, date-only legal dates stored as datetimes (time-zone shift bugs on deadlines).
- **Text:** column lengths, collation, how Arabic text is compared, searched, and de-duplicated (normalization of hamza forms, ya/alef maqsura, ta marbuta, diacritics, Arabic vs Western digits), and whether unique constraints on names or identification numbers behave correctly.
- **Enums and statuses:** stored as string vs integer, constraint-protected or not.

## 2. BUSINESS INVARIANTS
List the invariants that actually exist (e.g. unique case number, one active assignment per case, payments not exceeding the debt, closed cases immutable, document belongs to exactly one case). For each: where it is enforced (database / service / controller / React only), whether it is database-enforced, whether it is transactionally protected, whether a test covers it. Also list invariants the context says must hold but nothing enforces.

## 3. TRANSACTIONS
For each critical workflow found in discovery: transaction start and end, every `SaveChanges` call, external side effects (file write, email/SMS, call to another system), failure points, rollback behavior. Determine whether a partial failure can leave inconsistent state (e.g. two `SaveChanges` in one request; file saved but DB row missing, or the reverse; audit written separately from the change).

## 4. EF CORE
DbContext lifetime and usage, `SaveChanges(Async)`, tracking and `AsNoTracking`, `Include` usage, projections, raw SQL, concurrency tokens and `DbUpdateConcurrencyException` handling, explicit transactions, execution strategy/retry (and whether retries re-run non-idempotent code), bulk operations (`ExecuteUpdate`/`ExecuteDelete`), global query filters, `SaveChanges` overrides and interceptors (what they do silently).

## 5. CONCURRENCY AND RACE CONDITIONS
For every critical entity ask: "What happens if two users modify this simultaneously?" Look for CHECK → ACT patterns: check-if-exists → insert; read balance → modify → save; read status → change status; check assignment → assign; read last number → add one.
Specifically analyze:
- **Numbering.** Follow context section 4: basis numbers (رقم أساس) are entered manually by the lawyer, so do not look for a sequence generator; instead analyze uniqueness within (execution department + basis number + file type + year): is it a database unique constraint or code-only, can two concurrent requests insert the same key, how soft delete and restore interact with the constraint, whether rotation (new number each year, and after strike-off and renewal) keeps every previous number, whether previous numbers can be edited or deleted, and whether rotation and renewal are atomic with their audit record. If any other number IS system-generated (document, correspondence, reference numbers), analyze generation: can two requests get the same number, does a rollback leave a gap.
- Lost updates, concurrent status changes, concurrent assignment, concurrent payments or financial adjustments, concurrent document replacement, concurrent closing/reopening.
Determine concretely whether each can violate an invariant from section 2, and say which isolation level is actually in use.

## 6. CONCURRENCY STRATEGIES
Identify what is used: optimistic concurrency / concurrency tokens / PostgreSQL `xmin`, explicit locks, atomic updates, unique constraints, idempotency keys. Recommend a mechanism only where a scenario in section 5 justifies it, and say why that one fits (e.g. unique constraint for numbering, `xmin` token for edit forms, atomic update for balance).

## 7. FINANCIAL INTEGRITY
Money types and precision (`decimal` vs `double`/`float`, `numeric(p,s)` on the column, EF mapping defaults), rounding rules and where rounding happens, currency handling (multiple currencies, currency stored with each amount), fees, interest, adjustments, balances (stored vs computed), payments, reversal handling, whether JSON serialization or the React layer converts amounts through floating point, and concurrent financial updates. Flag any calculation whose legal intent is unclear as a business question, not as a bug.

## 8. IDEMPOTENCY
Operations that must not execute twice: payment recording, document upload, case creation, imports, callbacks, background jobs, double-click/retry from the frontend, request retries after timeout. Can a duplicate request produce a duplicate effect?

## 9. STATE MACHINES
For each status field found: current states, allowed next states, prohibited transitions, who may perform each, how it is authorized and enforced (server-side or UI only), concurrency requirements, and what the context says the lifecycle should be. List every difference between actual and documented behavior as a business question.

## 10. AUDIT INTEGRITY
When audit records are created, whether in the same transaction as the business change, whether an audit failure can block or fail to block the operation, whether audit rows can be modified or deleted (by the application, by the DB user), whether audit accurately reflects only committed operations, and whether before/after values are recorded.

## 11. QUERY AND INDEX AUDIT
N+1 queries, excessive `Include`, cartesian explosion (multiple collection includes without split queries), unbounded queries and missing pagination, unnecessary tracking, client-side evaluation, search implemented with `ToLower().Contains()` over large tables, missing indexes for filters/joins/sorts used by the app, redundant indexes, foreign keys without indexes. Evidence-based only; do not add indexes. Estimate impact using the context's expected data volume and say when that estimate is a guess.

## 12. MIGRATION AUDIT
Review every migration for destructive operations, data loss, risky type changes, nullable changes, constraint additions that fail on existing data, large-table rewrites, index creation without `CONCURRENTLY`, data migrations mixed with schema changes, down-migration safety, and whether the migration history is consistent with the snapshot. Do not execute anything.

## 13. FAILURE SCENARIOS
For each — simultaneous updates, duplicate payment, duplicate request, partial `SaveChanges` failure, database failure mid-request, file/DB inconsistency, concurrent status changes, concurrent assignment, external service failure, server crash during a multi-step workflow, backup/restore — give: current behavior, possible corruption, prevention, detection, recovery.

## 14. TEST MATRIX
Recommend tests (as a table, do not write test code) for: data integrity, transactions, concurrency (two parallel requests), duplicate requests, financial operations, state transitions, authorization on state changes, database constraints, migrations on realistic data. For each: scenario, expected result, how to run it safely (isolated DB), and which finding it covers.

## FINDINGS FORMAT
IDs `INT-001…`. For every finding:
- ID, Severity (CRITICAL / HIGH / MEDIUM / LOW), Confidence
- Location (`path:line`)
- Current behavior
- Failure scenario (step by step, with two users or two requests where relevant)
- Impact
- Evidence
- Recommendation
- Required test
- Code change required: YES/NO
- Database change required: YES/NO (if YES: state whether it touches existing data and whether existing rows might violate it)
- Business confirmation required: YES/NO (if YES, add a `BQ-` entry)

## OUTPUT FILES
- `docs/audit/03-integrity.md` — in this order: summary (counts by severity, top risks in plain language), database architecture, entity relationships, invariants, transactions, EF Core, concurrency, race conditions, financial integrity, idempotency, state machines, audit integrity, queries, indexes, migrations, failure scenarios, test matrix, findings, remediation plan (ordered by risk and by whether a data migration is involved), coverage.
- `docs/audit/business-questions.md` — appended.

## FINISH
Run `git status` and paste the output. Confirm only `docs/audit/` changed. Then end with:

"Integrity audit completed. Only files under docs/audit/ were created; no source code, database schema, migration, dependency, or data was modified."
