export interface GuarantorDto {
  id?: number;
  guarantorNumber: number;
  name?: string;
  father?: string;
  family?: string;
  mother?: string;
  birth?: string;
  register?: string;
  nationalId?: string;
  address?: string;
  addressType?: string;
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeAddressType?: string;
  representativeAddress?: string;
  heirs?: HeirDto[];
  /** ط·ط¨ظٹط¹ط© ط§ظ„ظƒظپظٹظ„: ط´ط®طµ ط·ط¨ظٹط¹ظٹ (natural) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal). */
  nature?: PartyNature;
  /** ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  registrationNumber?: string;
  /** ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  representedBy?: string;
}

/** ظˆط±ظٹط« ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظ…طھظˆظپظ‰ (ط§ظ„ظ…ظ‚طھط±ط¶ ط£ظˆ ط£ط­ط¯ ط§ظ„ظƒظپظ„ط§ط،). */
export interface HeirDto {
  id?: number;
  name?: string;
  father?: string;
  family?: string;
  /** طµظپط© ط§ظ„ظˆط±ظٹط«: ط£طµط§ظ„ط© / ط¥ط¶ط§ظپط© ظ„طھط±ظƒط© / ط£طµط§ظ„ط© ظˆط¥ط¶ط§ظپط©. */
  capacity?: string;
  addressType?: string;
  address?: string;
}

/**
 * ظ…ط§ظ„ ظ…ط±ظ‡ظˆظ† ط¶ظ…ظ† ظ‚ط§ط¦ظ…ط© ط§ظ„ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ط±ظ‡ظˆظ†ط© (ظ…ظ†ظ‚ظˆظ„ ط£ظˆ ط؛ظٹط± ظ…ظ†ظ‚ظˆظ„). ط§ظ„ظ…ظ„ط§ظƒ ظ‚ط§ط¦ظ…ط© ط£ط³ظ…ط§ط، (ظˆط§ط­ط¯ ط£ظˆ ط£ظƒط«ط±)
 * ط¨طھط±طھظٹط¨ ط§ظ„ط§ط®طھظٹط§ط±طŒ ظˆطھظڈط³طھط®ط¯ظ… ظپظٹ ط§ظ„ظ†طµظˆطµ ظˆط§ظ„طھظˆظ„ظٹط¯ ظ…ط¬ظ…ط¹ط©ظ‹طŒ ظˆظٹظڈظˆط¬ظژظ‘ظ‡ طھظˆظ„ظٹط¯ 005/006 ظ„ظƒظ„ ظˆط±ظٹط«ظچ ظ…ظ† ط§ظ„ظˆط±ط«ط©.
 * ط§ظ„ط­ظ‚ظˆظ„ ط؛ظٹط± ط§ظ„ظ…ط¹ظ†ظٹط© ط¨ظ†ظˆط¹ ط§ظ„ط£طµظ„ طھط¨ظ‚ظ‰ ظپط§ط±ط؛ط©.
 */
export interface AssetDto {
  id?: number;
  /** ظ†ظˆط¹ ط§ظ„ط£طµظ„: ط¹ظ‚ط§ط± / ظ…ط±ظƒط¨ط© / ظ…طھط¬ط± / ظƒظپط§ظ„ط© ط±ظˆط§طھط¨ / ظ…طھط¬ط± ط؛ظٹط± ظ…ط³ط¬ظ„. */
  assetKind?: string;
  owners?: string[];
  /** ظ…ظ‚ط¯ط§ط± ط§ظ„ط­طµط© (طھظ…ط§ظ… ط§ظ„ط¹ظ‚ط§ط±/ط§ظ„ظ…ط±ظƒط¨ط©/ط§ظ„ظ…طھط¬ط± ط£ظˆ ط­طµط© ط³ظ‡ظ…ظٹط©) â€” ط¨ظ„ط§ ظ‚ظٹظ…ط© ظ„ظƒظپط§ظ„ط© ط§ظ„ط±ظˆط§طھط¨ ظˆط§ظ„ظ…طھط¬ط± ط؛ظٹط± ط§ظ„ظ…ط³ط¬ظ„. */
  shareType?: string;
  // ط§ظ„ط¹ظ‚ط§ط±
  property?: string;
  propertyNumber?: string;
  propertyDistrict?: string;
  landRegistry?: string;
  // ط§ظ„ظ…ط±ظƒط¨ط©
  vehicleType?: string;
  vehicleClass?: string;
  plateNumber?: string;
  vehicleGovernorate?: string;
  // ط§ظ„ظ…طھط¬ط± ط§ظ„ظ…ط³ط¬ظ„
  registerNumber?: string;
  registrationDate?: string;
  shopGovernorate?: string;
  shopDescription?: string;
shopLocation?: string;
  // ظƒظپط§ظ„ط© ط§ظ„ط±ظˆط§طھط¨
  publicEntity?: string;
  // ط§ظ„ظ…طھط¬ط± ط؛ظٹط± ط§ظ„ظ…ط³ط¬ظ„
  licenseNumber?: string;
  /** طھط§ط±ظٹط® ط§ظ„ظ‚ط§ط، ط§ظ„ط­ط¬ط² ط¹ظ„ظ‰ ط§ظ„ط£طµظ„ â€” ظ†طµ ط­ط± ظٹظڈظپط³ظژظ‘ط± ط²ظ…ظ†ظٹظ‹ط§ ط¨ط§ظ„ط®ظ„ظپظٹط©. */
  seizureDate?: string;
  licenseDate?: string;
  licenseIssuer?: string;
  // ط§ظ„ظ…ظ„ط§ط­ط¸ط§طھ (ظƒظپط§ظ„ط© ط§ظ„ط±ظˆط§طھط¨ ظˆط§ظ„ظ…طھط¬ط± ط؛ظٹط± ط§ظ„ظ…ط³ط¬ظ„)
  notes?: string;
}

export interface ExecutionActionDto {
  id: number;
  type: string;
  text: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  createdByName?: string;
  createdAt: string;
}

/** طµظپط© ط§ظ„ظ…ظ„ظپ ط§ظ„ط«ط§ط¨طھط© (طھظڈط«ط¨ظژظ‘طھ ط¹ظ†ط¯ ط§ظ„ط¥ظ†ط´ط§ط، ظˆظ„ط§ طھطھط؛ظٹط± ط£ط«ظ†ط§ط، ط§ظ„طھط¹ط¯ظٹظ„). */
export type GeneralEntitySide = 'applicant' | 'executed' | 'deposit';

/** ط·ط¨ظٹط¹ط© ط·ط±ظپ ط§ظ„ظ…ظ„ظپ: ط´ط®طµ ط·ط¨ظٹط¹ظٹ ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ. */
export type PartyNature = 'natural' | 'legal';

/** ط·ط¨ظٹط¹ط© ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ»: ط¬ظ‡ط© ط¹ط§ظ…ط© ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ. */
export type EntityNature = 'public' | 'legal';

/** ظˆط±ظٹط« ظ„ظ…ظˆط±ط«ظچ ظ…طھظˆظپظ‰ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» (ط§ط³ظ… ط«ظ„ط§ط«ظٹ). */
export interface ExecutedHeirDto {
  id?: number;
  heirName?: string;
  heirFather?: string;
  heirFamily?: string;
  addressType?: string;
  heirAddress?: string;
}

/** ط·ط§ظ„ط¨ ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» ظ…ط¹ ظˆط±ط«ط© ظ…ظˆط±ط«ظ‡ ط§ظ„ظ…طھظˆظپظ‰ (ط¥ظ† ط§ط®طھظٹط± آ«ط¥ط¶ط§ظپط© ظ„طھط±ظƒط©آ» ط£ظˆ آ«ط£طµط§ظ„ط© ظˆط¥ط¶ط§ظپط©آ»). */
export interface ExecutionApplicantDto {
  id?: number;
  name?: string;
  father?: string;
  family?: string;
  legalRepresentative?: string;
  representationType?: string;
  deceasedName?: string;
  deceasedFather?: string;
  deceasedFamily?: string;
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeLegalRepresentative?: string;
  heirs?: ExecutedHeirDto[];
  /** ط·ط¨ظٹط¹ط© ط·ط§ظ„ط¨ ط§ظ„طھظ†ظپظٹط°/ط§ظ„ط¹ط±ط¶: ط´ط®طµ ط·ط¨ظٹط¹ظٹ (natural) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal). */
  nature?: PartyNature;
  /** ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  registrationNumber?: string;
  /** ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  representedBy?: string;
  /** ظ†ظˆط¹ ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ: ظ…ظˆط·ظ† ظ…ط®طھط§ط± / ط¹ظ†ظˆط§ظ† / ظˆظƒظٹظ„ ظ‚ط§ظ†ظˆظ†ظٹ. */
  addressType?: string;
  /** ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط£ظˆ ظˆظƒظٹظ„ظ‡ ط§ظ„ظ‚ط§ظ†ظˆظ†ظٹ. */
  address?: string;
  /** ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط·ط§ظ„ط¨ ط§ظ„طھظ†ظپظٹط° ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ (ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط©) ظپظٹ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ (ط§ط®طھظٹط§ط±ظٹ). */
  registryId?: number | null;
}

/** ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ»: ط¬ظ‡ط© ط¹ط§ظ…ط© ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ. */
export interface ExecutedPublicEntityDto {
  id?: number;
  entityName?: string;
  entityBranch?: string;
  /** ط§ظ„ظ…ط­ط§ظپط¸ط© ط§ظ„طھظٹ طھطھط¨ط¹ ظ„ظ‡ط§ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط£ظˆ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ (ظ…ط«ظ„: ط¯ظ…ط´ظ‚/ط§ظ„ظ„ط§ط°ظ‚ظٹط©). */
  governorate?: string;
  /** ط·ط¨ظٹط¹ط© ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡: ط¬ظ‡ط© ط¹ط§ظ…ط© (public) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal). */
  nature?: EntityNature;
  /** ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© (legal). */
  registrationNumber?: string;
  /** ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© (legal). */
  representedBy?: string;
  /** ظ†ظˆط¹ ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ: ظ…ظˆط·ظ† ظ…ط®طھط§ط± / ط¹ظ†ظˆط§ظ† / ظˆظƒظٹظ„ ظ‚ط§ظ†ظˆظ†ظٹ. */
  addressType?: string;
  /** ط¹ظ†ظˆط§ظ† ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط£ظˆ ظˆظƒظٹظ„ظ‡ ط§ظ„ظ‚ط§ظ†ظˆظ†ظٹ. */
  address?: string;
  /** ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط§ظ„ط¬ظ‡ط© ظپظٹ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ (ظ…ظ† ظ†ط§ظپط°ط© ط§ظ„ط§ط®طھظٹط§ط±) â€” ظٹظڈظپظƒظ‘ ط¨ط²ط± آ«ظپظƒ ط§ظ„ط±ط¨ط·آ». */
  registryId?: number | null;
}

/** ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ط§ط³ظ… ط§ظ„ط¬ظ‡ط© + ظپط±ط¹ظ‡ط§ + ظ…ط­ط§ظپط¸طھظ‡ط§). */
export interface ApplicantPublicEntityDto {
  id?: number;
  name?: string;
  branch?: string;
  /** ط§ظ„ظ…ط­ط§ظپط¸ط© ط§ظ„طھظٹ طھطھط¨ط¹ ظ„ظ‡ط§ ط§ظ„ط¬ظ‡ط© (ظ…ط«ظ„: ط¯ظ…ط´ظ‚/ط§ظ„ظ„ط§ط°ظ‚ظٹط©) â€” طھظڈظ…ظ„ط£ طھظ„ظ‚ط§ط¦ظٹظ‹ط§ ظ…ظ† ظپط±ط¹ ط§ظ„ظ…ط­ط§ظ…ظٹ ظˆظ‚ط§ط¨ظ„ط© ظ„ظ„طھط¹ط¯ظٹظ„. */
  governorate?: string;
  /** ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط§ظ„ط¬ظ‡ط© ظپظٹ ط§ظ„ط³ط¬ظ„ ط§ظ„ظ…ط±ط¬ط¹ظٹ (ظ…ظ† ظ†ط§ظپط°ط© ط§ظ„ط§ط®طھظٹط§ط±) â€” ظٹظڈظپظƒظ‘ ط­طµط±ظ‹ط§ ط¨ط²ط± آ«ظپظƒ ط§ظ„ط±ط¨ط·آ». */
  registryId?: number | null;
}

/** ط³ط¬ظ„ طھط¹ط§ظ‚ط¨ ظ…ط­ط§ظ…ظچ ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ: ظ…ظ†ط´ط¦ ط§ظ„ظ…ظ„ظپ (create) ط£ظˆ ط¥ط­ط§ظ„ط© (transfer). */
export interface DocumentAssignmentDto {
  id: number;
  kind: 'create' | 'transfer';
  lawyerName?: string;
  assignedByName?: string;
  assignedAt?: string;
}

/** ط§ظ„ط´ط®طµ ط§ظ„ط·ط¨ظٹط¹ظٹ ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ» ظ…ط¹ ظˆط±ط«ط© ظ…ظˆط±ط«ظ‡ ط§ظ„ظ…طھظˆظپظ‰ (ط¥ظ† ط§ط®طھظٹط± آ«ط¥ط¶ط§ظپط© ظ„طھط±ظƒط©آ» ط£ظˆ آ«ط£طµط§ظ„ط© ظˆط¥ط¶ط§ظپط©آ»). */
export interface ExecutedNaturalPersonDto {
  id?: number;
  name?: string;
  father?: string;
  family?: string;
  addressType?: string;
  addressOrRepresentative?: string;
  representationType?: string;
  deceasedName?: string;
  deceasedFather?: string;
  deceasedFamily?: string;
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeAddressType?: string;
  representativeAddress?: string;
  heirs?: ExecutedHeirDto[];
}

export interface InitialActionRequest {
  type: 'action' | 'note';
  text: string;
  actionDate?: string | null;
  reminderDuration?: string | null;
  reminderColor?: string | null;
}

export interface DocumentResponse {
  id: number;
  createdAt: string;
  updatedAt: string;
  /** ط¹ط¯ظ‘ط§ط¯ ط§ظ„طھط²ط§ظ…ظ† ط§ظ„ظ…طھظپط§ط¦ظ„ (RF-010) â€” ظٹظڈط¹ط§ط¯ ط¥ط±ط³ط§ظ„ظ‡ ظپظٹ `version` ط¹ظ†ط¯ ط§ظ„ط­ظپط¸. */
  version: number;
  createdById?: number;
  branchId?: number;
documentType?: string;
  isDraft: boolean;
  /** ط§ظ„ظ…ظ„ظپ ظ…ظ†ط§ط¨ظŒ ظ…ظ† ظ…ظ„ظپ ط¢ط®ط± (ط¥ظ†ط§ط¨ط© ط³ط·ط±ظ‡ط§ ظ…ط­ط§ظ…ظٹ ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ظ†ظٹط¨): ط§ظ„ط­ظ‚ظˆظ„ ط§ظ„ط¬ظˆظ‡ط±ظٹط© ظ…ظ‚ظپظ„ط© ظˆظ…ظڈط²ط§ظ…ظ†ط©. */
  sourceDelegationId?: number | null;
  borrowerName?: string;
  borrowerFather?: string;
  borrowerFamily?: string;
  borrowerMother?: string;
  borrowerBirth?: string;
  borrowerRegister?: string;
  borrowerNationalId?: string;
  borrowerAddress?: string;
  borrowerAddressType?: string;
  borrowerRepresentativeName?: string;
  borrowerRepresentativeFather?: string;
  borrowerRepresentativeFamily?: string;
  borrowerRepresentativeCapacity?: string;
  borrowerRepresentativeAddressType?: string;
  borrowerRepresentativeAddress?: string;
  /** ط·ط¨ظٹط¹ط© ط§ظ„ظ…ظ‚طھط±ط¶/ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡: ط´ط®طµ ط·ط¨ظٹط¹ظٹ (natural) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal). */
  borrowerNature?: PartyNature;
  /** ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  borrowerRegistrationNumber?: string;
  /** ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  borrowerRepresentedBy?: string;
  contractType?: string;
  contractTypeSelector?: string;
  contractNumber?: string;
  contractDate?: string;
  /** ظ…ظ„ط­ظ‚ ط§ظ„ط¹ظ‚ط¯ (ظ„ظ„ط¹ظ‚ط¯ ط§ظ„ظ…طµط±ظپظٹ ظپظ‚ط·طŒ ط§ط®طھظٹط§ط±ظٹ): ظ†ظˆط¹ظ‡ ظˆط±ظ‚ظ…ظ‡ ظˆطھط§ط±ظٹط®ظ‡. */
  annexType?: string;
  annexNumber?: string;
  annexDate?: string;
  inclusionText?: string;
  amountNumeric: number;
  amountWords?: string;
  currency?: string;
  amount2Numeric: number;
  amount2Words?: string;
  currency2?: string;
  amount3Numeric: number;
  amount3Words?: string;
  currency3?: string;
  inclusionAmountNumeric: number;
  inclusionAmountWords?: string;
  inclusionCurrency?: string;
  inclusionAmount2Numeric: number;
  inclusionAmount2Words?: string;
  inclusionCurrency2?: string;
  inclusionAmount3Numeric: number;
  inclusionAmount3Words?: string;
  inclusionCurrency3?: string;
  court?: string;
  /** ط¯ط§ط¦ط±ط© ط§ظ„طھظ†ظپظٹط° ط§ظ„ظ…ط±ط¬ط¹ظٹط© (ط³ط¬ظ„ ط§ظ„ط¯ظˆط§ط¦ط±) â€” طھظڈط¹ط±ط¶ ظ†طµظ‹ط§ ط¹ط¨ط± court ظ„ظ„طھظˆط§ظپظ‚. */
  executionCircuitId?: number | null;
  /** ط¹ظ„ظ… آ«ط¨ط§ظ†طھط¸ط§ط± ط¥ط¹ط§ط¯ط© ط§ظ„ظ‚ظٹط¯آ» â€” ط¨ط§ظ†طھط¸ط§ط± ط§ظ„ظ…ط­ط§ظ…ظٹ ظ„ط¥ط¯ط®ط§ظ„ ط§ظ„ط±ظ‚ظ… ط§ظ„ط¬ط¯ظٹط¯. */
  needsRegistration?: boolean;
  applicant?: string;
  lawyer?: string;
  /** ط§ط³ظ… ط§ظ„ظ…ط­ط§ظ…ظٹ ط§ظ„ط°ظٹ ط£ظڈط­ظٹظ„ ظ…ظ†ظ‡ ط§ظ„ظ…ظ„ظپ ط¥ظ„ظ‰ ط§ظ„ظ…ط­ط§ظ…ظٹ ط§ظ„ط­ط§ظ„ظٹ (ظپط§ط±ط؛ ط¥ط°ط§ ظ„ظ… ظٹظڈظ†ظ‚ظ„). */
  referredFromLawyer?: string;
  /** ظ„ط­ط¸ط© ط¥ط­ط§ظ„ط© ط§ظ„ظ…ظ„ظپ ط¥ظ„ظ‰ ط§ظ„ظ…ط­ط§ظ…ظٹ ط§ظ„ط­ط§ظ„ظٹ. */
  referredAt?: string;
fileNumber?: string;
  /** ط§ظ„ط±ظ‚ظ… ط§ظ„ط¸ط§ظ‡ط±: ط¢ط®ط± ط±ظ‚ظ… ط£ط³ط§ط³ â‰¤ ط³ظ†ط© ظ‚ظٹط¯ ط§ظ„ظ…ظ„ظپ ط¥ظ† ظˆظڈط¬ط¯طŒ ظˆط¥ظ„ط§ ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط£طµظ„ظٹ. */
  displayFileNumber?: string;
  /** ط³ظ†ط© ط§ظ„ط±ظ‚ظ… ط§ظ„ظپط¹ظ‘ط§ظ„ ط§ظ„ظ…ط±ط§ظپظ‚ط© ظ„ظ€ displayFileNumber. */
  displayFileYear?: string;
  fileType?: string;
  fileYear?: string;
  fileIncoming?: string;
  fileIncomingDate?: string;
  underFilingNumber?: string;
  fileRegistrationDate?: string;
  branchName?: string;
  administrativeBranchName?: string;
  /** ط­ط§ظ„ط© ط§ظ„ط¹ط±ط¶ ط§ظ„ظ…ظˆط­ط¯ط© ط§ظ„ظ…ط´طھظ‚ط© ظ…ظ† ط§ظ„ط®ظ„ظپظٹط© (ظ…ظ†ظپط°/طھط±ظٹط«/طھط­طھ ط±ظپط¹/ظ…طھط¯ط§ظˆظ„/ظ…طھط¯ط§ظˆظ„ / ظ…ظ†ظپط° ط¬ط²ط¦ظٹط§/ظ…ط´ط·ظˆط¨/ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©). */
  displayStatus?: string;
  execStatus?: string;
  execSubStatus?: string;
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ط­طµظژظ‘ظ„ط© (ط­طھظ‰ ط«ظ„ط§ط«ط© ط¨ط¹ظ…ظ„ط§طھظ‡ط§) ظپظٹ آ«ظ…ظ†ظپط° ط¨ط§ظ„طھط³ظˆظٹط©آ»/آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ». */
  collectedAmount?: number;
  collectedAmount2?: number;
  collectedAmount3?: number;
  collectedCurrency?: string;
  collectedCurrency2?: string;
  collectedCurrency3?: string;
  baraetNumber?: string;
  baraetDate?: string;
  /** طھط§ط±ظٹط® ظ‚ط±ط§ط± ط§ظ„ط¥ط­ط§ظ„ط© ط§ظ„ظ‚ط·ط¹ظٹط© ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ†طµ ط­ط±). */
  forcedExecutionDate?: string;
  /** طھط§ط±ظٹط® طھط­ظˆظٹظ„ ط¨ط¯ظ„ ط§ظ„ظ…ط¨ظٹط¹ ظ„ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط¹ظ†ط¯ آ«ط§ط¹طھط¨ط§ط± ط§ظ„ظ…ظ„ظپ ظ…ظ†ظپط°ظ‹ط§ ظƒط§ظ…ظ„ظ‹ط§ ط¨ظ‡ط°ط§ ط§ظ„ط¨ظٹط¹آ». */
  forcibleTransferDate?: string;
  /** ط±ظ‚ظ… ط§ظ„ط¥ط´ط¹ط§ط± (ط§ط®طھظٹط§ط±ظٹ) ط¹ظ†ط¯ طھط­ظˆظٹظ„ ط¨ط¯ظ„ ط§ظ„ظ…ط¨ظٹط¹ ظ„ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط©. */
  forcibleTransferNoticeNumber?: string;
  baraetRegNumber?: string;
  baraetRegDate?: string;
  tarithNumber?: string;
  tarithDate?: string;
  tarithRegNumber?: string;
  tarithRegDate?: string;
  /** ط­ظ‚ظˆظ„ ظƒطھط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط¨ط§ظ„ط³ظٹط± ط¨ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ط§ظ„طھط±ط§ط¬ط¹ (ط±ظ‚ظ…/طھط§ط±ظٹط® ط§ظ„ظƒطھط§ط¨ + ظˆط±ظˆط¯ظ‡). */
  sayerNumber?: string;
  sayerDate?: string;
  sayerRegNumber?: string;
  sayerRegDate?: string;
  /** ظ…ط¹ط±ظ‘ظپط§طھ ط§ظ„ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ط¨ط§ط¹ط© ط¨ط§ظ„ظ…ط²ط§ط¯ ط§ظ„ط¹ظ„ظ†ظٹ ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ…ظ† ط£ظ…ظˆط§ظ„ ط§ظ„ظ…ظ„ظپطŒ ط¹ط¯ط§ ظƒظپط§ظ„ط© ط§ظ„ط±ظˆط§طھط¨). */
  soldAssetIds?: number[];
  seizureDate?: string;
  immediateActions?: string;
  notes?: string;
  viewCount: number;
  printCount: number;
  createdByName?: string;
  deletedAt?: string;
  needsRotation?: boolean;
  /** طµظپط© ط§ظ„ظ…ظ„ظپ: applicant = ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط°طŒ executed = ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§. */
  generalEntitySide?: GeneralEntitySide;
  /** ط§ظ„طھط³ظ…ظٹط© ط§ظ„ط¹ط±ط¨ظٹط© ظ„ظ„طµظپط© (ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° / ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§). */
  generalEntitySideLabel?: string;
  /** ط­ط§ظ„ط© ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ»: ظ…طھط¯ط§ظˆظ„ (ظپط§ط±ط؛) / ظ…ظ†ظپط° / ظ…ط´ط·ظˆط¨. */
  executedStatus?: string;
  /** ظˆطµظپ/ط¨ظٹط§ظ† ط¥ط¶ط§ظپظٹ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedDescription?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ط§ط®ط·ط§ط± ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ظٹط؛ط°ظٹ ظپطھط±ط© ط¥ط­طµط§ط¦ظٹط© آ«ظ…طھط¯ط§ظˆظ„ ظ„ظ„ط¶ط¯آ»). */
  fileReceiptDate?: string;
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ط¥ط®ط·ط§ط± ط§ظ„طھظ†ظپظٹط°ظٹ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  fileReceiptNumber?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط¯ظپط¹ظ‡ ظ…ظ† ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedRequiredAmount?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط£ظˆظ„ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedRequiredCurrency?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedRequiredAmount2?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ†ظٹ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedRequiredCurrency2?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedRequiredAmount3?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ„ط« ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedRequiredCurrency3?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ط°ظٹ ط¯ظپط¹طھظ‡ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedPaidAmount?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط£ظˆظ„ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedPaidCurrency?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedPaidAmount2?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedPaidCurrency2?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedPaidAmount3?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedPaidCurrency3?: string;
  /** طھط§ط±ظٹط® ط§ظٹط¯ط§ط¹ظ‡ ط­ط³ط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ». */
  executedDepositDate?: string;
  /** طھط§ط±ظٹط® ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedExecutionDate?: string;
  /** ظ„ط­ط¸ط© ط´ط·ط¨ ط§ظ„ظ…ظ„ظپ (طھط§ط±ظٹط® ط¥ط¯ط®ط§ظ„ظ‡ ظ…ظ† ط§ظ„ظ†ظ…ظˆط°ط¬ ظˆطھط¨ظ‚ظ‰ ظ…ط­ظپظˆط¸ط© ط¨ط¹ط¯ ط§ظ„ط¥ط¹ط§ط¯ط©). */
  struckOffDate?: string;
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ظ…ظ„ظپ ظ…ط´ط·ظˆط¨ ط¥ظ„ظ‰ ط§ظ„ظ…طھط¯ط§ظˆظ„ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalFileReceiptNumber?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalFileReceiptDate?: string;
  /** ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط¥ظ„ط²ط§ظ…ظٹ) â€” ظٹط¹ظˆط¯ ط§ظ„ظ…ظ„ظپ ط¨ظ‡ ظ„ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط©. */
  renewalFileNumber?: string;
  /** ظ†ظˆط¹ ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalFileType?: string;
/** طھط§ط±ظٹط® ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalDate?: string;
  /** ط±ظ‚ظ… ظƒطھط§ط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© ط¨ط¹ط¯ظ… ظˆط¬ظˆط¯ ط£ظ…ظˆط§ظ„ ظ„ظ„طھظ†ظپظٹط° ط¹ظ„ظٹظ‡ط§ ظپظٹ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ». */
  noFundsDemandNumber?: string;
  /** طھط§ط±ظٹط® ظƒطھط§ط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© ط¨ط¹ط¯ظ… ظˆط¬ظˆط¯ ط£ظ…ظˆط§ظ„ ظ„ظ„طھظ†ظپظٹط° ط¹ظ„ظٹظ‡ط§ (ظٹظڈط¹ط±ط¶ ط¨طµظٹط؛ط© yyyy-MM-dd). */
  noFundsDemandDate?: string;
  /** ط±ظ‚ظ… ظƒطھط§ط¨ ط§ظ„ط¥ط­ط§ظ„ط© ظپظٹ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ». */
  startReferralNumber?: string;
  /** طھط§ط±ظٹط® ظƒطھط§ط¨ ط§ظ„ط¥ط­ط§ظ„ط© (ظٹظڈط¹ط±ط¶ ط¨طµظٹط؛ط© yyyy-MM-dd). */
  startReferralDate?: string;
  guarantors: GuarantorDto[];
  assets: AssetDto[];
  borrowerHeirs?: HeirDto[];
  executionActions?: ExecutionActionDto[];
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  /** ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظˆط§ط­ط¯ط© ط£ظˆ ط£ظƒط«ط±). */
  applicantPublicEntities?: ApplicantPublicEntityDto[];
  /** ط³ط¬ظ„ طھط¹ط§ظ‚ط¨ ط§ظ„ظ…ط­ط§ظ…ظٹظ† ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ (ظ…ظ†ط´ط¦ + ظƒظ„ ط§ظ„ظ…ط­ط§ظ…ظٹظ† ط§ظ„ظ…طھط¹ط§ظ‚ط¨ظٹظ† ظ…ط¹ طھظˆط§ط±ظٹط® ط§ظ„ط¥ط­ط§ظ„ط©). */
  assignments?: DocumentAssignmentDto[];
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ط§ط®طھظٹط§ط±ظٹ). */
  fileArrivalNumber?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظ†طµ ط­ط±). */
  fileArrivalDate?: string;
  /** آ«ظˆظ‚ظˆط¹ط§طھ ط§ظ„ظ…ظ„ظپآ»: ط³ط¬ظ„ ط²ظ…ظ†ظٹ ظ„ظƒظ„ ط´ط·ط¨ ظˆطھط¬ط¯ظٹط¯ ظپظٹ ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ»/آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» (ظ…ط±طھط¨ طھطµط§ط¹ط¯ظٹظ‹ط§). */
  occurrences?: DocumentOccurrenceDto[];
  /** ظ‡ظ„ ظ„ظ„ظ…ظ„ظپ ط§ط³طھط¦ظ†ط§ظپ ظ…ظ†ط¸ظˆط± ظˆط§ط­ط¯ ط¹ظ„ظ‰ ط§ظ„ط£ظ‚ظ„ â€” ظ„ط´ط§ط±ط© آ«ط§ط³طھط¦ظ†ط§ظپآ» ط¨ط¬ط§ظ†ط¨ ظ†طھط§ط¦ط¬ ط§ظ„ط¨ط­ط« (ط§ظ„ظ…ط­ط³ظˆظ… ظˆط§ظ„ظ…ط´ط·ظˆط¨ ظ„ط§ ظٹظڈط¸ظ‡ط±ط§ظ† ط§ظ„ط´ط§ط±ط©). */
  hasAppeals?: boolean;
  /** ظ…ط¹ط±ظپ ط£ظˆظ„ ط§ط³طھط¦ظ†ط§ظپ ظ…ظ†ط¸ظˆط± ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ ط¹ظ†ط¯ ظˆط¬ظˆط¯ظ‡ (ظ„ظ„ط§ظ†طھظ‚ط§ظ„ ط¥ظ„ظ‰ طھظپط§طµظٹظ„ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ). */
  matchedAppealId?: number;
}

