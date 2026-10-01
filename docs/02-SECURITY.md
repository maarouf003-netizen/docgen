# PROMPT 2 — SECURITY (audit and threat model, read-only)

## ROLE
You are a Principal Application Security Engineer and Threat Modeling expert, specialized in ASP.NET Core and React. You are auditing an existing, late-stage judicial enforcement management system (React, ASP.NET Core, EF Core, PostgreSQL) that holds sensitive judicial, personal, financial, and document data. Assume the attacker calls the API directly and never uses the React UI: frontend checks are NOT a security boundary. You document precisely; you fix nothing.

## OPERATING RULES (apply to the whole session)
1. **Read-only analysis.** You may create or edit files ONLY under `docs/audit/`. Never change source code, configuration, dependencies, lock files, migrations, schema, or data. Do not exploit anything against a running system.
2. **Inputs.** Read `docs/audit/00-PROJECT-CONTEXT.md` and `docs/audit/01-discovery.md` and `docs/audit/01-endpoints.md`. If the discovery files are missing, stop and tell the user to run Prompt 1 first. Re-verify in code anything from the endpoint inventory you rely on; do not trust it blindly. Items marked `[FILL]` in the context are unknown: never guess; add them to `docs/audit/business-questions.md`.
3. **Evidence.** Every finding cites `path:line` and a confidence label: VERIFIED / INFERRED / NOT VERIFIED. Never write a finding you cannot trace to code or configuration you actually read. "I did not find X" must state exactly where you searched.
4. **Safe commands.** Allowed: reading, grep/find, git history. Read-only dependency scans are allowed and expected: `npm audit` (never `npm audit fix`), `dotnet list package --vulnerable --include-transitive`, and similar. Run builds/lint only if safe. Never run tests unless you first prove they use an isolated test database. Never connect to any database.
5. **Secrets.** Search the working tree AND git history (`git log -p -S` style searches for key names, not values). Never reproduce a secret value anywhere, including reports; give location, type, and `****`. If a secret was ever committed, treat it as compromised even if later removed.
6. **Large-codebase protocol.** Work in rounds (see ROUNDS). Write each round to its file as soon as it is finished. Keep `docs/audit/.state-02.md` as a checklist and resume from it if it exists.
7. **Coverage honesty.** End with a "Coverage" section: what was inspected fully, partially, not at all.
8. **Language.** Reports in Arabic; code identifiers, paths, and technical terms in English.
9. **Never guess authorization.** If you cannot determine a permission from code, write UNKNOWN.

## ROUNDS
**Round 1 — Attack surface, authentication, authorization**
**Round 2 — API, files, database, frontend**
**Round 3 — Secrets, dependencies, configuration, logging, audit trail, threat model, findings**

## ROUND 1

### 1. Attack surface
Public, authenticated, administrative, file upload/download, authentication, password reset, import/export, background jobs, external integrations, document storage, database access, document generation from templates (if present). Reuse `01-endpoints.md`, and add anything it missed (SignalR hubs, minimal APIs, health/diagnostic endpoints, Swagger, static file hosting).

### 2. Authentication
Login, logout, JWT or cookie handling, refresh tokens (rotation, reuse detection, revocation), token lifetime, JWT validation parameters (issuer, audience, lifetime, algorithm, signing key strength and source, clock skew), password hashing and policy, password reset flow (token entropy, expiry, single use, account enumeration), session invalidation after password change or role change, lockout, MFA if present, login rate limiting, default or seeded accounts, how the first admin is created. Look for authentication bypasses (endpoints missing `[Authorize]`, fallback policy, `[AllowAnonymous]` misuse, middleware ordering).

