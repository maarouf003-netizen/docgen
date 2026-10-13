/* â”€â”€ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ ظ„ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© (ط¨ظˆط§ط¨ط© ط§ظ„ط¬ظ‡ط§طھ â€” ط§ظ„ظ…ط±ط­ظ„ط© 1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ظ†ظˆط¹ ط§ظ„ط¬ظ‡ط© (ظƒطھط§ظ„ظˆط¬ ط§ظ„ط£ط­ط¯ ط¹ط´ط± ظ†ظˆط¹ظ‹ط§ ط§ظ„ظ…ط¹طھظ…ط¯). */
export type PublicEntityType =
  | 'ministry'
  | 'administration'
  | 'authority'
  | 'foundation'
  | 'company'
  | 'directorate'
  | 'sub-administration'
  | 'general-secretariat'
  | 'governorate-body'
  | 'city-council'
  | 'town-council';

/** ط­ط§ظ„ط© ظ‚ظٹط¯ ط§ظ„ط¬ظ‡ط©: ظ†ظ‡ط§ط¦ظٹ (ظٹط¸ظ‡ط± ظ„ظ„ظ…ظ†ط¯ظˆط¨ظٹظ†) ط£ظˆ ط¨ط§ظ†طھط¸ط§ط± ط§ظ„ظ…ط±ط§ط¬ط¹ط©. */
export type PublicEntityStatus = 'final' | 'pending';

/** طµظٹط؛ط© ظ…ظ†ط§ط¯ط§ط© ظ…ظ…ط«ظ„ ط§ظ„ط¬ظ‡ط© ط§ظ„ظ‚ط§ظ†ظˆظ†ظٹط©: ط¥ط¶ط§ظپط© ظ„ظˆط¸ظٹظپطھظ‡ / ط¥ط¶ط§ظپط© ظ„ظ…ظ†طµط¨ظ‡. */
export type CitationFormula = 'add-to-job' | 'add-to-position';

/** ظ‚ظٹط¯ ط¬ظ‡ط© ظپظٹ ط§ظ„ط³ط¬ظ„ ط¨ظ…ط³طھظˆظ‰ ظ…ط­ط§ظپط¸ط© + ظپط±ط¹. */
export interface PublicEntityEntryDto {
  id: number;
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  branchName: string;
  citationFormula: CitationFormula;
  status: PublicEntityStatus;
  isActive: boolean;
  createdAt: string;
  aliases: string[];
  createdByName?: string | null;
  /** ط£ط¯ط®ظ„ظ‡ط§ ظ…ط­ط§ظ…ظچ ظˆظ‡ظٹ ط¨ط§ظ†طھط¸ط§ط± ظ…ط±ط§ط¬ط¹ط© ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ… (ظ†ظ…ظˆط°ط¬ ط§ظ„ط­ظˆظƒظ…ط© ط§ظ„ط¬ط¯ظٹط¯). */
  needsReview?: boolean;
  /** طھط³ظ…ظٹط© ط§ظ„طھط؛ط·ظٹط© ط§ظ„ط¬ط؛ط±ط§ظپظٹط© (طھط¸ظ‡ط± ط¨ط¯ظ„ ط§ظ„ظ…ط­ط§ظپط¸ط© ظپظٹ ط§ظ„ط¨ط·ط§ظ‚ط§طھ ظˆط§ظ„ط¨ط­ط«). */
  coverageLabel?: string | null;
  /** ظ‚ظٹط¯ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط£ظ…آ» (ط¨ظ„ط§ ظپط±ط¹): ظٹط؛ط·ظٹ ظƒظ„ ط§ظ„ظ…ط­ط§ظپط¸ط§طھ ظˆظٹط¸ظ‡ط± ظ…ط±ط© ظˆط§ط­ط¯ط© â€” ظˆظپط±ظˆط¹ظ‡ طھط­طھظ‡. */
  isParentEntity?: boolean;
}