export interface DocumentUpsertRequest {
  documentType?: string;
  borrowerName?: string;
  borrowerFather?: string;
  borrowerFamily?: string;
  borrowerMother?: string;
  borrowerBirth?: string;
  borrowerRegister?: string;
  borrowerNationalId?: string;
  borrowerAddress?: string;
  borrowerAddressType?: string;
  borrowerRepresentativeName?: string;
  borrowerRepresentativeFather?: string;
  borrowerRepresentativeFamily?: string;
  borrowerRepresentativeCapacity?: string;
  borrowerRepresentativeAddressType?: string;
  borrowerRepresentativeAddress?: string;
  /** ط·ط¨ظٹط¹ط© ط§ظ„ظ…ظ‚طھط±ط¶/ط§ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡: ط´ط®طµ ط·ط¨ظٹط¹ظٹ (natural) ط£ظˆ ط´ط®طµ ط§ط¹طھط¨ط§ط±ظٹ (legal). */
  borrowerNature?: PartyNature;
  /** ط±ظ‚ظ… طھط³ط¬ظٹظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  borrowerRegistrationNumber?: string;
  /** ظ…ظ† ظٹظ…ط«ظ„ ط§ظ„ط´ط®طµ ط§ظ„ط§ط¹طھط¨ط§ط±ظٹ ط¹ظ†ط¯ ط§ظ„ط·ط¨ظٹط¹ط© ط§ظ„ط§ط¹طھط¨ط§ط±ظٹط©. */
  borrowerRepresentedBy?: string;
  contractType?: string;
  contractTypeSelector?: string;
  contractNumber?: string;
  contractDate?: string;
  /** ظ…ظ„ط­ظ‚ ط§ظ„ط¹ظ‚ط¯ (ظ„ظ„ط¹ظ‚ط¯ ط§ظ„ظ…طµط±ظپظٹ ظپظ‚ط·طŒ ط§ط®طھظٹط§ط±ظٹ): ظ†ظˆط¹ظ‡ ظˆط±ظ‚ظ…ظ‡ ظˆطھط§ط±ظٹط®ظ‡. */
  annexType?: string;
  annexNumber?: string;
  annexDate?: string;
  inclusionText?: string;
  amountNumeric?: number;
  amountWords?: string;
  currency?: string;
  amount2Numeric?: number;
  amount2Words?: string;
  currency2?: string;
  amount3Numeric?: number;
  amount3Words?: string;
  currency3?: string;
  inclusionAmountNumeric?: number;
  inclusionAmountWords?: string;
  inclusionCurrency?: string;
  inclusionAmount2Numeric?: number;
  inclusionAmount2Words?: string;
  inclusionCurrency2?: string;
  inclusionAmount3Numeric?: number;
  inclusionAmount3Words?: string;
  inclusionCurrency3?: string;
  court?: string;
  /** ط¯ط§ط¦ط±ط© ط§ظ„طھظ†ظپظٹط° (ط³ط¬ظ„ ط§ظ„ط¯ظˆط§ط¦ط±): ط¥ظ„ط²ط§ظ…ظٹط© â€” ط§ظ„ط®ط§ط¯ظ… ظٹط´طھظ‚ court ظ…ظ†ظ‡ط§ ظˆظٹطھط¬ط§ظ‡ظ„ ط§ظ„ظ†طµ ط¯ط§ط¦ظ…ظ‹ط§. */
  executionCircuitId?: number | null;
  applicant?: string;
  /** ظ†ط³ط®ط© طھط³ط±ظٹط¹: ظ…ط¹ط±ظ‘ظپ ظ‚ظٹط¯ ط£ظˆظ„ ط¬ظ‡ط© ط·ط§ظ„ط¨ ظ…ط±طھط¨ط·ط© ط¨ط§ظ„ط³ط¬ظ„ â€” طھظڈط­ط¯ظژظ‘ط« ط¹ظ†ط¯ ط§ظ„ط­ظپط¸ (ظ„ظ„ظپظ„طھط±ط© ظپظٹ ط§ظ„ط¨ظˆط§ط¨ط©). */
  applicantRegistryId?: number | null;
  fileNumber?: string;
  fileType?: string;
  fileYear?: string;
  fileIncoming?: string;
  fileIncomingDate?: string;
  underFilingNumber?: string;
  fileRegistrationDate?: string;
  branchName?: string;
  seizureDate?: string;
  immediateActions?: string;
  notes?: string;
  guarantors: GuarantorDto[];
  assets: AssetDto[];
  borrowerHeirs?: HeirDto[];
  initialActions?: InitialActionRequest[];
  /** طµظپط© ط§ظ„ظ…ظ„ظپ: طھظڈط«ط¨ظژظ‘طھ ط¹ظ†ط¯ ط§ظ„ط¥ظ†ط´ط§ط، ظˆظ„ط§ طھظڈط؛ظٹظژظ‘ط± ط¹ظ†ط¯ ط§ظ„طھط¹ط¯ظٹظ„. */
  generalEntitySide?: GeneralEntitySide;
  /** ط­ط§ظ„ط© ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ»: ظ…طھط¯ط§ظˆظ„ (ظپط§ط±ط؛) / ظ…ظ†ظپط° / ظ…ط´ط·ظˆط¨. */
  executedStatus?: string;
  /** طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨ (ظٹظڈط¯ط®ظ„ ط¹ظ†ط¯ ط§ط®طھظٹط§ط± آ«ظ…ط´ط·ظˆط¨آ»). */
  struckOffDate?: string;
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ظ…ظ„ظپ ظ…ط´ط·ظˆط¨ ط¥ظ„ظ‰ ط§ظ„ظ…طھط¯ط§ظˆظ„ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalFileReceiptNumber?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalFileReceiptDate?: string;
  /** ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط¥ظ„ط²ط§ظ…ظٹ) â€” ظٹط¹ظˆط¯ ط§ظ„ظ…ظ„ظپ ط¨ظ‡ ظ„ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط©. */
  renewalFileNumber?: string;
  /** ظ†ظˆط¹ ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalFileType?: string;
  /** ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط© (ط¥ظ„ط²ط§ظ…ظٹط© ظپظٹ ظ†ط¸ط§ظ… آ«ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ظ…ظ„ظپ ظ…ط´ط·ظˆط¨). */
  renewalYear?: number;
  /** طھط§ط±ظٹط® ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط¥ط¹ط§ط¯ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط´ط·ظˆط¨ (ط§ط®طھظٹط§ط±ظٹ). */
  renewalDate?: string;
  /** ظˆطµظپ/ط¨ظٹط§ظ† ط¥ط¶ط§ظپظٹ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedDescription?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ط§ط®ط·ط§ط± ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  fileReceiptDate?: string;
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ط¥ط®ط·ط§ط± ط§ظ„طھظ†ظپظٹط°ظٹ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  fileReceiptNumber?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط¯ظپط¹ظ‡ ظ…ظ† ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedRequiredAmount?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط£ظˆظ„ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedRequiredCurrency?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedRequiredAmount2?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ†ظٹ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedRequiredCurrency2?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedRequiredAmount3?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط§ظ„ط«ط§ظ„ط« ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedRequiredCurrency3?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ط°ظٹ ط¯ظپط¹طھظ‡ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedPaidAmount?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط£ظˆظ„ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedPaidCurrency?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedPaidAmount2?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ†ظٹ (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedPaidCurrency2?: string;
  /** ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ط®طھظٹط§ط±ظٹ) ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedPaidAmount3?: number;
  /** ط¹ظ…ظ„ط© ط§ظ„ظ…ط¨ظ„ط؛ ط§ظ„ظ…ط¯ظپظˆط¹ ط§ظ„ط«ط§ظ„ط« (ط§ظپطھط±ط§ط¶ظٹظ‹ط§ ظ„ظٹط±ط© ط³ظˆط±ظٹط©). */
  executedPaidCurrency3?: string;
  /** طھط§ط±ظٹط® ط§ظٹط¯ط§ط¹ظ‡ ط­ط³ط§ط¨ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظپظٹ ظˆط¶ط¹ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ». */
  executedDepositDate?: string;
  /** طھط§ط±ظٹط® ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ». */
  executedExecutionDate?: string;
  /** طھط§ط±ظٹط® ظ‚ط±ط§ط± ط§ظ„ط¥ط­ط§ظ„ط© ط§ظ„ظ‚ط·ط¹ظٹط© ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» (ظ†طµ ط­ط±). */
  forcedExecutionDate?: string;
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  /** ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط° ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظˆط§ط­ط¯ط© ط£ظˆ ط£ظƒط«ط±). */
  applicantPublicEntities?: ApplicantPublicEntityDto[];
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ط§ط®طھظٹط§ط±ظٹ). */
  fileArrivalNumber?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ظ…ظ„ظپ ظپظٹ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ» (ظ†طµ ط­ط±). */
  fileArrivalDate?: string;
  /** ط¹ط¯ظ‘ط§ط¯ ط§ظ„طھط²ط§ظ…ظ† ط§ظ„ظ…طھظپط§ط¦ظ„ (RF-010) ظƒظ…ط§ ظ‚ظڈط±ط¦ ط¹ظ†ط¯ ط§ظ„ط¬ظ„ط¨ â€” ط؛ظٹط§ط¨ظ‡ = ظ‚ط¨ظˆظ„ ط¨ظ„ط§ ظپط­طµ ظ…ط¨ظƒط±. */
  version?: number | null;
}

