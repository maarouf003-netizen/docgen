# PROJECT CONTEXT — judicial enforcement management system

> ملاحظة لك: هذا الملف عُبّئ بالحوار من إجاباتك. انسخه إلى المستودع في `docs/audit/00-PROJECT-CONTEXT.md`.
> ما كُتب فيه "UNKNOWN" لم تحدده أنت؛ على الوكيل أن يستخرجه من الكود ويعرضه عليك كسؤال، لا أن يخمّنه.
> راجع الملف قبل التشغيل، فإن وجدت خطأً في صياغة أي بند فصحّحه بنفسك.

## 1. What the system is
- Purpose: web application for managing judicial enforcement (تنفيذ) case files.
- Legal/jurisdictional context: Syrian legal and judicial workflows. The specific law names are not provided; do not assume them.
- Primary language: Arabic. UI is RTL.
- Stack (verify against the repo, do not trust this list blindly): React, ASP.NET Core / .NET, Entity Framework Core, Npgsql, PostgreSQL.
- Stage: late stage, not yet deployed. ALL current data is test data; no real data exists yet.
- Expected scale: about 200 users initially; about 2,000 enforcement files per year.
- Scanned documents (مسوحات ضوئية) may be added in a later phase AFTER deployment. Currently there is NO document upload. Treat future upload as a planned feature: note what must be in place before it is added.

## 2. Users and roles
Five roles. The owner gave a summary only; permissions are numerous and overlapping, so the real permissions must be extracted from code and compared with this table.

| Role | What they do | Scope of files |
|---|---|---|
| محامي (Lawyer) | Creates enforcement files, edits them, follows them, closes them, reopens them. Enters the basis numbers and does the yearly rotation. Enters amounts and dates. The ONLY role that deletes and restores files (soft delete: deleted files stay stored and can be restored). Has a calendar with reminders, visible only to himself. | Files assigned to him (files are normally his own creation; nobody assigns them to him except as noted for رئيس القسم below) |
| رئيس القسم (Head of department) | Views and follows files (no file editing). Assigns files to lawyers ONLY for الإنابة and الاستئنافات, and transfers files between lawyers. Manages the lawyers of his branch and the public-entity representatives of his governorate (exact extent of this account management is UNKNOWN). Manages public entities (الجهات العامة). | Files of his branch |
| المدير (Manager) | Views and follows files. Creates user accounts. Manages public entities. Broad permissions. | Files of all branches |
| المشرف (Supervisor) | Very similar to the manager in permissions (exact differences UNKNOWN). | Files of all branches |
| مندوب الجهة العامة (Public-entity representative) | EXTERNAL person, not part of the organization. Views files, cannot edit them. Replies to correspondence (المراسلات); replies are text only (no attachments at present). | Only the files of his own public entity (and apparently within his governorate: UNKNOWN) |

- Scoping dimensions seen so far: branch (فرع), governorate (محافظة), public entity (جهة عامة), assigned lawyer, execution department (دائرة تنفيذ). The relationship between the department and the branch is UNKNOWN: determine it from code.
- Roles for which "view only" is stated must NEVER be able to modify file contents.
- Not stated by the owner, to be determined from code and reported as questions: who may delete or restore besides the lawyer (owner says lawyer only); whether رئيس القسم can create accounts or only manage existing ones; whether any account creator can create an account with a role equal to or higher than his own, or outside his branch/governorate; whether a user can grant himself a higher role; what exactly المدير differs from المشرف.

## 3. Case lifecycle
- Statuses and transitions are defined precisely in the code. The CODE IS THE SOURCE OF TRUTH for the lifecycle. Extract them as a table (state, allowed next states, actor, authorization).
- Known from the owner: the lawyer closes and reopens files. A file can be struck off (شطب) and renewed (تجديد); a renewed file can receive another basis number within the same year.
- Do not treat the code's lifecycle as wrong merely because it differs from expectations. DO report anomalies as questions, for example: a closed file that can still be edited, reopening by a role other than the lawyer, transitions that skip states, strike-off/renewal that can leave a file half-done.

