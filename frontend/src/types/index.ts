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

export interface LoginBranchChoiceDto {
  branchId: number | null;
  branchName: string | null;
}

export interface LoginBranchSelectionResponse {
  requiresBranchSelection: true;
  branches: LoginBranchChoiceDto[];
}

export interface BranchDto {
  id: number;
  name: string;
  code: string;
  address?: string;
  phone?: string;
  isActive?: boolean;
  userCount?: number;
  documentCount?: number;
}

export interface CreateBranchRequest {
  name: string;
  code: string;
  address?: string | null;
  phone?: string | null;
}

export interface UpdateBranchRequest {
  name: string;
  code: string;
  address?: string | null;
  phone?: string | null;
  isActive: boolean;
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
  heirs?: HeirDto[];
}

/** وريث لمنفذ عليه متوفى (المقترض أو أحد الكفلاء). */
export interface HeirDto {
  id?: number;
  name?: string;
  addressType?: string;
  address?: string;
}

/** عقار ضمن قائمة العقارات المرهونة، ملاكه قائمة أسماء (واحد أو أكثر) بترتيب الاختيار. */
export interface RealEstateDto {
  id?: number;
  owners?: string[];
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

/** صفة الملف الثابتة (تُثبَّت عند الإنشاء ولا تتغير أثناء التعديل). */
export type GeneralEntitySide = 'applicant' | 'executed';

/** وريث لمورثٍ متوفى في وضع «منفذ عليه» (اسم ثلاثي). */
export interface ExecutedHeirDto {
  id?: number;
  heirName?: string;
  heirFather?: string;
  heirFamily?: string;
  addressType?: string;
  heirAddress?: string;
}

/** طالب التنفيذ في وضع «منفذ عليه» مع ورثة مورثه المتوفى (إن اختير «إضافة لتركة»). */
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
  heirs?: ExecutedHeirDto[];
}

/** الجهة العامة المنفذ عليها في وضع «منفذ عليه». */
export interface ExecutedPublicEntityDto {
  id?: number;
  entityName?: string;
  entityBranch?: string;
}

/** الشخص الطبيعي المنفذ عليه في وضع «منفذ عليه» مع ورثة مورثه المتوفى (إن اختير «إضافة لتركة»). */
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
  /** الرقم الظاهر: رقم أساس السنة الحالية إن وُجد، وإلا رقم الملف الأصلي. */
  displayFileNumber?: string;
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
  needsRotation?: boolean;
  /** صفة الملف: applicant = الجهة العامة طالبة التنفيذ، executed = الجهة العامة منفذ عليها. */
  generalEntitySide?: GeneralEntitySide;
  /** التسمية العربية للصفة (الجهة العامة طالبة التنفيذ / الجهة العامة منفذ عليها). */
  generalEntitySideLabel?: string;
  /** حالة وضع «الجهة العامة منفذ عليها»: متداول (فارغ) / منفذ / مشطوب. */
  executedStatus?: string;
  /** وصف/بيان إضافي في وضع «الجهة العامة منفذ عليها». */
  executedDescription?: string;
  /** تاريخ ورود الملف في وضع «الجهة العامة منفذ عليها» (يغذي فترة إحصائية «متداول للضد»). */
  fileReceiptDate?: string;
  /** المبلغ المطلوب دفعه من الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount?: number;
  /** المبلغ الذي دفعته الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount?: number;
  /** لحظة شطب الملف (تاريخ إدخاله من النموذج وتبقى محفوظة بعد الإعادة). */
  struckOffDate?: string;
  guarantors: GuarantorDto[];
  realEstates: RealEstateDto[];
  borrowerHeirs?: HeirDto[];
  executionActions?: ExecutionActionDto[];
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
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
  borrowerHeirs?: HeirDto[];
  initialActions?: InitialActionRequest[];
  /** صفة الملف: تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل. */
  generalEntitySide?: GeneralEntitySide;
  /** حالة وضع «الجهة العامة منفذ عليها»: متداول (فارغ) / منفذ / مشطوب. */
  executedStatus?: string;
  /** تاريخ الشطب (يُدخل عند اختيار «مشطوب»). */
  struckOffDate?: string;
  /** وصف/بيان إضافي في وضع «الجهة العامة منفذ عليها». */
  executedDescription?: string;
  /** تاريخ ورود الملف في وضع «الجهة العامة منفذ عليها». */
  fileReceiptDate?: string;
  /** المبلغ المطلوب دفعه من الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount?: number;
  /** المبلغ الذي دفعته الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount?: number;
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
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
  /** عدد ملفات وضع «الجهة العامة منفذ عليها» المتداولة في الفترة (بطاقة «متداول للضد»). */
  tradingAgainstCount: number;
  /** مجموع المبالغ المطلوب دفعها من الجهات العامة في ملفات المتداول (بطاقة «متداول للضد»). */
  tradingAgainstAmount: number;
  /** عدد ملفات وضع «الجهة العامة منفذ عليها» المنفذة في الفترة (بطاقة «منفذ للضد»). */
  executedAgainstCount: number;
  /** مجموع المبالغ التي دفعتها الجهات العامة في ملفات المنفذ (بطاقة «منفذ للضد»). */
  executedAgainstAmount: number;
  totalAmount: number;
  activeAmount: number;
  draftsAmount: number;
  deferredAmount: number;
  totalAmount2: number;
  activeAmount2: number;
  draftsAmount2: number;
  deferredAmount2: number;
  periodYear: number;
  periodQuarter: number | null;
  periodMonth: number | null;
}

export interface DocumentFilterOptionsDto {
  applicants: string[];
  courts: string[];
  lawyers: string[];
  administrativeBranches: string[];
  branches: string[];
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

export interface TransferAllRequest {
  sourceLawyerId: number;
  targetLawyerId: number;
}

export type HeadAlertTargetType = 'document' | 'lawyer' | 'branch';

export interface HeadAlertDto {
  id: number;
  message: string;
  targetType: HeadAlertTargetType;
  documentId?: number | null;
  documentTitle?: string | null;
  targetLawyerId?: number | null;
  targetLawyerName?: string | null;
  isRead?: boolean;
  recipientCount?: number;
  unreadCount?: number;
  createdAt: string;
  createdByName?: string;
}

export interface CreateHeadAlertRequest {
  targetType: HeadAlertTargetType;
  documentId?: number | null;
  targetLawyerId?: number | null;
  message: string;
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
}

export interface BaseNumberEntry {
  documentId: number;
  baseNumber?: string | null;
}

export interface SaveBaseNumbersRequest {
  entries: BaseNumberEntry[];
}

/** سجل سنة واحدة في تاريخ أرقام الأساس للملف. */
export interface BaseNumberHistoryDto {
  year: number;
  baseNumber: string;
}