export interface DocumentFilterOptionsDto {
  applicants: string[];
  courts: string[];
  lawyers: string[];
  administrativeBranches: string[];
  branches: string[];
  executedEntities: string[];
  publicEntityBranches: string[];
}

export interface RotationDocumentDto {
  documentId: number;
  court?: string;
  borrowerName?: string;
  borrowerFather?: string;
  borrowerFamily?: string;
  fileNumber?: string;
  fileType?: string;
  baseNumber?: string;
/** ط§ط³ظ… ط§ظ„ط¹ط±ط¶ ط§ظ„ظ…ظˆط­ط¯ â€” ط§ط³ظ… ط§ظ„ظ…ظ‚طھط±ط¶طŒ ط£ظˆ ط§ط³ظ… ط·ط§ظ„ط¨ ط§ظ„ط¹ط±ط¶ ظ„ظ…ظ„ظپط§طھ ط§ظ„ط¹ط§ط¦ظ„طھظٹظ† Executed + Deposit. */
  displayName?: string;
  /** ط§ظ„ط±ظ‚ظ… ط§ظ„ظپط¹ظ‘ط§ظ„ ط§ظ„ظ…ط¹ط±ظˆط¶ ظپظٹ طµظپط­ط© ط§ظ„طھط¯ظˆظٹط± (ط§ظ„ظ…ط­ظ„ظ„ ط§ظ„ظ…ط±ظƒط²ظٹ: ط¢ط®ط± ط±ظ‚ظ… ط£ط³ط§ط³ â‰¤ ط³ظ†ط© ظ‚ظٹط¯ ط§ظ„ظ…ظ„ظپطŒ ظˆط¥ظ„ط§ ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ). */
  effectiveFileNumber?: string;
  /** ط³ظ†ط© ط§ظ„ط±ظ‚ظ… ط§ظ„ظپط¹ظ‘ط§ظ„ ط§ظ„ظ…ط¹ط±ظˆط¶ ظپظٹ طµظپط­ط© ط§ظ„طھط¯ظˆظٹط±. */
  effectiveFileYear?: string;
}

