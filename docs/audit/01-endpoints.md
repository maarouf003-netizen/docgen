# جرد النقاط الكامل — `DocGenerator.Api`

> التقاطع: 17 ملف متحكم على القرص = 18 صنفًا (`PortalController.cs` يحوي صنفين). المجموع **184** نقطة.
> التسجيل: `AddControllers()` في `backend/src/DocGenerator.Api/Program.cs:148` و`MapControllers()` في `Program.cs:310` — لا توجد مسارات تقليدية، كل المسارات من `[Route]` + `[HttpX]`.
> المصادقة: `AddJwtBearer` في `Program.cs:152-199` (تحقق `IssuerSigningKey/Issuer/Audience/Lifetime` في `Program.cs:155-167`، `ClockSkew=1min`، `ValidAlgorithms=[HmacSha256]` فقط، حارس السر `>=32 bytes` في `Program.cs:93-95`).
> التفويض: معظم النقاط `[Authorize]` عارٍ + فحص برمجي عبر `RolePermissions.*` داخل الأكشن؛ الأدوار النصية: `manager,admin,head,lawyer,entitymanager`.
> الحد المعدلي: `AddRateLimiter` في `Program.cs:151` و`UseRateLimiter()` في `Program.cs:302` (بعد المصادقة عمدًا للتقسيم لكل `userId`).