## 4. Numbering
- Each active file has a رقم أساس tied to a year. The number is ENTERED MANUALLY by the lawyer (not system-generated).
- Yearly rotation (تدوير, as practiced in the courts): in a new year the file gets a new basis number for that year; previous numbers remain stored. The lawyer performs the rotation.
- A file can get another basis number in the same year after strike-off (شطب) and renewal (تجديد).
- The file type is usually attached to the basis number.
- Uniqueness rule: within ONE execution department (دائرة تنفيذ), two files can never have the same basis number AND file type in the same year. Different departments may repeat the same number.
- To verify: whether this is enforced by a database unique constraint (not only in code); concurrency (two users entering the same number); interaction between soft delete/restore and uniqueness; whether old numbers can be edited or deleted after rotation; whether rotation is audited.

## 5. Money
- Most enforcement files contain monetary amounts, entered and edited by the lawyer.
- Several currencies can appear in ONE file. The code displays amounts and sums them in statistics.
- To verify: amounts never use floating-point types; every sum is per currency, never mixing currencies in one number and never converting with a hard-coded rate; edits to amounts are recorded in the audit log with before/after values; statistics respect each role's scope (branch, entity).
- Rounding rules, fees, interest, partial payments: UNKNOWN. Report what the code does as questions; do not judge.

## 6. Deadlines and dates
- Dates are entered by the lawyer as free text.
- The app has a calendar for adding reminders for appointments; each lawyer sees only his own reminders.
- The system does not compute legal deadlines as far as the owner stated. Reminders are treated as an aid, not a legally binding calculation. If the code computes any deadline, report it as a question.
- To verify: how dates are stored (free text in the database?), the effect on sorting/filtering/statistics, calendar time-zone handling, and whether notifications are actually sent.
- Hijri vs Gregorian: UNKNOWN.

## 7. Documents
- No document upload exists now (planned later: scanned documents).
- The app generates documents into Word format. How exactly (server-side from templates, where templates live, who can change them, whether generated files are stored on the server) is UNKNOWN to the owner: the agent must determine it from code, explain it plainly, and list its risks.
- Who may generate documents: UNKNOWN.
- Retention and deletion rules for documents: UNKNOWN.

## 8. Audit and legal traceability
- The code has an audit log. Requirement stated by the owner: the audit log must record EVERY operation and who performed it.
- Whether READING a file must also be logged: UNKNOWN. Report whether reads are logged.
- Tamper-evidence requirement: UNKNOWN. Report who can modify or delete audit records.

## 9. Deployment and operations
- Where it will run is UNKNOWN. The owner will hand the code to the software engineer of his organization.
- The owner's requirement: the application must be VERY secure against hacking.
- Assumption for all audits: WORST CASE. Assume the application is exposed to the public internet and used by an external user (the public-entity representative). Do not relax any finding because it might be on an internal network.
- Backups, restore tests, integrations (SMS, email, other government systems): UNKNOWN. Report what the code reveals.

## 10. Known problems and decisions
- Known bugs, shortcuts, fragile areas: UNKNOWN (the owner does not know).
- Decisions that must not be changed: none declared. Default rule: do not change any behavior described in sections 2–8 without explicit approval; report instead.

## 11. Consolidated checklist for the agents (from this conversation)
1. Extract the real permission matrix and compare it with section 2; report every difference.
2. Verify every role scope (branch, governorate, entity, assigned lawyer) on detail, list, search, statistics, calendar, and document endpoints.
3. Verify that "view only" roles cannot modify file contents; verify what the external representative can reach.
4. Verify account creation limits and privilege escalation (section 2).
5. Verify basis-number uniqueness at database level, rotation history preservation, and strike-off/renewal atomicity (sections 3–4).
6. Verify money types, per-currency sums, and amount-change auditing (section 5).
7. Verify date storage and calendar behavior (section 6).
8. Explain the Word-generation mechanism and audit its risks (section 7).
9. Verify audit-log completeness for every write operation, transactional consistency, and immutability (section 8).
10. Produce a deployment hardening handoff for the organization's software engineer (see Prompt 2).