/** ظ†طھظٹط¬ط© ظ‚ط§ط¦ظ…ط©/ط¨ط­ط« ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…طµط¯ظ‘ط±ط©. */
export interface PublicEntityListResponse {
  items: PublicEntityEntryDto[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}

export interface CreatePublicEntityRequest {
  canonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  branchName: string;
  citationFormula?: CitationFormula | null;
  aliases?: string[] | null;
  coverageLabel?: string | null;
  /** ط¬ط¹ظ„ ط§ظ„ظ‚ظٹط¯ ظ‚ظٹط¯ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط£ظ…آ» (ط¨ظ„ط§ ظپط±ط¹) â€” ظٹظڈط®ط²ظژظ‘ظ† ظ…ط±ط© ظˆط§ط­ط¯ط© ظˆظٹط؛ط·ظٹ ظƒظ„ ط§ظ„ظ…ط­ط§ظپط¸ط§طھ. */
  isParentEntity?: boolean;
}

/** ط£ظٹ ط­ظ‚ظ„ ظٹظڈطھط±ظƒ undefined ظٹط¨ظ‚ظ‰ ظƒظ…ط§ ظ‡ظˆط› canonicalName ظٹط¹ظ†ظٹ ط¥ط¹ط§ط¯ط© طھط³ظ…ظٹط© ط¬ظ…ط§ط¹ظٹط©. ط­ظ‚ظˆظ„ ط§ظ„ظ…ط±ط³ظˆظ… ظ„ظ„طھط¹ط¯ظٹظ„ط§طھ ط§ظ„ط¹ط§ظ…ط© ط¨ظ…ط±ط³ظˆظ…. */
export interface UpdatePublicEntityRequest {
  canonicalName?: string | null;
  entityType?: PublicEntityType | null;
  governorate?: string | null;
  branchName?: string | null;
  citationFormula?: CitationFormula | null;
  status?: PublicEntityStatus | null;
  isActive?: boolean | null;
  coverageLabel?: string | null;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  isParentEntity?: boolean | null;
}

export interface AddPublicEntityAliasRequest {
  aliasText: string;
}

export interface ProposeEditRequest {
  canonicalName?: string | null;
  entityType?: PublicEntityType | null;
  governorate?: string | null;
  branchName?: string | null;
  citationFormula?: CitationFormula | null;
  coverageLabel?: string | null;
  isParentEntity?: boolean | null;
}

/** ظƒطھط§ط¨ط© ظ…طھظ…ط§ظٹط²ط© ظˆط§ط­ط¯ط© ظ„ظ†طµ ط¬ظ‡ط© ظپظٹ ط§ظ„ط§ط³طھظٹط±ط§ط¯ ظ…ط¹ ط¹ط¯ظ‘ط§ط¯ ظ…ظ„ظپط§طھظ‡ط§. */
export interface ImportVariantDto {
  text: string;
  /** ظ…طµط¯ط± ط§ظ„ظƒطھط§ط¨ط©: applicant = ط·ط§ظ„ط¨ طھظ†ظپظٹط°طŒ executed = ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§. */
  side: 'applicant' | 'executed';
  governorate?: string | null;
  documentCount: number;
}

/** ظ…ط±ط´ظ‘ط­ ط§ط³طھظٹط±ط§ط¯ ظٹط¬ظ…ط¹ ظƒطھط§ط¨ط§طھ ظ…طھط·ط§ط¨ظ‚ط© ط¨ط¹ط¯ ط§ظ„طھط·ط¨ظٹط¹ طھط­طھ ط§ط³ظ… ظ…ظ‚طھط±ط­. */
export interface ImportPreviewItemDto {
  normalizedName: string;
  suggestedCanonicalName: string;
  totalDocuments: number;
  governorates: string[];
  variants: ImportVariantDto[];
}

export interface ImportPreviewResponse {
  generatedAtUtc: string;
  items: ImportPreviewItemDto[];
}

export interface ImportCommitItemRequest {
  normalizedName: string;
  canonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  branchName: string;
  citationFormula?: CitationFormula | null;
  addVariantsAsAliases?: boolean;
}

export interface ImportCommitRequest {
  items: ImportCommitItemRequest[];
}

export interface ImportCommitResultDto {
  groupsCreated: number;
  entriesCreated: number;
  aliasesAdded: number;
}

/* â”€â”€ ط§ظ„ظ†ظ‚ظ„ (MoveEntry) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط·ظ„ط¨ ظ†ظ‚ظ„ ظ‚ظٹط¯ ط¬ظ‡ط© ظ…ظ† ظ‡ظˆظٹط© ط¥ظ„ظ‰ ط£ط®ط±ظ‰ ط£ظˆ ط·ظٹظ‘ظ‡ ظپظٹ ظ‚ظٹط¯ ظ‚ط§ط¦ظ…. */
export interface MoveEntryRequest {
  targetGroupId?: number | null;
  targetEntryId?: number | null;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  note?: string | null;
}

/** ط·ظ„ط¨ ظ†ظ‚ظ„ ط¬ظ…ظٹط¹ ظ‚ظٹظˆط¯ ظ…ط¬ظ…ظˆط¹ط© ط¥ظ„ظ‰ ظ…ط¬ظ…ظˆط¹ط© ط£ط®ط±ظ‰ (طھط¨ط¹ظٹط© ظƒط§ظ…ظ„ط©). */
export interface MoveAllEntriesRequest {
  sourceGroupId: number;
  targetGroupId: number;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  note?: string | null;
}

/** ظ†طھظٹط¬ط© ظ†ظ‚ظ„ ظ‚ظٹط¯ ظˆط§ط­ط¯. */
export interface MoveEntryResponse {
  entryId: number;
  fromGroupId: number;
  toGroupId: number;
  affectedDocuments: number;
  changeEventId: number;
}

/** ظ†طھظٹط¬ط© ظ†ظ‚ظ„ ط¬ظ…ظٹط¹ ظ‚ظٹظˆط¯ ظ…ط¬ظ…ظˆط¹ط©. */
export interface MoveAllEntriesResponse {
  sourceGroupId: number;
  targetGroupId: number;
  entriesMoved: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* â”€â”€ ط§ظ„ط¯ظ…ط¬ Nâ†گ1 (ط¯5 آ§4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط·ظ„ط¨ ظ…ط¹ط§ظٹظ†ط© ط§ظ„ط¯ظ…ط¬ ظ‚ط¨ظ„ ط§ظ„ط§ط¹طھظ…ط§ط¯. */
export interface MergePreviewRequest {
  survivorGroupId: number;
  absorbedGroupIds: number[];
}

/** ظ‚ظٹط¯ ظ…ظڈظ‡ظ…ظژظ„ ظپظٹ ط§ظ„ظ…ط¹ط§ظٹظ†ط© ظ…ط¹ ظ…ط³ط§ط± ط§ظ…طھطµط§طµظ‡. */
export interface AbsorbedEntryPreviewDto {
  entryId: number;
  governorate: string;
  branchName: string;
  documentCount: number;
  mappedToEntryId: number;
  conflictsWithSurvivor: boolean;
}

/** ظ‡ظˆظٹط© ط£ظ… ظ…ظڈظ‡ظ…ظژظ„ط© ظپظٹ ط§ظ„ظ…ط¹ط§ظٹظ†ط©. */
export interface AbsorbedGroupPreviewDto {
  groupId: number;
  name: string;
  entries: AbsorbedEntryPreviewDto[];
  totalDocuments: number;
  aliases: string[];
}

/** ظ†طھظٹط¬ط© ظ…ط¹ط§ظٹظ†ط© ط§ظ„ط¯ظ…ط¬. */
export interface MergePreviewResponse {
  survivorName: string;
  absorbedGroups: AbsorbedGroupPreviewDto[];
  totalAffectedDocuments: number;
  warnings: string[];
}

/** ط·ظ„ط¨ ط§ط¹طھظ…ط§ط¯ ط§ظ„ط¯ظ…ط¬. */
export interface MergeCommitRequest {
  survivorGroupId: number;
  absorbedGroupIds: number[];
  unifyTexts?: boolean;
  newCanonicalName?: string | null;
  decreeKind?: string;
  decreeNumber?: string;
  decreeDate?: string;
}

/** ظ†طھظٹط¬ط© ط§ظ„ط¯ظ…ط¬. */
export interface MergeCommitResponse {
  absorbedGroupsCount: number;
  entriesMigrated: number;
  aliasesAdded: number;
  totalAffectedDocuments: number;
  changeEventId: number;
}

/* â”€â”€ ط¥ط¹ط§ط¯ط© طھط³ظ…ظٹط© ظ‡ظˆظٹط© ط£ظ… (ط§ظ„ظ…ط¯ظٹط±/ط§ظ„ظ…ط´ط±ظپ) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط·ظ„ط¨ ظ…ط¹ط§ظٹظ†ط© ط¥ط¹ط§ط¯ط© طھط³ظ…ظٹط© ظ‡ظˆظٹط© ط£ظ… ظ‚ط¨ظ„ ط§ظ„ط§ط¹طھظ…ط§ط¯. */
export interface RenameGroupPreviewRequest {
  groupId: number;
  newCanonicalName: string;
}

/** ظ†طھظٹط¬ط© ظ…ط¹ط§ظٹظ†ط© ط¥ط¹ط§ط¯ط© ط§ظ„طھط³ظ…ظٹط©. */
export interface RenameGroupPreviewResponse {
  oldCanonicalName: string;
  newCanonicalName: string;
  affectedDocuments: number;
  branches: string[];
}

/** ط·ظ„ط¨ ط¥ط¹ط§ط¯ط© طھط³ظ…ظٹط© ظ‡ظˆظٹط© ط£ظ… ظˆط§ط­ط¯ط© ط¨ظ…ظˆط¬ط¨ ظ…ط±ط¬ط¹ (ظ‚ط±ط§ط±/ظ‚ط§ظ†ظˆظ†/ظ…ط±ط³ظˆظ…). */
export interface RenameGroupRequest {
  groupId: number;
  newCanonicalName: string;
  decreeKind: string;
  decreeNumber: string;
  decreeDate: string;
}

/** ظ†طھظٹط¬ط© ط¥ط¹ط§ط¯ط© طھط³ظ…ظٹط© ط§ظ„ظ‡ظˆظٹط© ط§ظ„ط£ظ…. */
export interface RenameGroupResponse {
  groupId: number;
  oldCanonicalName: string;
  newCanonicalName: string;
  affectedDocuments: number;
  changeEventId: number;
}

/* â”€â”€ ط§ظ„ط­ظ„ظˆظ„: ط¥ظ„ط؛ط§ط، ظ‡ظˆظٹط§طھ ط£ظ… ظˆط§ط³طھط¨ط¯ط§ظ„ظ‡ط§ ط¨ظ‡ظˆظٹط© ط¬ط¯ظٹط¯ط© â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط·ظ„ط¨ ظ…ط¹ط§ظٹظ†ط© ط§ظ„ط¥ظ„ط؛ط§ط، ظˆط§ظ„ط§ط³طھط¨ط¯ط§ظ„ ظ‚ط¨ظ„ ط§ظ„ط§ط¹طھظ…ط§ط¯. */
export interface AbolishReplacePreviewRequest {
  abolishedGroupIds: number[];
}

/** ظ†طھظٹط¬ط© ظ…ط¹ط§ظٹظ†ط© ط§ظ„ط¥ظ„ط؛ط§ط، ظˆط§ظ„ط§ط³طھط¨ط¯ط§ظ„. */
export interface AbolishReplacePreviewResponse {
  abolishedNames: string[];
  abolishedGroups: number;
  activeEntries: number;
  affectedDocuments: number;
  delegatesToReassign: number;
  branches: string[];
}

/** ط·ظ„ط¨ ط¥ظ„ط؛ط§ط، ط¹ط¯ط© ظ‡ظˆظٹط§طھ ط£ظ… ظˆط§ط³طھط¨ط¯ط§ظ„ظ‡ط§ ط¨ظ‡ظˆظٹط© ط£ظ… ط¬ط¯ظٹط¯ط© طھط­ظ„ظ‘ ظ…ط­ظ„ظ‡ط§. */
export interface AbolishAndReplaceRequest {
  abolishedGroupIds: number[];
  newCanonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  citationFormula?: string | null;
  aliases?: string[];
  coverageLabel?: string | null;
  decreeKind: string;
  decreeNumber: string;
  decreeDate: string;
}

/** ظ†طھظٹط¬ط© ط§ظ„ط¥ظ„ط؛ط§ط، ظˆط§ظ„ط§ط³طھط¨ط¯ط§ظ„. */
export interface AbolishAndReplaceResponse {
  newGroupId: number;
  newCanonicalName: string;
  abolishedGroups: number;
  entriesMoved: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* â”€â”€ ظ‚ط§ط¦ظ…ط© ط§ظ„ظ…ط¬ظ…ظˆط¹ط§طھ (ط§ظ„ظ‡ظˆظٹط§طھ ط§ظ„ط£ظ…) ظˆطھظˆط­ظٹط¯ ط§ظ„طھط³ظ…ظٹط© Nâ†گ1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export interface PublicEntityGroupDto {
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  isActive: boolean;
  entryCount: number;
  governorates: string[];
  linkedDocumentCount?: number;
}

export interface PublicEntityGroupListResponse {
  items: PublicEntityGroupDto[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}

export interface UnifyPreviewRequest {
  targetGroupId: number;
  absorbedGroupIds: number[];
}

export interface AbsorbedGroupUnifyPreviewDto {
  groupId: number;
  name: string;
  entryCount: number;
  governorates: string[];
}

export interface EntryFoldPreviewDto {
  absorbedGroupId: number;
  absorbedGroupName: string;
  governorate: string;
  branchName: string;
  linkedDocumentCount: number;
}

export interface UnifyPreviewResponse {
  targetName: string;
  absorbedGroups: AbsorbedGroupUnifyPreviewDto[];
  totalEntriesToMove: number;
  totalEntriesFolded: number;
  foldsToApply: EntryFoldPreviewDto[];
  warnings: string[];
}

export interface UnifyRequest {
  targetGroupId: number;
  absorbedGroupIds: number[];
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
}

export interface UnifyResponse {
  targetGroupId: number;
  canonicalName: string;
  groupsUnified: number;
  entriesMoved: number;
  entriesFolded: number;
  changeEventId: number;
}

/* â”€â”€ ظ…ط´ط§ط¨ظ‡ط§طھ ط¬ظ‡ط© ظ…ط­ط¯ط¯ط© (طھظˆط­ظٹط¯ ط§ظ„طھط³ظ…ظٹط©) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export interface SimilarToItemDto {
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  entryCount: number;
  linkedDocumentCount: number;
  similarity: number;
}

export interface SimilarToResponse {
  targetGroupId: number;
  targetCanonicalName: string;
  items: SimilarToItemDto[];
  threshold: number;
}

/* â”€â”€ ط³ط¬ظ„ طھط؛ظٹظٹط±ط§طھ ط§ظ„ط¬ظ‡ط§طھ (ط¯5 آ§7) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export interface EntityChangeEventDto {
  id: number;
  entryId?: number | null;
  groupId?: number | null;
  actionKind: string;
  actionKindLabel: string;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  summaryAr: string;
  /** ظˆط³ظ… ط¬ظˆط¯ط© ط§ظ„ظ…ظ„ط®ظ‘طµ: ط­ظ…ظ„ ط؛ظٹط± ظپط§ط±ط؛ ظ„ط§ ظٹظڈظ†طھط¬ طھظپط§طµظٹظ„ ظپط³ظ‚ط· ط¥ظ„ظ‰ ط§ظ„طھط³ظ…ظٹط© â€” ظٹظڈط¹ط±ط¶ ظ…ظˆط³ظˆظ…ظ‹ط§. */
  summaryDegraded: boolean;
  actorUserId: number;
  actorName?: string | null;
  createdAtUtc: string;
  governorate?: string | null;
  canonicalName?: string | null;
}

export interface EntityChangeEventQuery {
  governorate?: string | null;
  actionKind?: string | null;
  actorUserId?: number | null;
  from?: string | null;
  to?: string | null;
  page?: number;
  perPage?: number;
}

/* â”€â”€ ط¨ظˆط§ط¨ط© ظ…ظ†ط¯ظˆط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© (ط§ظ„ظ…ط±ط­ظ„ط© 3) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/* â”€â”€ ط¥ط¯ط§ط±ط© ظپط±ظˆط¹ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ… (ط¶ظ…ظ† ظ…ط­ط§ظپط¸طھظ‡ â€” ط¨ظ„ط§ ظ…ط±ط³ظˆظ…) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export interface RenameBranchRequest {
  newBranchName: string;
  coverageLabel?: string | null;
}

export interface MergeBranchesRequest {
  sourceEntryId: number;
  targetEntryId: number;
}

export interface AbolishBranchRequest {
  targetEntryId?: number | null;
}

export interface UnifyBranchesRequest {
  targetEntryId: number;
  absorbedEntryIds: number[];
  correctedName?: string | null;
}

export type BranchActionKind = 'rename' | 'merge' | 'abolish' | 'unify';

export interface PreviewBranchActionRequest {
  action: BranchActionKind;
  /** ط§ظ„ظپط±ط¹ ط§ظ„ظ…ط¹ط§ظ„ظژط¬ (ط§ظ„ظ…ط¹ط§ط¯ طھط³ظ…ظٹطھظ‡ / ط§ظ„ظ…طµط¯ط± ظپظٹ ط§ظ„ط¯ظ…ط¬ / ط§ظ„ظ…ظڈظ„ط؛ظ‰ / ط§ظ„ظ†ط§ط¬ظٹ ظپظٹ ط§ظ„طھظˆط­ظٹط¯). */
  entryId: number;
  targetId?: number | null;
  absorbedIds?: number[] | null;
  newBranchName?: string | null;
  correctedName?: string | null;
}

export interface BranchPreviewEntryDto {
  entryId: number;
  branchName: string;
  governorate: string;
  documentCount: number;
}

export interface BranchActionPreviewResponse {
  action: BranchActionKind;
  summary: string;
  targetBranchName: string;
  entries: BranchPreviewEntryDto[];
  totalAffectedDocuments: number;
  warnings: string[];
  errors: string[];
}

export interface RenameBranchResponse {
  entryId: number;
  oldBranchName: string;
  newBranchName: string;
  affectedDocuments: number;
  changeEventId: number;
}

export interface MergeBranchesResponse {
  sourceEntryId: number;
  targetEntryId: number;
  affectedDocuments: number;
  changeEventId: number;
}

export interface AbolishBranchResponse {
  entryId: number;
  targetEntryId?: number | null;
  affectedDocuments: number;
  changeEventId: number;
}

export interface UnifyBranchesResponse {
  targetEntryId: number;
  entriesUnified: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* â”€â”€ ط§ظ‚طھط±ط§ط­ طھط¹ط¯ظٹظ„ ط§ظ„ط¬ظ‡ط© ط§ظ„ط£ظ… (ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ… â†’ ظ…ط¯ظٹط±/ظ…ط´ط±ظپ) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export type ParentEditSuggestionStatus = 'pending' | 'approved' | 'rejected' | 'withdrawn';

export interface SuggestParentEditRequest {
  proposedCanonicalName?: string | null;
  proposedEntityType?: PublicEntityType | null;
  proposedCitationFormula?: string | null;
  reason?: string | null;
}

export interface ReviewParentEditSuggestionRequest {
  status: 'approved' | 'rejected';
  reviewReason?: string | null;
}

export interface ParentEditSuggestionDto {
  id: number;
  groupId: number;
  entryId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  proposedCanonicalName?: string | null;
  proposedEntityType?: PublicEntityType | null;
  proposedCitationFormula?: string | null;
  reason: string;
  status: ParentEditSuggestionStatus;
  createdById: number;
  createdByName: string;
  createdBranchId: number;
  reviewedById?: number | null;
  reviewReason?: string | null;
  createdAtUtc: string;
  reviewedAtUtc?: string | null;
}

export interface ParentEditSuggestionListQuery {
  status?: string | null;
  groupId?: number | null;
  page?: number;
  perPage?: number;
}

export interface ParentEditSuggestionListResponse {
items: ParentEditSuggestionDto[];
total: number;
}