| Method | Route | Controller.Action | Auth | DTO | file:line |
|---|---|---|---|---|---|
| GET | `api/alerts` | Alerts.List | `[Authorize]` | — | `AlertsController.cs:35` |
| GET | `api/alerts/unread-count` | Alerts.UnreadCount | `[Authorize]` | — | `AlertsController.cs:52` |
| POST | `api/alerts` | Alerts.Create | `[Authorize]` | `CreateHeadAlertRequest` | `AlertsController.cs:63` |
| GET | `api/alerts/{id:int}` | Alerts.Get | `[Authorize]` | — | `AlertsController.cs:84` |
| GET | `api/alerts/by-delegation/{delegationId:int}` | Alerts.ByDelegation | `[Authorize]` | — | `AlertsController.cs:111` |
| PATCH | `api/alerts/{id:int}/read` | Alerts.MarkRead | `[Authorize]` | — | `AlertsController.cs:123` |
| GET | `api/documents/{documentId:int}/appeals` | Appeals.ListForDocument | `[Authorize]` | — | `AppealsController.cs:62` |
| POST | `api/documents/{documentId:int}/appeals` | Appeals.Create | `[Authorize]` | `UpsertAppealRequest` | `AppealsController.cs:81` |
| GET | `api/appeals` | Appeals.Search | `[Authorize]` | `query: q,status,page,perPage` | `AppealsController.cs:100` |
| GET | `api/appeals/{id:int}` | Appeals.Get | `[Authorize]` | — | `AppealsController.cs:122` |
| PUT | `api/appeals/{id:int}` | Appeals.Update | `[Authorize]` | `UpsertAppealRequest` | `AppealsController.cs:132` |
| DELETE | `api/appeals/{id:int}` | Appeals.Delete | `[Authorize]` | — | `AppealsController.cs:149` |
| PUT | `api/appeals/{id:int}/registration` | Appeals.UpdateRegistration | `[Authorize]` | `UpdateAppealRegistrationRequest` | `AppealsController.cs:168` |
| POST | `api/appeals/{id:int}/decide` | Appeals.Decide | `[Authorize]` | `DecideAppealRequest` | `AppealsController.cs:185` |
| POST | `api/appeals/{id:int}/strike` | Appeals.Strike | `[Authorize]` | `StrikeAppealRequest` | `AppealsController.cs:202` |
| POST | `api/appeals/{id:int}/assign` | Appeals.Assign | `[Authorize]` | `AssignAppealRequest` | `AppealsController.cs:221` |
| POST | `api/appeals/{id:int}/transfer` | Appeals.Transfer | `[Authorize]` | `TransferAppealRequest` | `AppealsController.cs:238` |
| POST | `api/appeals/transfer-all` | Appeals.TransferAll | `[Authorize]` | `TransferAllAppealsRequest` | `AppealsController.cs:255` |
| GET | `api/appeals/owner/{lawyerId:int}/count` | Appeals.CountForOwner | `[Authorize]` | — | `AppealsController.cs:272` |
| GET | `api/appeals/{id:int}/base-numbers` | Appeals.GetBaseNumbers | `[Authorize]` | — | `AppealsController.cs:290` |
| PUT | `api/appeals/{id:int}/base-numbers` | Appeals.SaveBaseNumbers | `[Authorize]` | `SaveAppealBaseNumbersRequest` | `AppealsController.cs:306` |
| GET | `api/appeals/{id:int}/actions` | Appeals.GetActions | `[Authorize]` | — | `AppealsController.cs:325` |
| POST | `api/appeals/{id:int}/actions` | Appeals.AddAction | `[Authorize]` | `AddAppealActionRequest` | `AppealsController.cs:341` |
| PUT | `api/appeals/{id:int}/actions/{actionId:int}` | Appeals.UpdateAction | `[Authorize]` | `UpdateAppealActionRequest` | `AppealsController.cs:357` |
| DELETE | `api/appeals/{id:int}/actions/{actionId:int}` | Appeals.DeleteAction | `[Authorize]` | — | `AppealsController.cs:374` |
| DELETE | `api/appeals/{id:int}/actions/{actionId:int}/reminder` | Appeals.ClearReminder | `[Authorize]` | — | `AppealsController.cs:391` |
| GET | `api/appeals/reminders` | Appeals.Reminders | `[Authorize]` + `[Authorize(Roles="lawyer")]` | — | `AppealsController.cs:408` |
| GET | `api/app-suggestions` | AppSuggestions.List | `[Authorize]` | `query: page,perPage` | `AppSuggestionsController.cs:33` |
| GET | `api/app-suggestions/{id:int}` | AppSuggestions.Get | `[Authorize]` | — | `AppSuggestionsController.cs:45` |
| POST | `api/app-suggestions` | AppSuggestions.Create | `[Authorize]` + `ExpensivePolicy` | `CreateAppSuggestionRequest` | `AppSuggestionsController.cs:55` |
| PATCH | `api/app-suggestions/{id:int}/read` | AppSuggestions.MarkRead | `[Authorize]` | — | `AppSuggestionsController.cs:73` |
| GET | `api/audit-logs` | AuditLogs.Search | `[Authorize(Roles="manager,admin,head")]` | `query: userName,actionType,page,perPage` | `AuditLogsController.cs:18` |
| POST | `api/auth/login` | Auth.Login | `[AllowAnonymous]` + `LoginIpPolicy` | `LoginRequest` | `AuthController.cs:42` |
| POST | `api/auth/logout` | Auth.Logout | `[AllowAnonymous]` | — | `AuthController.cs:99` |
| POST | `api/auth/change-password` | Auth.ChangePassword | `[Authorize]` + `PasswordPolicy` | `ChangePasswordRequest` | `AuthController.cs:109` |
| GET | `api/auth/me` | Auth.Me | `[Authorize]` | — | `AuthController.cs:127` |
| GET | `api/branches` | Branches.List | `[Authorize]` | — | `BranchesController.cs:28` |
| POST | `api/branches` | Branches.Create | `[Authorize]` | `CreateBranchRequest` | `BranchesController.cs:35` |
| GET | `api/branches/{id:int}` | Branches.Get | `[Authorize]` | — | `BranchesController.cs:51` |
| PUT | `api/branches/{id:int}` | Branches.Update | `[Authorize]` | `UpdateBranchRequest` | `BranchesController.cs:59` |
| DELETE | `api/branches/{id:int}` | Branches.Delete | `[Authorize]` | — | `BranchesController.cs:79` |
| POST | `api/client-errors` | ClientErrors.Report | `[Authorize]` | `ClientErrorReport` | `ClientErrorsController.cs:36` |
| GET | `api/correspondence` | Correspondences.Search | `[Authorize]` | `query: q,governorate,importance,page,perPage` | `CorrespondencesController.cs:36` |
| GET | `api/correspondence/filter-options` | Correspondences.GetFilterOptions | `[Authorize]` | — | `CorrespondencesController.cs:55` |
| GET | `api/correspondence/targets` | Correspondences.SearchTargets | `[Authorize]` | `query: q,documentId` | `CorrespondencesController.cs:66` |
| GET | `api/correspondence/urgent-unseen-count` | Correspondences.UrgentUnseenCount | `[Authorize]` | — | `CorrespondencesController.cs:88` |
| GET | `api/correspondence/document/{documentId:int}` | Correspondences.ListByDocument | `[Authorize]` | — | `CorrespondencesController.cs:99` |
| POST | `api/correspondence` | Correspondences.Create | `[Authorize]` | `CreateCorrespondenceRequest` | `CorrespondencesController.cs:122` |
| GET | `api/correspondence/{id:int}` | Correspondences.Get | `[Authorize]` | — | `CorrespondencesController.cs:146` |
| POST | `api/correspondence/{id:int}/addenda` | Correspondences.AddAddendum | `[Authorize]` | `AddCorrespondenceAddendumRequest` | `CorrespondencesController.cs:169` |
| POST | `api/correspondence/{id:int}/replies` | Correspondences.Reply | `[Authorize]` | `ReplyCorrespondenceRequest` | `CorrespondencesController.cs:196` |
| POST | `api/correspondence/{id:int}/mark-seen` | Correspondences.MarkSeen | `[Authorize]` | — | `CorrespondencesController.cs:223` |
| GET | `api/documents/{documentId:int}/delegations` | Delegations.ListForDocument | `[Authorize]` | — | `DelegationsController.cs:48` |
| POST | `api/documents/{documentId:int}/delegations` | Delegations.Create | `[Authorize]` | `UpsertDelegationRequest` | `DelegationsController.cs:67` |
| PUT | `api/delegations/{id:int}` | Delegations.Update | `[Authorize]` | `UpsertDelegationRequest` | `DelegationsController.cs:84` |
| DELETE | `api/delegations/{id:int}` | Delegations.Delete | `[Authorize]` | — | `DelegationsController.cs:101` |
| GET | `api/delegations/pending` | Delegations.Pending | `[Authorize]` | — | `DelegationsController.cs:118` |
| GET | `api/delegations/pending-count` | Delegations.PendingCount | `[Authorize]` | — | `DelegationsController.cs:130` |
| POST | `api/delegations/{id:int}/assign` | Delegations.Assign | `[Authorize]` | `AssignDelegationRequest` | `DelegationsController.cs:145` |
| POST | `api/delegations/{id:int}/register` | Delegations.Register | `[Authorize]` | `RegisterDelegationRequest` | `DelegationsController.cs:162` |
| POST | `api/delegations/{id:int}/complete` | Delegations.Complete | `[Authorize]` | `CompleteDelegationRequest` | `DelegationsController.cs:182` |
| GET | `api/documents` | Documents.Search | `[Authorize]` | `query: q,status,applicant,court,lawyer,branch,...` | `DocumentsController.cs:94` |
| GET | `api/documents/filter-options` | Documents.GetFilterOptions | `[Authorize]` | `query: status,applicant,court,lawyer,branch,...` | `DocumentsController.cs:115` |
| GET | `api/documents/export` | Documents.Export | `[Authorize]` + `ExpensivePolicy` | `query: same as Search (no paging)` | `DocumentsController.cs:139` |
| GET | `api/documents/deleted` | Documents.GetDeleted | `[Authorize]` | `query: q,page,perPage` | `DocumentsController.cs:170` |
| GET | `api/documents/struck-off` | Documents.GetStruckOff | `[Authorize]` | `query: q,page,perPage` | `DocumentsController.cs:186` |
| GET | `api/documents/executed` | Documents.GetExecuted | `[Authorize]` | `query: q,page,perPage` | `DocumentsController.cs:203` |
| GET | `api/documents/referred-to-start` | Documents.GetReferredToStart | `[Authorize]` | `query: q,page,perPage` | `DocumentsController.cs:217` |
| GET | `api/documents/{id:int}` | Documents.Get | `[Authorize]` | — | `DocumentsController.cs:231` |
| GET | `api/documents/{id:int}/base-numbers` | Documents.GetBaseNumberHistory | `[Authorize]` | — | `DocumentsController.cs:240` |
| GET | `api/documents/{id:int}/changes` | Documents.GetChanges | `[Authorize]` | `query: page,perPage` | `DocumentsController.cs:255` |
| POST | `api/documents` | Documents.Create | `[Authorize]` | `DocumentUpsertRequest` | `DocumentsController.cs:264` |
| PUT | `api/documents/{id:int}` | Documents.Update | `[Authorize]` | `DocumentUpsertRequest` | `DocumentsController.cs:282` |
| GET | `api/documents/rotate` | Documents.GetRotationList | `[Authorize]` | `query: page,perPage` | `DocumentsController.cs:296` |
| PUT | `api/documents/rotate` | Documents.SaveBaseNumbers | `[Authorize]` | `SaveBaseNumbersRequest` | `DocumentsController.cs:306` |
| DELETE | `api/documents/{id:int}` | Documents.Delete | `[Authorize]` | — | `DocumentsController.cs:324` |
| POST | `api/documents/{id:int}/restore` | Documents.Restore | `[Authorize]` | — | `DocumentsController.cs:338` |
| POST | `api/documents/{id:int}/status` | Documents.SetStatus | `[Authorize]` | `StatusRequest` | `DocumentsController.cs:354` |
| POST | `api/documents/{id:int}/revert-status` | Documents.RevertStatus | `[Authorize]` | `StatusRequest` | `DocumentsController.cs:375` |
| POST | `api/documents/{id:int}/return-referred-to-start` | Documents.ReturnFromReferredToStart | `[Authorize]` | `ReturnReferredToStartRequest` | `DocumentsController.cs:397` |
| POST | `api/documents/{id:int}/consider-executed-by-delegation` | Documents.ConsiderExecutedByDelegation | `[Authorize]` | `StatusRequest` | `DocumentsController.cs:420` |
| POST | `api/documents/{id:int}/executed-status` | Documents.SetExecutedStatus | `[Authorize]` | `ExecutedStatusRequest` | `DocumentsController.cs:443` |
| POST | `api/documents/{id:int}/restore-struck-off` | Documents.RestoreStruckOff | `[Authorize]` | `RenewalRequest` | `DocumentsController.cs:465` |
| POST | `api/documents/{id:int}/view` | Documents.TrackView | `[Authorize]` | — | `DocumentsController.cs:488` |
| GET | `api/documents/{id:int}/generate` | Documents.Generate | `[Authorize]` + `ExpensivePolicy` | `query: template,recipient,estateIds,heirId` | `DocumentsController.cs:498` |
| GET | `api/documents/owner/{lawyerId:int}/count` | Documents.CountFilesByOwner | `[Authorize]` | — | `DocumentsController.cs:539` |
| POST | `api/documents/transfer-all` | Documents.TransferAll | `[Authorize]` | `TransferAllRequest` | `DocumentsController.cs:562` |
| POST | `api/documents/{id:int}/transfer` | Documents.Transfer | `[Authorize]` | `TransferDocumentRequest` | `DocumentsController.cs:594` |
| GET | `api/documents/{id:int}/actions` | Documents.GetActions | `[Authorize]` | — | `DocumentsController.cs:624` |
| POST | `api/documents/{id:int}/actions` | Documents.AddAction | `[Authorize]` | `AddExecutionActionRequest` | `DocumentsController.cs:635` |
| PUT | `api/documents/{id:int}/actions/{actionId:int}` | Documents.UpdateAction | `[Authorize]` | `UpdateExecutionActionRequest` | `DocumentsController.cs:657` |
| DELETE | `api/documents/{id:int}/actions/{actionId:int}` | Documents.DeleteAction | `[Authorize]` | — | `DocumentsController.cs:680` |
| DELETE | `api/documents/{id:int}/actions/{actionId:int}/reminder` | Documents.ClearReminder | `[Authorize]` | — | `DocumentsController.cs:696` |
| GET | `api/entity-registry` | EntityRegistry.List | `[Authorize]` | `query: q,governorate,status,branchName,page,perPage` | `EntityRegistryController.cs:30` |
| GET | `api/entity-registry/search` | EntityRegistry.Search | `[Authorize]` | `query: q,governorate,branchName` | `EntityRegistryController.cs:51` |
| POST | `api/entity-registry` | EntityRegistry.Create | `[Authorize]` | `CreatePublicEntityRequest` | `EntityRegistryController.cs:69` |
| PUT | `api/entity-registry/{id:int}` | EntityRegistry.Update | `[Authorize]` | `UpdatePublicEntityRequest` | `EntityRegistryController.cs:89` |
| POST | `api/entity-registry/{id:int}/aliases` | EntityRegistry.AddAlias | `[Authorize]` | `AddPublicEntityAliasRequest` | `EntityRegistryController.cs:110` |
| POST | `api/entity-registry/{id:int}/propose-edit` | EntityRegistry.ProposeEdit | `[Authorize]` | `ProposeEditRequest` | `EntityRegistryController.cs:131` |
| GET | `api/entity-registry/pending-review` | EntityRegistry.PendingReview | `[Authorize]` | — | `EntityRegistryController.cs:157` |
| GET | `api/entity-registry/pending-review-count` | EntityRegistry.PendingReviewCount | `[Authorize]` | — | `EntityRegistryController.cs:166` |
| POST | `api/entity-registry/{id:int}/approve-review` | EntityRegistry.ApproveReview | `[Authorize]` | — | `EntityRegistryController.cs:175` |
| POST | `api/entity-registry/import-preview` | EntityRegistry.ImportPreview | `[Authorize]` | — | `EntityRegistryController.cs:193` |
| POST | `api/entity-registry/import-commit` | EntityRegistry.ImportCommit | `[Authorize]` | `ImportCommitRequest` | `EntityRegistryController.cs:202` |
| POST | `api/entity-registry/{id:int}/move` | EntityRegistry.MoveEntry | `[Authorize]` | `MoveEntryRequest` | `EntityRegistryController.cs:219` |
| POST | `api/entity-registry/move-all` | EntityRegistry.MoveAllEntries | `[Authorize]` | `MoveAllEntriesRequest` | `EntityRegistryController.cs:238` |
| GET | `api/entity-registry/groups` | EntityRegistry.ListGroups | `[Authorize]` | `query: q,governorate,excludeIds,includeIds,page,perPage` | `EntityRegistryController.cs:261` |
| GET | `api/entity-registry/groups/{groupId:int}/entries` | EntityRegistry.ListGroupEntries | `[Authorize]` | — | `EntityRegistryController.cs:287` |
| GET | `api/entity-registry/groups/{groupId:int}/similar-to` | EntityRegistry.SimilarTo | `[Authorize]` | `query: threshold,maxResults` | `EntityRegistryController.cs:303` |
| POST | `api/entity-registry/groups/unify-preview` | EntityRegistry.UnifyPreview | `[Authorize]` | `UnifyNamesPreviewRequest` | `EntityRegistryController.cs:319` |
| POST | `api/entity-registry/groups/unify` | EntityRegistry.Unify | `[Authorize]` | `UnifyNamesRequest` | `EntityRegistryController.cs:335` |
| POST | `api/entity-registry/merge-preview` | EntityRegistry.MergePreview | `[Authorize]` | `MergePreviewRequest` | `EntityRegistryController.cs:358` |
| POST | `api/entity-registry/merge-commit` | EntityRegistry.MergeCommit | `[Authorize]` | `MergeCommitRequest` | `EntityRegistryController.cs:374` |
| POST | `api/entity-registry/groups/{groupId:int}/rename-preview` | EntityRegistry.RenameGroupPreview | `[Authorize]` | `RenameGroupPreviewRequest` | `EntityRegistryController.cs:395` |
| POST | `api/entity-registry/groups/{groupId:int}/rename` | EntityRegistry.RenameGroup | `[Authorize]` | `RenameGroupRequest` | `EntityRegistryController.cs:414` |
| POST | `api/entity-registry/groups/abolish-preview` | EntityRegistry.AbolishPreview | `[Authorize]` | `AbolishReplacePreviewRequest` | `EntityRegistryController.cs:438` |
| POST | `api/entity-registry/groups/abolish-and-replace` | EntityRegistry.AbolishAndReplace | `[Authorize]` | `AbolishAndReplaceRequest` | `EntityRegistryController.cs:454` |
| GET | `api/entity-registry/change-events` | EntityRegistry.ListChangeEvents | `[Authorize]` | `query: governorate,actionKind,actorUserId,from,to,page,perPage` | `EntityRegistryController.cs:473` |
| GET | `api/entity-registry/change-events/export` | EntityRegistry.ExportChangeEvents | `[Authorize]` | `query: governorate,actionKind,actorUserId,from,to` | `EntityRegistryController.cs:498` |
| POST | `api/entity-registry/groups/{groupId:int}/branches/preview` | EntityRegistry.PreviewBranchAction | `[Authorize]` | `PreviewBranchActionRequest` | `EntityRegistryController.cs:524` |
| POST | `api/entity-registry/groups/{groupId:int}/branches/{entryId:int}/rename-branch` | EntityRegistry.RenameBranch | `[Authorize]` | `RenameBranchRequest` | `EntityRegistryController.cs:544` |
| POST | `api/entity-registry/groups/{groupId:int}/branches/merge` | EntityRegistry.MergeBranches | `[Authorize]` | `MergeBranchesRequest` | `EntityRegistryController.cs:564` |
| POST | `api/entity-registry/groups/{groupId:int}/branches/{entryId:int}/abolish` | EntityRegistry.AbolishBranch | `[Authorize]` | `AbolishBranchRequest` | `EntityRegistryController.cs:584` |
| POST | `api/entity-registry/groups/{groupId:int}/branches/unify` | EntityRegistry.UnifyBranches | `[Authorize]` | `UnifyBranchesRequest` | `EntityRegistryController.cs:604` |
| POST | `api/entity-registry/entries/{entryId:int}/suggest-parent-edit` | EntityRegistry.SuggestParentEdit | `[Authorize]` | `SuggestParentEditRequest` | `EntityRegistryController.cs:626` |
| GET | `api/entity-registry/parent-edit-suggestions` | EntityRegistry.ListParentEditSuggestions | `[Authorize]` | `query: status,groupId,page,perPage` | `EntityRegistryController.cs:646` |
| POST | `api/entity-registry/parent-edit-suggestions/{suggestionId:int}/review` | EntityRegistry.ReviewParentEditSuggestion | `[Authorize]` | `ReviewParentEditSuggestionRequest` | `EntityRegistryController.cs:662` |
| POST | `api/entity-registry/parent-edit-suggestions/{suggestionId:int}/withdraw` | EntityRegistry.WithdrawParentEditSuggestion | `[Authorize]` | — | `EntityRegistryController.cs:684` |
| GET | `api/meta/current-year` | Meta.CurrentYear | `[AllowAnonymous]` | — | `MetaController.cs:26` |
| GET | `api/personal-reminders` | PersonalReminders.List | `[Authorize]` | `query: includeArchived` | `PersonalRemindersController.cs:29` |
| POST | `api/personal-reminders` | PersonalReminders.Create | `[Authorize]` + `ExpensivePolicy` | `CreatePersonalReminderRequest` | `PersonalRemindersController.cs:39` |
| GET | `api/personal-reminders/{id:int}` | PersonalReminders.Get | `[Authorize]` | — | `PersonalRemindersController.cs:56` |
| PUT | `api/personal-reminders/{id:int}` | PersonalReminders.Update | `[Authorize]` | `UpdatePersonalReminderRequest` | `PersonalRemindersController.cs:66` |
| PATCH | `api/personal-reminders/{id:int}/occurrences` | PersonalReminders.SetOccurrence | `[Authorize]` + `ExpensivePolicy` | `SetOccurrenceRequest` | `PersonalRemindersController.cs:84` |
| DELETE | `api/personal-reminders/{id:int}` | PersonalReminders.Delete | `[Authorize]` + `ExpensivePolicy` | — | `PersonalRemindersController.cs:102` |
| GET | `api/portal/my-scope` | Portal.MyScope | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:40` |
| GET | `api/portal/files` | Portal.Files | `[Authorize(Roles="entitymanager")]` | `query: q,status,page,perPage,entryId` | `PortalController.cs:45` |
| GET | `api/portal/files/{id:int}` | Portal.File | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:69` |
| GET | `api/portal/files/{id:int}/appeals` | Portal.Appeals | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:77` |
| GET | `api/portal/files/{id:int}/execution-actions` | Portal.ExecutionActions | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:85` |
| GET | `api/portal/files/{id:int}/delegations` | Portal.Delegations | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:93` |
| GET | `api/portal/files/{id:int}/appeals/details` | Portal.AppealDetails | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:101` |
| GET | `api/portal/files/{id:int}/base-numbers` | Portal.BaseNumbers | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:109` |
| GET | `api/portal/stats` | Portal.Stats | `[Authorize(Roles="entitymanager")]` + `ExpensivePolicy` | `query: entryId` | `PortalController.cs:117` |
| GET | `api/portal/export` | Portal.Export | `[Authorize(Roles="entitymanager")]` + `ExpensivePolicy` | `query: q,status,entryId` | `PortalController.cs:132` |
| GET | `api/portal/correspondence` | Portal.CorrespondenceList | `[Authorize(Roles="entitymanager")]` | `query: q,importance,page,perPage` | `PortalController.cs:162` |
| GET | `api/portal/correspondence/targets` | Portal.CorrespondenceTargets | `[Authorize(Roles="entitymanager")]` | `query: q,documentId` | `PortalController.cs:183` |
| GET | `api/portal/correspondence/urgent-unseen-count` | Portal.CorrespondenceUrgentCount | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:208` |
| GET | `api/portal/correspondence/{id:int}` | Portal.CorrespondenceGet | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:208` |
| POST | `api/portal/correspondence` | Portal.CorrespondenceCreate | `[Authorize(Roles="entitymanager")]` | `CreateCorrespondenceRequest` | `PortalController.cs:231` |
| POST | `api/portal/correspondence/{id:int}/addenda` | Portal.CorrespondenceAddAddendum | `[Authorize(Roles="entitymanager")]` | `AddCorrespondenceAddendumRequest` | `PortalController.cs:252` |
| POST | `api/portal/correspondence/{id:int}/replies` | Portal.CorrespondenceReply | `[Authorize(Roles="entitymanager")]` | `ReplyCorrespondenceRequest` | `PortalController.cs:275` |
| POST | `api/portal/correspondence/{id:int}/mark-seen` | Portal.CorrespondenceMarkSeen | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:298` |
| GET | `api/portal/files/{id:int}/correspondence` | Portal.FileCorrespondence | `[Authorize(Roles="entitymanager")]` | — | `PortalController.cs:321` |
| GET | `api/entity-portal/delegates` | Delegates.List | `[Authorize]` | — | `PortalController.cs:351` |
| POST | `api/entity-portal/delegates` | Delegates.Create | `[Authorize]` | `CreateDelegateRequest` | `PortalController.cs:359` |
| PUT | `api/entity-portal/delegates/{id:int}` | Delegates.Update | `[Authorize]` | `UpdateDelegateRequest` | `PortalController.cs:374` |
| GET | `api/review-letters` | ReviewLetters.Search | `[Authorize]` | `query: q,administrativeBranch,page,perPage` | `ReviewLettersController.cs:36` |
| GET | `api/review-letters/filter-options` | ReviewLetters.GetFilterOptions | `[Authorize]` | — | `ReviewLettersController.cs:55` |
| GET | `api/review-letters/pending-count` | ReviewLetters.PendingCount | `[Authorize]` | — | `ReviewLettersController.cs:66` |
| GET | `api/review-letters/unseen-replies-count` | ReviewLetters.UnseenRepliesCount | `[Authorize]` | — | `ReviewLettersController.cs:79` |
| POST | `api/review-letters/{id:int}/mark-replies-seen` | ReviewLetters.MarkRepliesSeen | `[Authorize]` | — | `ReviewLettersController.cs:90` |
| GET | `api/review-letters/document/{documentId:int}` | ReviewLetters.ListByDocument | `[Authorize]` | — | `ReviewLettersController.cs:112` |
| POST | `api/review-letters` | ReviewLetters.Create | `[Authorize]` | `CreateReviewLetterRequest` | `ReviewLettersController.cs:131` |
| GET | `api/review-letters/{id:int}` | ReviewLetters.Get | `[Authorize]` | — | `ReviewLettersController.cs:155` |
| POST | `api/review-letters/{id:int}/addenda` | ReviewLetters.AddAddendum | `[Authorize]` | `AddReviewLetterAddendumRequest` | `ReviewLettersController.cs:174` |
| POST | `api/review-letters/{id:int}/replies` | ReviewLetters.Reply | `[Authorize]` | `ReplyReviewLetterRequest` | `ReviewLettersController.cs:197` |
| GET | `api/dashboard` | Statistics.Dashboard | `[Authorize]` + `[Authorize(Roles="manager,admin,head,lawyer")]` + `ExpensivePolicy` | — | `StatisticsController.cs:46` |
| GET | `api/monthly-stats` | Statistics.Monthly | `[Authorize]` + `[Authorize(Roles="manager,admin,head,lawyer")]` + `ExpensivePolicy` | — | `StatisticsController.cs:62` |
| GET | `api/reminders` | Statistics.Reminders | `[Authorize]` + `[Authorize(Roles="lawyer")]` + `ExpensivePolicy` | — | `StatisticsController.cs:77` |
| GET | `api/branches/summary` | Statistics.BranchesSummary | `[Authorize]` + `[Authorize(Roles="manager,admin")]` + `ExpensivePolicy` | — | `StatisticsController.cs:84` |
| GET | `api/users/activity` | Statistics.UserActivity | `[Authorize]` + `[Authorize(Roles="manager,admin")]` + `ExpensivePolicy` | — | `StatisticsController.cs:89` |
| GET | `api/stats/manager` | Statistics.ManagerStats | `[Authorize]` + `[Authorize(Roles="manager,admin,head")]` + `ExpensivePolicy` | `query: period,branchId,year,month,quarter` | `StatisticsController.cs:94` |
| GET | `api/stats/manager/lawyers` | Statistics.ManagerLawyerStats | `[Authorize]` + `[Authorize(Roles="manager,admin,head")]` + `ExpensivePolicy` | `query: period,branchId,year,month,quarter` | `StatisticsController.cs:119` |
| GET | `api/stats/me` | Statistics.PersonalStats | `[Authorize]` + `[Authorize(Roles="lawyer")]` + `ExpensivePolicy` | `query: period,year,month,quarter` | `StatisticsController.cs:145` |
| GET | `api/stats/periods` | Statistics.AvailablePeriods | `[Authorize]` + `[Authorize(Roles="manager,admin,head,lawyer")]` + `ExpensivePolicy` | `query: branchId` | `StatisticsController.cs:168` |
| GET | `api/users` | UserManagement.ListUsers | `[Authorize]` | — | `UserManagementController.cs:29` |
| POST | `api/users` | UserManagement.CreateUser | `[Authorize]` | `CreateUserRequest` | `UserManagementController.cs:38` |
| GET | `api/users/{id:int}` | UserManagement.GetUser | `[Authorize]` | — | `UserManagementController.cs:54` |
| PUT | `api/users/{id:int}` | UserManagement.UpdateUser | `[Authorize]` | `UpdateUserRequest` | `UserManagementController.cs:63` |
| GET | `api/users/lawyers` | UserManagement.ListLawyers | `[Authorize]` | `query: branchId` | `UserManagementController.cs:79` |
| POST | `api/users/lawyers` | UserManagement.CreateLawyer | `[Authorize]` | `CreateLawyerRequest` | `UserManagementController.cs:93` |
| PUT | `api/users/lawyers/{id:int}` | UserManagement.UpdateLawyer | `[Authorize]` | `UpdateLawyerRequest` | `UserManagementController.cs:125` |
| PATCH | `api/users/{id:int}/active` | UserManagement.SetActive | `[Authorize]` | `SetUserActiveRequest` | `UserManagementController.cs:147` |