export interface BaseNumberEntry {
  documentId: number;
  baseNumber?: string | null;
}

export interface SaveBaseNumbersRequest {
  entries: BaseNumberEntry[];
}

/** ط³ط¬ظ„ ط³ظ†ط© ظˆط§ط­ط¯ط© ظپظٹ طھط§ط±ظٹط® ط£ط±ظ‚ط§ظ… ط§ظ„ط£ط³ط§ط³ ظ„ظ„ظ…ظ„ظپ. */
export interface BaseNumberHistoryDto {
  year: number;
  baseNumber: string;
}

/** ظ†ظˆط¹ ظˆظ‚ط¹ط© ط§ظ„ظ…ظ„ظپ: ط´ط·ط¨/طھط¬ط¯ظٹط¯ (ظˆط¶ط¹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡آ») ط£ظˆ ط¥ط¬ط±ط§ط، طھط؛ظٹظٹط± ط­ط§ظ„ط© (ظ†ط¸ط§ظ… آ«ط·ط§ظ„ط¨ط© طھظ†ظپظٹط°آ») ط£ظˆ طھط؛ظٹظٹط± ط¬ظ‡ط© ط¢ظ„ظٹ. */
export type OccurrenceType = 'struck-off' | 'renewal' | 'deferred' | 'settled' | 'forcible' | 'revert' | 'recovered' | 'entity-change' | 'referred-to-start' | 'circuit-referred' | 'circuit-reregistered' | 'circuit-renamed';

