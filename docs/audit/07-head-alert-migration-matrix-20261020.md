# مصفوفة ترحيل مواقع كتابة التنبيهات — PR-2

> **التاريخ:** 2026-10-20
> **المرجع:** `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §5.3 و§8.1 («مصفوفة ترحيل إلزامية»).
> **الحالة:** الأساس (Outbox + معالج التنبيه النظامي) مبنيٌّ ومُتحقَّق منه؛ **ترحيل المواقع لم يبدأ بعد**.

## 1) ما أُنجز في PR-2 حتى الآن

| البند | الحالة |
|---|---|
| `IHeadAlertOutbox` + `IHeadAlertOutboxStore` + الحمولة/الإعدادات | ✔ |
| حارس المعاملة في التسجيل (يرفض بلا معاملة) | ✔ |
| `HeadAlertService.CreateSystemAsync` (المعالج الداخلي) | ✔ |
| `HeadAlertService.ApplyCircuitRenamedAsync` | ✔ |
| `ArchiveByDelegationAsync` / `ArchiveByAppealAsync` (أرشفة مقيدة بالنوع) | ✔ |
| `HeadAlertOutboxStore` + `HeadAlertOutboxLock` + `HeadAlertOutboxDispatcher` | ✔ |
| استثناءات داخلية (`HeadAlertConflictException` / `HeadAlertNoRecipientsException`) | ✔ |
| **ترحيل الـ26 موقعًا** | ✘ **متبقٍ** |
| **هجرة `M2` (Backfill + التصنيف + الفهرس الفريد الجزئي)** | ✘ **متبقٍ** |
| **الجسر الانتقالي `legacy`** | ✘ **متبقٍ** (وموقوف على قرار G0) |
| **تحديث `TargetUserId` عند التعاقب** | ✘ **متبقٍ** |
| **إزالة 31 موضع `head_alert_failed`** | ✘ **متبقٍ** |

**حالة السلوك الآن:** لم يُرحَّل أي منتج، فالكتابة الحالية (معاملة منفصلة بعد التزام العمل)
هي الفعّالة. لا يوجد مسار جديد بلا مُنتِج — وهذا مقصود: المشغّل مبنيٌّ ومُختبَر، والترحيل
步骤 تالية.

## 2) الاكتشاف الجوهري (يُغيّر ترتيب التنفيذ)

**كل مواقع الـ26 اليوم خارج معاملة عملها.** السبب: `HeadAlertService.CreateAsync` يفتح
معاملة **خاصة** به (`HeadAlertService.cs:148`)، فالتنبيه يُنشأ في معاملة مستقلة **بعد**
التزام العملية الأصلية. لذلك:

- «نقل الاستدعاء داخل الـ`tx.RunAsync`» لا يكفي — يجب أيضًا **إلغاء** المعاملة الخاصة
  بخدمة التنبيهات، وإلا remainedت المعاملتان.
- الفخ الجوهري: `catch` يبتلع الفشل يسجّل `head_alert_failed` ويكمل — وهو ما يجعل
  التنبيه **مفقودًا بصمت** اليوم. الخطة (§5.3) تشترط حذفه.

## 3) مصفوفة المواقع (مقروءة من الكود لا مقدَّرة)

| # | الموقع | النوع (Kind) | الجمهور | الفاعل (CreatedBy) | المستلِم | مالك المعاملة | موضع التسجيل | حدّ الحفظ | `catch` يبتلع؟ | الروابط الدلالية |
|---|---|---|---|---|---|---|---|---|---|---|
| D1 | `DocumentDelegationService.cs:170` | `delegation_pending_approval` | SingleHead | `userId` | `ownerId` | `CreateAsync` (tx@98) | **خارج** | معاملة منفصلة | نعم (179) | `DocumentId`+`DelegationId` |
| D2 | `DocumentDelegationService.cs:279` | `delegation_pending_approval` | SingleHead | `userId` | `rebuildOwner` | `UpdateAsync` (tx@211) | **خارج** | — | نعم (288) | `DocumentId`+`DelegationId` |
| D3 | `DocumentDelegationService.cs:302` (تحديث) | `delegation_pending_approval` | SingleHead | `userId` | القائم | `UpdateAsync` (tx@211) | **خارج** | — | نعم (304) | `DelegationId` |
| D4 | `DocumentDelegationService.cs:562` | `delegation_assigned_to_lawyer` | SingleLawyer | `userId` | `target.CreatedById` | `AssignAsync` (tx@492) | **خارج** | — | نعم (569) | `DocumentId` فقط ⚠️ ينقص `DelegationId` |
| D5 | `DocumentDelegationService.cs:710` | `delegation_sent_to_subhead` | SingleHead | `userId` | `subHead.Id` | `RedirectToSectionAsync` (tx@688) | **خارج** | — | نعم (719) | `DocumentId`+`DelegationId` |
| D6 | `DocumentDelegationService.cs:784` | `delegation_pending_approval` | SingleHead | `userId` | `userId` | `RecallRedirectAsync` (tx@762) | **خارج** | — | نعم (793) | `DocumentId`+`DelegationId` |
| D7 | `DocumentDelegationService.cs:872` | `delegation_rejected_wrong_court` | DocumentCreator | `userId` | منشئ الملف | `RejectAsync` (tx@841) | **خارج** | — | نعم (880) + مسار بلا catch (862) | `DocumentId`+`DelegationId` |
| D8 | `DocumentDelegationService.cs:995` | `delegation_pending_completion` | SingleHead | `userId` | `completionOwnerId` | `RegisterAsync` (tx@939) | **خارج** | — | نعم (1004) + بلا catch (969) | `DocumentId`+`DelegationId` |
| D9 | `DocumentDelegationService.cs:1178` | `delegation_completed_returned` | DocumentCreator | `userId` | منشئ الملف | `CompleteAsync` (tx@1078) | **خارج** | — | نعم (1185) + بلا catch (1160) | `DocumentId` فقط ⚠️ ينقص `DelegationId` |
| M1 | `DocumentDelegationService.Mirror.cs:923` (تحديث) | يُقسَّم إلى 7 أنواع (§4.3) | SingleLawyer | `intent.ActorUserId` | `intent.RecipientLawyerId` | **لا معاملة في الملف** | **خارج** | معاملة منفصلة | نعم (927) | `DelegationId`+`TargetLawyerId` |
| M3 | `Mirror.cs:1037` | `delegation_mirror_local_addition` | SingleLawyer | `target.CreatedById` | `source.CreatedById` | `FireTargetEditReportAsync` (خارج) | **خارج** | — | نعم (1007) | `DelegationId` |
| M4 | `Mirror.cs:994` | `delegation_mirror_status_changed` | SingleLawyer | `doc.CreatedById` | `target.CreatedById` | `FireStatusChangeAlertsAsync` (خارج) | **خارج** | — | نعم (927) | `DelegationId` |
| M5 | `Mirror.cs:979` (T2) | `delegation_mirror_deferred` | SingleLawyer | `doc.CreatedById` | `target.CreatedById` | `DocumentStatusService` (خارج) | **خارج** | — | نعم (927/945) | `DelegationId` |
| M6 | `Mirror.cs:983` (T3) | `delegation_mirror_returned` | SingleLawyer | `doc.CreatedById` | `target.CreatedById` | `DocumentStatusService` (خارج) | **خارج** | — | نعم (927) | `DelegationId` |
| M7 | `Mirror.cs:987` (T4) | `delegation_mirror_settled` | SingleLawyer | `doc.CreatedById` | `target.CreatedById` | `DocumentStatusService` (خارج) | **خارج** | — | نعم (927) | `DelegationId` |
| M8 | `Mirror.cs:991` (T5) | `delegation_mirror_fully_forcibly` | SingleLawyer | `doc.CreatedById` | `target.CreatedById` | `DocumentStatusService` (خارج) | **خارج** | — | نعم (927) | `DelegationId` |
| A1 | `DocumentAppealService.cs:543` | `appeal_referral_to_head` | SingleHead | `caller.Id` | `head.Id` | `ForwardAsync` (tx@507) | **خارج** | — | نعم (552) | `DocumentId`+`AppealId` |
| A2 | `DocumentAppealService.cs:626` | `appeal_returned_to_head` | SingleHead | `caller.Id` | `forwarder.Id` | `RecallForwardAsync` (tx@593) | **خارج** | — | نعم (636) | `DocumentId`+`AppealId` |
| A3 | `DocumentAppealService.cs:1219` | `appeal_pending_to_owner` | SingleHead | `userId` | `owner.Id` | `CreateAsync` (tx@87) | **خارج** | — | نعم (1228) + بلا catch (1198/1214) | `DocumentId`+`AppealId` |
| A4 | `DocumentAppealService.cs:1243` | `appeal_assigned_to_lawyer` | SingleLawyer | `userId` | `lawyer.Id` | `AssignAsync` (tx@352) / `TransferAsync` (tx@397) | **خارج** | — | نعم (1251) | `DocumentId`+`AppealId` |
| A5 | `DocumentAppealService.cs:1271` | `appeal_final_to_base_lawyer` | DocumentCreator | `appeal.AssignedLawyerId ?? source.CreatedById` | منشئ الملف الأساس | `DecideAsync` (tx@277) / `StrikeAsync` (tx@315) | **خارج** | — | نعم (1279) | `DocumentId`+`AppealId` |
| C1 | `ExecutionCircuitService.cs:526` | `circuit_transferred` | SingleHead | `actorUserId` | `newHead.Id` | `TransferCircuitAsync` (tx@452) | **داخل** ✔ | 535 | لا | ⚠️ ينقص `ExecutionCircuitId` |
| C2 | `ExecutionCircuitService.cs:879` | `circuit_pending_documents` | SingleLawyer | `actorUserId` | `lawyerId` | `ReferFilesCoreAsync` (tx@634) / `SyncPendingAlertsAfterTransferAsync` (tx@898) | **داخل** ✔ | 840/908 | لا | ⚠️ ينقص `ExecutionCircuitId` |
| C2b | `ExecutionCircuitService.cs:852` (تحديث) | `circuit_pending_documents` | SingleLawyer | `actorUserId` | `lawyerId` | `ReferFilesCoreAsync` | **داخل** ✔ | 840 | لا | — |
| C3 | `ExecutionCircuitService.cs:330` | `circuit_renamed` (بلا `Kind`) | — | `actorUserId ?? circuit.CreatedById` | — | `RenameAsync` (tx@269) | **داخل** ✔ | 337 | لا | `ExecutionCircuitId` |
| P1 | `PublicEntityService.cs:76` | `entity_rename_notice_to_creator` | SingleLawyer/SingleHead | `entry.ReviewedById ?? creator.Id` | `creator.Id` | `Registry.UpdateAsync` (tx@525) / `Branches.RenameBranchAsync` (tx@240) | **داخل** ✔ | 539 / ⚠️لا حفظ بعد 284 | لا | ⚠️ ينقص `PublicEntityId` |
| P2 | `PublicEntityService.cs:600` | `entity_*_lawyers` (4) | BranchLawyers | `actorUserId` | محامو الفرع | `Groups.RenameGroupAsync` / `Merge.CommitMergeCoreAsync` / `Groups.AbolishAndReplaceAsync` / `Unify.UnifyNamesCoreAsync` | **داخل** ✔ | 136/346/368/⚠️ | لا | `PublicEntityId` |
| P3 | `PublicEntityService.cs:623` | `entity_*_heads` (4) | BranchHeads | `actorUserId` | رؤساء الفرع | نفسها | **داخل** ✔ | نفسها | لا | `PublicEntityId` |
| R1 | `PublicEntityService.Registry.cs:390` | `entity_registry_entry_review` **متقاعد** | SingleHead | ⚠️ `head.Id` (خطأ: الفاعل هو الرئيس) | `head.Id` | `Registry.CreateAsync` (tx@326) | **داخل** ✔ | 401 | لا | `PublicEntityId` |
| R2 | `PublicEntityService.Registry.cs:418` (تحديث) | `entity_edit_proposal_review` **متقاعد** | SingleHead | `actorUserId` | `headId` | `Registry.ProposeEditAsync` (tx@256) | **داخل** ✔ | 293 | لا | `PublicEntityId` ✔ |
| V1 | `PublicEntityService.Moves.cs:191` | `entity_entry_moved` | SingleHead | `actor.UserId` | `head.Id` | `MoveEntryAsync` (tx@24) | **داخل** ✔ | 203 | لا | ⚠️ ينقص `PublicEntityId` |
| V2 | `PublicEntityService.Moves.cs:314` | `entity_entries_moved_all` | SingleHead | `actor.UserId` | `head.Id` | `MoveAllEntriesAsync` (tx@217) | **داخل** ✔ | 327 | لا | ⚠️ ينقص `PublicEntityId` |
| B1 | `PublicEntityService.Branches.cs:709` | `entity_branch_changed` (4 متغيرات) | SingleHead | `actor.UserId` | `head.Id` | 4 مستدعين (tx@240/307/408/546) | **داخل** ✔ | 279/366/503/619 | لا | `PublicEntityId` ✔ |
| RL1 | `ReviewLetterService.cs:405` (+ تحديث 397) | `review_letter_reply` | SingleLawyer/SingleHead | `headUserId` | `letter.CreatedById` | `ReplyAsync` (tx@371) | **داخل** ✔ | 376 | لا | `DocumentId`+`ReviewLetterId`+`TargetLawyerId` |

### مواقع ما بعد الالتزام (يجب نقلها إلى داخل معاملة عملها)

| # | الموقع | النوع | ملاحظة |
|---|---|---|---|
| P-1 | `DocumentService.cs:290` `FireMirrorNotificationsAsync` | `delegation_mirror_updated` | معاملة `UpdateAsync` تنتهي في 285 والنداء 290 — بعد الالتزام |
| P-2 | `DocumentService.cs:292` `FireTargetEditReportAsync` | `delegation_mirror_local_addition` | نفس |
| P-3 | `DocumentStatusService.cs:331` `CleanupPendingCompletionAlertsAsync` | أرشفة (لا إنشاء) | مسار حذف → `ArchiveByDelegationAsync` |
| P-4 | `DocumentStatusService.cs:336` | `delegation_mirror_deferred/settled/fully_forcibly` | بعد الالتزام |
| P-5 | `DocumentStatusService.cs:420` | `delegation_mirror_returned` | بعد الالتزام |
| P-6 | `DocumentStatusService.cs:628` | `delegation_mirror_fully_forcibly` | بعد الالتزام |
| P-7 | `DocumentStatusService.cs:850` | `delegation_mirror_status_changed` | بعد الالتزام |
| P-8 | `DocumentsController.cs:662` | `circuit_pending_documents` | المتحتكم بلا معاملة |
| P-9 | `DocumentsController.cs:700` | `circuit_pending_documents` | المتحكم بلا معاملة |

## 4) عيوب مكتشفة يجب إصلاحها أثناء الترحيل

1. **`BoundAlertMessage`** (`Mirror.cs:1046`) يقصّ عند 2000 حرف — يجب إلغاؤه (§3.1-5).
2. **قصّ 80 حرفًا لرد المطالعة** (`ReviewLetterService.cs`) — يُخزَّن `BodyPlainText` كاملًا (§3.1-6).
3. **`PendingAlertPrefix`/`BuildPendingMessage`/`FindByMessageAndBranchAsync`** — مطابقة نصية تُحذف.
4. **قصّ 2000 حرف** في `PublicEntityService.cs:82` و`611` و`633` — يُحذف (§3.1-5).
5. **`UpdateDelegationAlertAsync`** — تُحذف ككتابة مباشرة (تُستبدل.Outbox).
6. **`UpsertDelegationMessageAsync`** — تُحذف ككتابة مباشرة؛ تُقسَّم إلى 7 أنواع (§4.3).
7. **الرؤساء في `InsertBranchChangeAlertAsync`** — بث صف/فرع بلا `DedupeKey` لكل رأس (§8.4).
8. **`InsertEntryReviewAlertsAsync` / `UpsertEditProposalAlertAsync`** — تُحذف (متقاعدان) وتُنقل
   متابعتهما إلى سجل المراجعة (§4.1، §8.4).

## 5) ترتيب التنفيذ المقترح (كل خطوة تُبقي الشجرة خضراء)

1. **ترحيل المواقع «داخل المعاملة» أولًا** (C1/C2/C2b/C3/P1/P2/P3/R1/R2/V1/V2/B1/RL1) — هي
   داخل معاملتها أصلًا، فالنقل سهل، وإلغاء المعاملة الخاصة بخدمة التنبيهات هو الأثر الجوهري.
2. **ترحيل المواقع «خارج المعاملة»** (D1–D9/M1–M8/A1–A5) — نقل الاستدعاء داخل الـ`tx` +
   إلغاء الـ`catch` المبتلع + حذف `head_alert_failed`.
3. **نقل مسارات ما بعد الالتزام** (P-1…P-9) إلى داخل معاملة عملها.
4. **تنظيف `CreateHeadAlertRequest`** من حقوله الداخلية (ينتقل حينها كل نداء النظام إلى
   `CreateSystemHeadAlertRequest`).
5. **هجرة `M2`** (التصنيف + حل التصادم + دمج المستلمين + الفهرس الفريد الجزئي + قيد
   `(HeadAlertId, UserId)` + FK `ExecutionCircuitId`).
6. **الجسر الانتقالي** خلف `HeadAlerts:LegacyBridge:Enabled=false` — **يُفعَّل بقرار G0 فقط**.
7. **اختبار معماري** يمنع `new HeadAlert`/`CreateSystemAsync` خارج `HeadAlertService`/المشغّل.

---

*لقطة مؤرخة — أي تحديث يكون ملحقًا جديدًا مؤرخًا لا تحريرًا لهذا الملف.*
