export type Role = 'lawyer' | 'head' | 'manager' | 'admin';

export interface UserDto {
  id: number;
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  branchName?: string | null;
}

export interface LoginResponse {
  token: string;
  user: UserDto;
}

export interface BranchDto {
  id: number;
  name: string;
  code: string;
  address?: string;
}

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
}

export interface RealEstateDto {
  id?: number;
  owner?: string;
  property?: string;
  propertyNumber?: string;
  propertyDistrict?: string;
  landRegistry?: string;
  shareType?: string;
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

export interface DocumentResponse {
  id: number;
  createdAt: string;
  updatedAt: string;
  createdById?: number;
  branchId?: number;
  documentType?: string;
  isDraft: boolean;
  borrowerName?: string;
  borrowerFather?: string;
  borrowerFamily?: string;
  borrowerMother?: string;
  borrowerBirth?: string;
  borrowerRegister?: string;
  borrowerNationalId?: string;
  borrowerAddress?: string;
  borrowerAddressType?: string;
  contractType?: string;
  contractTypeSelector?: string;
  contractNumber?: string;
  contractDate?: string;
  inclusionText?: string;
  amountNumeric: number;
  amountWords?: string;
  currency?: string;
  amount2Numeric: number;
  amount2Words?: string;
  currency2?: string;
  inclusionAmountNumeric: number;
  inclusionAmountWords?: string;
  inclusionCurrency?: string;
  court?: string;
  applicant?: string;
  lawyer?: string;
  fileNumber?: string;
  fileType?: string;
  fileYear?: string;
  fileIncoming?: string;
  fileIncomingDate?: string;
  underFilingNumber?: string;
  fileRegistrationDate?: string;
  branchName?: string;
  administrativeBranchName?: string;
  execStatus?: string;
  execSubStatus?: string;
  collectedAmount?: number;
  baraetNumber?: string;
  baraetDate?: string;
  baraetRegNumber?: string;
  baraetRegDate?: string;
  tarithNumber?: string;
  tarithDate?: string;
  tarithRegNumber?: string;
  tarithRegDate?: string;
  seizureDate?: string;
  immediateActions?: string;
  notes?: string;
  viewCount: number;
  printCount: number;
  createdByName?: string;
  deletedAt?: string;
  guarantors: GuarantorDto[];
  realEstates: RealEstateDto[];
  executionActions?: ExecutionActionDto[];
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
  contractType?: string;
  contractTypeSelector?: string;
  contractNumber?: string;
  contractDate?: string;
  inclusionText?: string;
  amountNumeric?: number;
  amountWords?: string;
  currency?: string;
  amount2Numeric?: number;
  amount2Words?: string;
  currency2?: string;
  inclusionAmountNumeric?: number;
  inclusionAmountWords?: string;
  inclusionCurrency?: string;
  court?: string;
  applicant?: string;
  lawyer?: string;
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
  realEstates: RealEstateDto[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}

export interface DashboardStatsDto {
  totalDocuments: number;
  totalDrafts: number;
  totalExecuted: number;
  totalDeferred: number;
  totalActive: number;
  totalBorrowers: number;
  totalAmount: number;
  totalCollectedAmount: number;
}

export interface ReminderDto {
  actionId: number;
  documentId: number;
  documentType?: string;
  borrowerName?: string;
  borrowerFather?: string;
  borrowerFamily?: string;
  actionText: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  dueDate: string;
}

export interface MonthlyStatDto {
  year: number;
  month: number;
  count: number;
}

export type StatsPeriod = 'yearly' | 'quarterly' | 'monthly';

export interface ManagerStatsDto {
  totalFiles: number;
  active: number;
  drafts: number;
  deferred: number;
  settledCount: number;
  settledCollected: number;
  forcibleCount: number;
  forcibleCollected: number;
  periodYear: number;
  periodQuarter: number | null;
  periodMonth: number | null;
}

export interface ManagerPeriodPointDto {
  year: number;
  month: number;
  count: number;
}

export interface ManagerLawyerStatDto {
  lawyerId: number;
  lawyerName: string;
  totalCount: number;
  points: ManagerPeriodPointDto[];
}

export interface BranchSummaryDto {
  branchId: number;
  branchName: string;
  totalDocuments: number;
  totalDrafts: number;
  totalAmount: number;
}

export interface LawyerListItem {
  id: number;
  username: string;
  fullName: string;
  isActive: boolean;
  branchId: number | null;
  branchName?: string | null;
}

export interface CreateLawyerRequest {
  username: string;
  fullName: string;
  password: string;
  branchId?: number | null;
}

export interface SetUserActiveRequest {
  isActive: boolean;
}

export interface UserListItem {
  id: number;
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  branchName?: string | null;
  isActive: boolean;
}

export interface CreateUserRequest {
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  password: string;
}

export interface UpdateUserRequest {
  fullName: string;
  role: Role;
  branchId: number | null;
  isActive: boolean;
  password?: string | null;
}

export interface TransferDocumentRequest {
  targetLawyerId: number;
}