/** ظˆظ‚ط¹ط© ظˆط§ط­ط¯ط© ظ…ظ† آ«ظˆظ‚ظˆط¹ط§طھ ط§ظ„ظ…ظ„ظپآ»: ط´ط·ط¨/طھط¬ط¯ظٹط¯ ط£ظˆ ط¥ط¬ط±ط§ط، طھط؛ظٹظٹط± ط­ط§ظ„ط© (طھط±ظٹط«/ظ…ظ†ظپط°/طھط±ط§ط¬ط¹) ط£ظˆ طھط؛ظٹظٹط± ط¬ظ‡ط© ط¢ظ„ظٹ. */
export interface DocumentOccurrenceDto {
  id: number;
  occurrenceType: OccurrenceType;
  /** ط§ظ„طھط³ظ…ظٹط© ط§ظ„ط¹ط±ط¨ظٹط© ظ„ظ„ظˆظ‚ط¹ط© (ط´ط·ط¨ / طھط¬ط¯ظٹط¯ / طھط±ظٹط« / ظ…ظ†ظپط° ط¨ط§ظ„طھط³ظˆظٹط© / ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§ / طھط±ط§ط¬ط¹ / طھط؛ظٹظٹط± ط¬ظ‡ط©). */
  occurrenceTypeLabel: string;
  /** طھط§ط±ظٹط® ط§ظ„ظˆظ‚ط¹ط©: طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨ ط£ظˆ ط§ظ„طھط¬ط¯ظٹط¯ ط£ظˆ ط§ظ„ط¥ط¬ط±ط§ط،. */
  eventDate?: string;
  /** ط§ظ„ط±ظ‚ظ… ط§ظ„ظ…ط¹ظ†ظٹ ط¨ط§ظ„ظˆظ‚ط¹ط©: ط§ظ„ط±ظ‚ظ… ط§ظ„ظ‚ط¯ظٹظ… ط§ظ„ظ…ظڈط´ط·ظˆط¨ ط£ظˆ ط§ظ„ط±ظ‚ظ… ط§ظ„ط¬ط¯ظٹط¯ ظ„ظ„طھط¬ط¯ظٹط¯. */
  fileNumber?: string;
  /** ظ†ظˆط¹ ط§ظ„ظ…ظ„ظپ ط§ظ„ط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط§ظ„طھط¬ط¯ظٹط¯. */
  fileType?: string;
  /** ط³ظ†ط© ط§ظ„ظˆظ‚ط¹ط©: ط³ظ†ط© ط§ظ„ط´ط·ط¨ ط£ظˆ ط³ظ†ط© ط§ظ„ط¥ط¹ط§ط¯ط© ظ„ظ„طھط¬ط¯ظٹط¯. */
  year?: number;
  /** ط±ظ‚ظ… ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط§ظ„طھط¬ط¯ظٹط¯. */
  receiptNumber?: string;
  /** طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ط®ط·ط§ط± ط§ظ„طھط¬ط¯ظٹط¯ ط¹ظ†ط¯ ط§ظ„طھط¬ط¯ظٹط¯. */
  receiptDate?: string;
  /** ط­ظ‚ظˆظ„ ط¥ط¬ط±ط§ط،ط§طھ طھط؛ظٹظٹط± ط§ظ„ط­ط§ظ„ط© (ظ…ظپط§طھظٹط­ ط§ظ„ط®ط¯ظ…ط©: tarith*طŒ baraet*طŒ sayer*طŒ collectedAmount*طŒ execSubStatusطŒ soldAssetIds). */
  details?: Record<string, string>;
  /** ط§ظ„ط³ط±ط¯ ط§ظ„ظ†طµظٹ ط§ظ„ط­ط± ظ„ظ„ظˆظ‚ط¹ط© ط§ظ„ط¢ظ„ظٹط© (ظ†ظˆط¹ آ«طھط؛ظٹظٹط± ط¬ظ‡ط©آ»): ظ†طµ ط§ظ„ط­ط¯ط« ظ…ط¹ ظ…ط±ط¬ط¹ظ‡ â€” ظٹظڈط¹ط±ط¶ ط¨ط¯ظ„ ط§ظ„ظ‚ط§ظ…ظˆط³. */
  detailsText?: string | null;
/** ط§ط³ظ… ظ…ظ† ط£ط¯ط®ظ„ ط§ظ„ظˆظ‚ط¹ط©. */
  createdByName?: string;
  /** ظ…طµط¯ط± ط§ظ„ظˆظ‚ط¹ط©: "system" (ط³ط¬ظ‘ظ„ظ‡ط§ ط§ظ„ظ†ط¸ط§ظ… ط¢ظ„ظٹظ‹ط§ â€” ظ„ط§ طھظڈط¹ط¯ظژظ‘ظ„/طھظڈط­ط°ظپ ظ…ظ† ط§ظ„ظˆط§ط¬ظ‡ط©) ط£ظˆ "manual" (ط¥ط¯ط®ط§ظ„ ظٹط¯ظˆظٹ). */
  source?: 'system' | 'manual';
  /** ط§ط³ظ… ط¯ط§ط¦ط±ط© ط§ظ„ظ…طµط¯ط± ظ†طµظ‹ط§ ظ…ط¬ظ…ط¯ظ‹ط§ ظ„ظ„طھط§ط±ظٹط® (ظٹظڈظ…ظ„ط£ ط¹ظ†ط¯ ط§ظ„ط¥ط­ط§ظ„ط©). */
  fromCircuitName?: string | null;
  /** ط§ط³ظ… ط¯ط§ط¦ط±ط© ط§ظ„ظˆط¬ظ‡ط© ظ†طµظ‹ط§ ظ…ط¬ظ…ط¯ظ‹ط§ ظ„ظ„طھط§ط±ظٹط® (ظٹظڈظ…ظ„ط£ ط¹ظ†ط¯ ط§ظ„ط¥ط­ط§ظ„ط©). */
  toCircuitName?: string | null;
}