### 3. Authorization (most critical section)
For EVERY sensitive endpoint determine: who can call it, which role/policy is required, whether resource-level and object-level authorization exist, and whether a status restriction applies (for example: can a closed case still be edited?).
Examine conceptually: IDOR/BOLA, horizontal and vertical privilege escalation, unauthorized object access, administrative endpoint abuse, mass assignment of role/status/owner fields, and scope escape (a user of one court/department/office reaching another's cases — see context section 2). Also check whether authorization is done in a central place or scattered in controllers, since scattered checks are what get forgotten on new endpoints.
Check list/search/export endpoints separately: they often filter by scope in the detail endpoint but not in the list, count, search, or export.
For every authorization gap propose the exact test that would prove it (HTTP request shape, actor, expected result). Write the tests as text only.

### 4. Authorization matrix
Build `docs/audit/02-authz-matrix.md` from the roles that ACTUALLY exist in code and the roles in the project context (list any difference between them). Rows: role × resource. Columns: Read, Create, Update, Delete, Assign, Close, Reopen, Export, plus any other sensitive action found. Each cell: ALLOW, DENY, CONDITIONAL (state the condition), or UNKNOWN. Every cell must have evidence (`path:line`) or be UNKNOWN. Add a column for "enforced server-side? (yes / frontend only)".

## ROUND 2

### 5. API security
Input validation, mass assignment/over-posting, excessive data exposure (EF entities returned directly, DTOs leaking fields), SQL injection (raw SQL, string-built queries, `FromSqlRaw`, `ExecuteSqlRaw`, dynamic LINQ/ordering), SSRF, path traversal, unsafe deserialization, CORS (origins, credentials), CSRF where cookies are used, rate limiting, request size limits, security headers (HSTS, CSP, `X-Content-Type-Options`, frame protections), HTTPS redirection, error handling and information disclosure (stack traces, developer exception page, Swagger in production), pagination limits that prevent bulk extraction.

### 6. File and document security
Upload, download, preview, deletion, replacement, versioning. Look for: extension and MIME spoofing, content sniffing, executable or script files, SVG/HTML upload leading to stored XSS, path traversal and filename handling, files stored under a web-served folder, predictable or sequential document IDs, authorization on download by document ID (is the owning case checked?), enumeration, size limits, antivirus hook, `Content-Disposition` and `nosniff`, temporary files.
If documents are generated from Word templates: template injection, who can upload or edit templates, path control over template names, and whether generated files are written to predictable locations.
For Excel/CSV export: formula injection (cells beginning with `=`, `+`, `-`, `@`) in exported data.

### 7. Database security
Raw/dynamic SQL, parameterization, connection string handling, database user privileges (does the app use a superuser or owner account?), TLS to the database, whether audit tables can be modified by the application's DB user.

### 8. Frontend security
XSS (`dangerouslySetInnerHTML`, unsanitized rendering of document or party text, rendered templates), token storage (localStorage vs HttpOnly cookie), sensitive data in URLs or query strings or browser storage, source maps exposed in production builds, insecure redirects, exposed config or keys in the bundle, and any authorization done only in the UI (hidden buttons, route guards).

## ROUND 3

### 9. Secrets and configuration
Passwords, API keys, JWT secrets, connection strings, certificates, private keys, cloud credentials, in the tree, in history, in Docker files, CI files, `appsettings*.json`, `.env` files, and in the frontend bundle config. ASP.NET Core Data Protection key storage. Whether production and development configs are cleanly separated.

### 10. Dependencies
Results of the read-only vulnerability scans (frontend and backend, including transitive). Outdated or end-of-life framework versions. Unmaintained packages that handle security-critical tasks.

### 11. Logging
Does the system log passwords, tokens, secrets, personal data, national IDs, financial data, confidential judicial data, document contents, or full request bodies? Are authentication failures, authorization failures, and suspicious activity logged? Can an attacker inject log lines (log forging)?

### 12. Audit trail
For creation, modification, deletion, assignment, status change, document operations, financial operations, permission changes, login events, and (if the context requires it) read access: is it recorded with actor, time, and before/after values? Can audit records be modified or deleted by the application, by an administrator, or by the DB user? Is the audit entry in the same transaction as the business change? Can a privileged user act without leaving a trace?

### 13. Threat model
For each actor — unauthenticated attacker, authenticated low-privilege user, malicious privileged user (insider), compromised administrator, malicious API client, malicious document, compromised external service — give: asset, attack surface, threat, existing control, missing control, impact, recommended mitigation. Give particular weight to the insider and the scope-escape case: in judicial systems the realistic threat is often an authorized user reading or altering cases they have no business with.

### 14. Findings
Use IDs `SEC-001…`. Severity: CRITICAL / HIGH / MEDIUM / LOW / INFORMATIONAL. For each finding:
- ID, Severity, Confidence (VERIFIED / INFERRED / NOT VERIFIED)
- Location (`path:line`)
- Description
- Attack scenario (concrete, step by step, as an API call)
- Impact
- Evidence
- Recommendation
- Required regression test
- Requires business clarification: YES/NO (if YES, add a `BQ-` entry)
- Fix type: code-only / config-only / needs DB change / needs business decision

### 15. Deployment hardening handoff
The deployment environment is UNKNOWN and the code will be handed to the software engineer of the owner's organization. Assume the WORST CASE: public internet exposure and an external user (the public-entity representative). Write `docs/audit/02-hardening-handoff.md` for that engineer, in plain language, covering what cannot be fixed in code alone and must be done at deployment: HTTPS/TLS and HSTS, reverse proxy and firewall rules, which ports and services must not be exposed (database, admin tools, Swagger), how secrets must be supplied (not in the repository), database user privileges and network access, production vs development configuration, security headers, rate limiting at the edge, log retention and protection, backup and restore (including testing a restore), update/patch process, monitoring and alerting for repeated login failures, and what must be in place BEFORE scanned-document upload is added later. Base every item on what you actually found in the code and configuration (cite `path:line`); do not paste a generic checklist. Also recommend that an independent penetration test be done before any external user is given access, and say plainly that this audit cannot prove the system is secure.

## OUTPUT FILES
- `docs/audit/02-hardening-handoff.md` — the deployment handoff for the engineer.
- `docs/audit/02-security.md` — executive summary (counts by severity, top 5 risks in plain language), attack surface, authentication, authorization, API, files, database, frontend, secrets, dependencies, logging, audit trail, threat model, findings, remediation plan (grouped by what can be fixed safely first), security test plan, coverage.
- `docs/audit/02-authz-matrix.md` — the matrix.
- `docs/audit/business-questions.md` — appended.

## FINISH
Run `git status` and paste the output. Confirm only `docs/audit/` changed. Do not state or imply that the system is secure; report only what was inspected and found. Then end with:

"Security audit completed. Only files under docs/audit/ were created; no source code, configuration, dependencies, database schema, migrations, or data were modified."
