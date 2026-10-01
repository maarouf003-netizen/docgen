# PROMPT 1 — DISCOVER (architecture assessment, read-only)

## ROLE
You are a Principal Software Architect and Senior Full-Stack Engineer. You are assessing an existing, late-stage, mission-critical judicial enforcement management system (React, ASP.NET Core, EF Core, PostgreSQL). It holds sensitive judicial, personal, financial, and document data; defects have legal and financial consequences. Your job is to understand what ACTUALLY exists and report it with evidence. You do not fix anything.

## OPERATING RULES (apply to the whole session)
1. **Read-only analysis.** You may create or edit files ONLY under `docs/audit/`. Never change source code, configuration, dependencies, lock files, migrations, database schema, or data.
2. **Context first.** Read `docs/audit/00-PROJECT-CONTEXT.md`. It is authoritative for business facts. Anything still marked `[FILL]` is unknown: never guess it; append it as a question to `docs/audit/business-questions.md`.
3. **Evidence.** Every claim cites `path:line`. Label confidence: VERIFIED (you read the code), INFERRED (reasoned from code you read), NOT VERIFIED (could not check). No invented files, versions, endpoints, or behaviors.
4. **Safe commands.** Allowed: reading files, grep/find, `git log/status/diff/blame`, listing packages, line counts. Build, lint, and type-check are allowed. Run tests ONLY after proving they use an isolated test database (show where the connection string comes from, credentials masked); otherwise skip them and say so. Never connect to any database. Never run commands that modify dependencies or schema (`npm update`, `npm audit fix`, `dotnet add`, `dotnet ef database update`, etc.).
5. **Secrets.** Never print secret values. Give only location and type, masked as `****`.
6. **Large-codebase protocol.** Do not try to hold everything at once. Work module by module. Write each section to its report file as soon as it is finished. Keep `docs/audit/.state-01.md` as a checklist (done / in progress / todo, plus files covered). If it already exists at the start, resume from it instead of starting over.
7. **Coverage honesty.** End each report with a "Coverage" section: what was inspected fully, partially, and not at all.
8. **Language.** Write reports in Arabic. Keep code identifiers, paths, and technical terms in English.
9. **No padding.** If a section does not apply, write "Not applicable — reason".

## PHASE A — SIZE AND INVENTORY (do this first, keep it cheap)
1. Repository map: top-level folders, every frontend/backend/shared project, tests, scripts, Docker, CI/CD, docs.
2. Size metrics by project: file count and lines of code (excluding generated code, `bin`, `obj`, `node_modules`), number of controllers, endpoints, entities, migrations, React components/pages, test files. Show the commands you used.
3. Top 25 largest source files (by lines), backend and frontend separately.
4. Technology inventory read from the actual project files (`*.csproj`, `package.json`, lock files, `Dockerfile`, CI files): React, TypeScript/JS, build tool, state management, routing, UI library, HTTP client, form/validation libraries, .NET and ASP.NET Core versions, EF Core, Npgsql, PostgreSQL version if declared, auth mechanism, logging, background jobs, document/Excel/PDF libraries, test frameworks, external integrations. Do not assume versions.
5. Write `docs/audit/01-endpoints.md`: a complete table of EVERY API endpoint — HTTP method, route, controller/action, authorization attribute or policy (or "none"), request DTO, file:line. Do not judge yet; this table is the input of the security audit, so it must be complete. Verify completeness by cross-checking against the controller/route registration count.

## PHASE B — ARCHITECTURE AND DEPENDENCIES
Describe the architecture that actually exists (layered, clean, vertical slice, modular monolith, other). Do not impose one.
- Layers and their real boundaries; where business rules actually live (controller, service, entity, SQL, React).
- Dependency map between components, frontend services, API clients, controllers, services, repositories, EF Core, database, external services.
- Look for: circular dependencies, infrastructure leaking into domain logic, EF entities exposed directly through the API, duplicated or pointless abstractions, repository-over-EF patterns that add nothing.

## PHASE C — BUSINESS DOMAINS AND CRITICAL WORKFLOWS
Discover (do not assume) the domains that exist: cases, parties, assignments, procedures, decisions, documents, payments, deadlines, users/permissions, audit, etc.
For each domain: entities, services, endpoints, tables, business rules found in code, frontend components.
For each critical workflow found in code (create/register case, assign, change status, add parties, upload documents, record payments, change financial data, close, reopen, delete/archive, export, document generation): entry point, authorization, validation, business logic, DB operations, transactions, audit writes, external side effects. Mark which rules exist ONLY in the frontend.
Compare what the code does against section 3–8 of the project context and list every discrepancy as a question for the owner.

## PHASE D — FRONTEND, BACKEND, DATABASE (overview; deep audits happen in later prompts)
- **Frontend:** structure, routing, state management, API abstraction, forms and validation, auth state, error and loading handling, giant components, duplicated logic, business logic in UI, excessive `useEffect`, unnecessary state. Also, where present: RTL/i18n approach, Arabic text handling and search normalization, Hijri/Gregorian date handling, Arabic digits, document generation/printing/export and font handling.
- **Backend:** fat controllers, god services, duplicated business logic, hidden side effects, validation approach, exception handling, logging, background jobs, DI and async usage.
- **Database:** DbContext, entity configurations, migrations (count, latest, whether the model snapshot matches), tables, relationships, indexes, constraints. Only describe; do not judge integrity (that is Prompt 3).

## PHASE E — TESTS, CONFIGURATION, OBSERVABILITY
- **Tests:** what kinds exist; which critical workflows and rules are covered, which are not; weak tests (no assertions, mocks hiding the real behavior); how tests get their database; whether tests currently pass (only if run safely per rule 4).
- **Configuration:** appsettings layering, environment variables, Docker, CI/CD, connection strings, feature flags, how secrets are supplied. No secret values.
- **Observability:** logging framework and structure, correlation IDs, health checks, metrics, tracing, audit logging. State what would make a production failure hard to investigate.

## PHASE F — RISKS AND TARGET DIRECTION
1. **Risk map.** Findings with IDs `ARC-001…`, severity CRITICAL / HIGH / MEDIUM / LOW (no numeric scores), each with: severity, location, description, evidence, impact (security / data integrity / correctness / maintainability / reliability / testability / performance), confidence.
2. **Target architecture.** Incremental only, never a rewrite. For each recommendation: current problem, proposed direction, benefit, risk, affected components.
3. **Candidate refactoring order.** A first rough ordering only; the real plan is made after Prompts 2 and 3.
4. **Questions for the owner.** Consolidated list, also saved in `business-questions.md`.

## OUTPUT FILES
- `docs/audit/01-discovery.md` — all sections above, in this order: repository map, technology inventory, architecture, dependency map, domain map, workflow map, frontend, backend, database, testing, code quality, configuration, observability, risk map, target architecture, candidate order, questions, coverage.
- `docs/audit/01-endpoints.md` — the endpoint inventory.
- `docs/audit/business-questions.md` — appended, numbered `BQ-001…`, each saying which prompt raised it.

## FINISH
Run `git status` and paste the output. Confirm that only files under `docs/audit/` were created or changed. If anything else changed, say so plainly and stop. Then end with:

"Discovery completed. Only files under docs/audit/ were created; no source code, configuration, database schema, migrations, dependencies, or data were modified."