## التحقق من الاكتمال

- عدد ملفات المتحكمات على القرص: 17 — VERIFIED (`Get-ChildItem backend/src/DocGenerator.Api/Controllers`).
- عدد الأصناف: 18 — VERIFIED (`PortalController.cs:342-345` يحوي `DelegatesController` ثانيًا).
- عدد النقاط: 184 — VERIFIED بالعد من سمات `[HttpX]` (التفصيل: Alerts 6، Appeals 21، AppSuggestions 4، AuditLogs 1، Auth 4، Branches 5، ClientErrors 1، Correspondences 10، Delegations 9، Documents 32، EntityRegistry 35، Meta 1، PersonalReminders 6، Portal 19 + Delegates 3، ReviewLetters 10، Statistics 9، UserManagement 8).
- لا توجد مسارات تقليدية — VERIFIED (لا `MapControllerRoute` في `Program.cs`).
- النقاط الوحيدة بلا مصادقة: `POST api/auth/login` و`POST api/auth/logout` و`GET api/meta/current-year` — VERIFIED (`[AllowAnonymous]` في `AuthController.cs:42,99` و`MetaController.cs:26`).

## Coverage

- فُحصت كل ملفات `Controllers/*.cs` سطرًا بسطر لاستخراج السمات — كامل.
- لم تُفحص شروط `RolePermissions` الداخلية لكل أكشن هنا (تُدرَس في تدقيق الأمن PROMPT 2) — جزئي.
- لم يُتحقق من تطابق `DTO` مع `Application/DTOs/*.cs` حقلًا بحقل هنا — يُستكمل في PROMPT 3.