/* â”€â”€ ط³ط¬ظ„ طھط¹ط¯ظٹظ„ط§طھ ط§ظ„ظ…ظ„ظپ ط¹ظ„ظ‰ ظ…ط³طھظˆظ‰ ط§ظ„ط­ظ‚ظˆظ„ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** طھط؛ظٹظ‘ط± ط­ظ‚ظ„ ظˆط§ط­ط¯: ط§ظ„ظ‚ظٹظ…ط© ظ‚ط¨ظ„ ط§ظ„طھط¹ط¯ظٹظ„ ظˆط¨ط¹ط¯ظ‡ ط¨طھط³ظ…ظٹط© ط¹ط±ط¨ظٹط© ظ…ط¬ظ…ظ‘ط¯ط©. */
export interface DocumentFieldChangeDto {
  fieldLabel: string;
  fieldKey: string;
  oldValue?: string | null;
  newValue?: string | null;
}

/** ظ…ط¬ظ…ظˆط¹ط© طھط¹ط¯ظٹظ„ط§طھ ظˆط§ط­ط¯ط© (ط¥ط¯ط®ط§ظ„ طھط¯ظ‚ظٹظ‚) ط¨ظƒظ„ ط­ظ‚ظˆظ„ظ‡ط§ ط§ظ„ظ…طھط؛ظٹط±ط©. */
export interface DocumentChangeGroupDto {
  auditLogId: number;
  actionType: string;
  userName?: string | null;
  timestamp: string;
  changes: DocumentFieldChangeDto[];
}
