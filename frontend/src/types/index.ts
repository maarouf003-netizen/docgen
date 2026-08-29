export type Role = 'lawyer' | 'head' | 'manager' | 'admin' | 'entitymanager';

export interface UserDto {
  id: number;
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  branchName?: string | null;
}

export interface LoginResponse {
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
  /** المحافظة التابعة لها الفرع — تحدد نطاق رئيس القسم في سجل الجهات العامة. */
  governorate?: string | null;
  isActive?: boolean;
  userCount?: number;
  documentCount?: number;
}

export interface CreateBranchRequest {
  name: string;
  code: string;
  address?: string | null;
  phone?: string | null;
  governorate?: string | null;
}

export interface UpdateBranchRequest {
  name: string;
  code: string;
  address?: string | null;
  phone?: string | null;
  isActive: boolean;
  governorate?: string | null;
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
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeAddressType?: string;
  representativeAddress?: string;
  heirs?: HeirDto[];
  /** طبيعة الكفيل: شخص طبيعي (natural) أو شخص اعتباري (legal). */
  nature?: PartyNature;
  /** رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  registrationNumber?: string;
  /** من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  representedBy?: string;
}

/** وريث لمنفذ عليه متوفى (المقترض أو أحد الكفلاء). */
export interface HeirDto {
  id?: number;
  name?: string;
  father?: string;
  family?: string;
  /** صفة الوريث: أصالة / إضافة لتركة / أصالة وإضافة. */
  capacity?: string;
  addressType?: string;
  address?: string;
}

/**
 * مال مرهون ضمن قائمة الأموال المرهونة (منقول أو غير منقول). الملاك قائمة أسماء (واحد أو أكثر)
 * بترتيب الاختيار، وتُستخدم في النصوص والتوليد مجمعةً، ويُوجَّه توليد 005/006 لكل وريثٍ من الورثة.
 * الحقول غير المعنية بنوع الأصل تبقى فارغة.
 */
export interface AssetDto {
  id?: number;
  /** نوع الأصل: عقار / مركبة / متجر / كفالة رواتب / متجر غير مسجل. */
  assetKind?: string;
  owners?: string[];
  /** مقدار الحصة (تمام العقار/المركبة/المتجر أو حصة سهمية) — بلا قيمة لكفالة الرواتب والمتجر غير المسجل. */
  shareType?: string;
  // العقار
  property?: string;
  propertyNumber?: string;
  propertyDistrict?: string;
  landRegistry?: string;
  // المركبة
  vehicleType?: string;
  vehicleClass?: string;
  plateNumber?: string;
  vehicleGovernorate?: string;
  // المتجر المسجل
  registerNumber?: string;
  registrationDate?: string;
  shopGovernorate?: string;
  shopDescription?: string;
  shopLocation?: string;
  // كفالة الرواتب
  publicEntity?: string;
  // المتجر غير المسجل
  licenseNumber?: string;
  /** تاريخ القاء الحجز على الأصل — نص حر يُفسَّر زمنيًا بالخلفية. */
  seizureDate?: string;
  licenseDate?: string;
  licenseIssuer?: string;
  // الملاحظات (كفالة الرواتب والمتجر غير المسجل)
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

/** صفة الملف الثابتة (تُثبَّت عند الإنشاء ولا تتغير أثناء التعديل). */
export type GeneralEntitySide = 'applicant' | 'executed' | 'deposit';

/** طبيعة طرف الملف: شخص طبيعي أو شخص اعتباري. */
export type PartyNature = 'natural' | 'legal';

/** طبيعة المنفذ عليه في وضع «منفذ عليه»: جهة عامة أو شخص اعتباري. */
export type EntityNature = 'public' | 'legal';

/** وريث لمورثٍ متوفى في وضع «منفذ عليه» (اسم ثلاثي). */
export interface ExecutedHeirDto {
  id?: number;
  heirName?: string;
  heirFather?: string;
  heirFamily?: string;
  addressType?: string;
  heirAddress?: string;
}

/** طالب التنفيذ في وضع «منفذ عليه» مع ورثة مورثه المتوفى (إن اختير «إضافة لتركة» أو «أصالة وإضافة»). */
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
  /** طبيعة طالب التنفيذ/العرض: شخص طبيعي (natural) أو شخص اعتباري (legal). */
  nature?: PartyNature;
  /** رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  registrationNumber?: string;
  /** من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  representedBy?: string;
  /** نوع عنوان الشخص الاعتباري: موطن مختار / عنوان / وكيل قانوني. */
  addressType?: string;
  /** عنوان الشخص الاعتباري أو وكيله القانوني. */
  address?: string;
}

/** المنفذ عليه في وضع «منفذ عليه»: جهة عامة أو شخص اعتباري. */
export interface ExecutedPublicEntityDto {
  id?: number;
  entityName?: string;
  entityBranch?: string;
  /** المحافظة التي تتبع لها الجهة العامة أو الشخص الاعتباري (مثل: دمشق/اللاذقية). */
  governorate?: string;
  /** طبيعة المنفذ عليه: جهة عامة (public) أو شخص اعتباري (legal). */
  nature?: EntityNature;
  /** رقم تسجيل الشخص الاعتباري عند الطبيعة (legal). */
  registrationNumber?: string;
  /** من يمثل الشخص الاعتباري عند الطبيعة (legal). */
  representedBy?: string;
  /** نوع عنوان الشخص الاعتباري: موطن مختار / عنوان / وكيل قانوني. */
  addressType?: string;
  /** عنوان الشخص الاعتباري أو وكيله القانوني. */
  address?: string;
  /** معرّف قيد الجهة في السجل المرجعي (من نافذة الاختيار) — يُفكّ بحذف القيد. */
  registryId?: number | null;
}

/** الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (اسم الجهة + فرعها + محافظتها). */
export interface ApplicantPublicEntityDto {
  id?: number;
  name?: string;
  branch?: string;
  /** المحافظة التي تتبع لها الجهة (مثل: دمشق/اللاذقية) — تُملأ تلقائيًا من فرع المحامي وقابلة للتعديل. */
  governorate?: string;
  /** معرّف قيد الجهة في السجل المرجعي (من نافذة الاختيار) — يُفكّ بحذف القيد أو بتعديل نصي يدوي. */
  registryId?: number | null;
}

/** سجل تعاقب محامٍ على الملف: منشئ الملف (create) أو إحالة (transfer). */
export interface DocumentAssignmentDto {
  id: number;
  kind: 'create' | 'transfer';
  lawyerName?: string;
  assignedByName?: string;
  assignedAt?: string;
}

/** الشخص الطبيعي المنفذ عليه في وضع «منفذ عليه» مع ورثة مورثه المتوفى (إن اختير «إضافة لتركة» أو «أصالة وإضافة»). */
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
  borrowerRepresentativeName?: string;
  borrowerRepresentativeFather?: string;
  borrowerRepresentativeFamily?: string;
  borrowerRepresentativeCapacity?: string;
  borrowerRepresentativeAddressType?: string;
  borrowerRepresentativeAddress?: string;
  /** طبيعة المقترض/المنفذ عليه: شخص طبيعي (natural) أو شخص اعتباري (legal). */
  borrowerNature?: PartyNature;
  /** رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  borrowerRegistrationNumber?: string;
  /** من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  borrowerRepresentedBy?: string;
  contractType?: string;
  contractTypeSelector?: string;
  contractNumber?: string;
  contractDate?: string;
  /** ملحق العقد (للعقد المصرفي فقط، اختياري): نوعه ورقمه وتاريخه. */
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
  applicant?: string;
  lawyer?: string;
  /** اسم المحامي الذي أُحيل منه الملف إلى المحامي الحالي (فارغ إذا لم يُنقل). */
  referredFromLawyer?: string;
  /** لحظة إحالة الملف إلى المحامي الحالي. */
  referredAt?: string;
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
  /** حالة العرض الموحدة المشتقة من الخلفية (منفذ/تريث/تحت رفع/متداول/متداول / منفذ جزئيا/مشطوب). */
  displayStatus?: string;
  execStatus?: string;
  execSubStatus?: string;
  /** المبالغ المحصَّلة (حتى ثلاثة بعملاتها) في «منفذ بالتسوية»/«منفذ جبريا». */
  collectedAmount?: number;
  collectedAmount2?: number;
  collectedAmount3?: number;
  collectedCurrency?: string;
  collectedCurrency2?: string;
  collectedCurrency3?: string;
  baraetNumber?: string;
  baraetDate?: string;
  /** تاريخ قرار الإحالة القطعية في «منفذ جبريا» (نص حر). */
  forcedExecutionDate?: string;
  /** تاريخ تحويل بدل المبيع للجهة العامة عند «اعتبار الملف منفذًا كاملًا بهذا البيع». */
  forcibleTransferDate?: string;
  /** رقم الإشعار (اختياري) عند تحويل بدل المبيع للجهة العامة. */
  forcibleTransferNoticeNumber?: string;
  baraetRegNumber?: string;
  baraetRegDate?: string;
  tarithNumber?: string;
  tarithDate?: string;
  tarithRegNumber?: string;
  tarithRegDate?: string;
  /** حقول كتاب الجهة العامة بالسير بالملف عند التراجع (رقم/تاريخ الكتاب + وروده). */
  sayerNumber?: string;
  sayerDate?: string;
  sayerRegNumber?: string;
  sayerRegDate?: string;
  /** معرّفات الأموال المباعة بالمزاد العلني في «منفذ جبريا» (من أموال الملف، عدا كفالة الرواتب). */
  soldAssetIds?: number[];
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
  /** تاريخ ورود الاخطار في وضع «الجهة العامة منفذ عليها» (يغذي فترة إحصائية «متداول للضد»). */
  fileReceiptDate?: string;
  /** رقم ورود الإخطار التنفيذي في وضع «الجهة العامة منفذ عليها». */
  fileReceiptNumber?: string;
  /** المبلغ المطلوب دفعه من الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount?: number;
  /** عملة المبلغ المطلوب الأول في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedRequiredCurrency?: string;
  /** المبلغ المطلوب الثاني (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount2?: number;
  /** عملة المبلغ المطلوب الثاني في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedRequiredCurrency2?: string;
  /** المبلغ المطلوب الثالث (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount3?: number;
  /** عملة المبلغ المطلوب الثالث في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedRequiredCurrency3?: string;
  /** المبلغ الذي دفعته الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount?: number;
  /** عملة المبلغ المدفوع الأول في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedPaidCurrency?: string;
  /** المبلغ المدفوع الثاني (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount2?: number;
  /** عملة المبلغ المدفوع الثاني (افتراضيًا ليرة سورية). */
  executedPaidCurrency2?: string;
  /** المبلغ المدفوع الثالث (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount3?: number;
  /** عملة المبلغ المدفوع الثالث (افتراضيًا ليرة سورية). */
  executedPaidCurrency3?: string;
  /** تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع». */
  executedDepositDate?: string;
  /** تاريخ التنفيذ في وضع «الجهة العامة منفذ عليها». */
  executedExecutionDate?: string;
  /** لحظة شطب الملف (تاريخ إدخاله من النموذج وتبقى محفوظة بعد الإعادة). */
  struckOffDate?: string;
  /** رقم ورود اخطار التجديد عند إعادة ملف مشطوب إلى المتداول (اختياري). */
  renewalFileReceiptNumber?: string;
  /** تاريخ ورود اخطار التجديد عند إعادة الملف المشطوب (اختياري). */
  renewalFileReceiptDate?: string;
  /** رقم الملف الجديد عند إعادة الملف المشطوب (إلزامي) — يعود الملف به لسنة الإعادة. */
  renewalFileNumber?: string;
  /** نوع الملف الجديد عند إعادة الملف المشطوب (اختياري). */
  renewalFileType?: string;
  /** تاريخ التجديد عند إعادة الملف المشطوب (اختياري). */
  renewalDate?: string;
  guarantors: GuarantorDto[];
  assets: AssetDto[];
  borrowerHeirs?: HeirDto[];
  executionActions?: ExecutionActionDto[];
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  /** الجهات العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (واحدة أو أكثر). */
  applicantPublicEntities?: ApplicantPublicEntityDto[];
  /** سجل تعاقب المحامين على الملف (منشئ + كل المحامين المتعاقبين مع تواريخ الإحالة). */
  assignments?: DocumentAssignmentDto[];
  /** رقم ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (اختياري). */
  fileArrivalNumber?: string;
  /** تاريخ ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (نص حر). */
  fileArrivalDate?: string;
  /** «وقوعات الملف»: سجل زمني لكل شطب وتجديد في وضع «منفذ عليه»/«عرض وايداع» (مرتب تصاعديًا). */
  occurrences?: DocumentOccurrenceDto[];
  /** هل للملف استئناف واحد على الأقل — لشارة «استئناف» بجانب نتائج البحث. */
  hasAppeals?: boolean;
  /** معرف أول استئناف على الملف عند وجوده (للانتقال إلى تفاصيل الاستئناف). */
  matchedAppealId?: number;
}

/** أصل موضوع إنابة: وصف قراءة (النوع + وصفه) وبدل المبيع عند البيع (بالليرة السورية). */
export interface DelegationAssetDto {
  id: number;
  assetKind: string;
  assetLabel: string;
  salePrice?: number | null;
  /** عُدّلت بيانات الأصل في الملف المنيب بعد الإنابة فحُدِّثت اللقطة تلقائيًا. */
  snapshotAdjusted: boolean;
}

/** إنابة للعرض: بطاقة «تشعبات الملف» في المنيب و«معلومات الملف المنيب» في المناب. */
export interface DelegationDto {
  id: number;
  sourceDocumentId: number;
  sourceDocumentLabel?: string;
  /** رقم أساس الملف المنيب الحالي: رقم أساس سنة التدوير إن وُجد وإلا رقم ملفه الأصلي. */
  sourceFileNumber?: string | null;
  /** سنة الرقم المعروض للملف المنيب (سنة التدوير إن وُجدت وإلا سنة ملفه الأصلي). */
  sourceFileYear?: string | null;
  targetDocumentId?: number | null;
  delegatedCourt?: string;
  isExternal: boolean;
  externalBranchId?: number | null;
  externalBranchName?: string | null;
  /** تاريخ الإنابة (نص بصيغة yyyy-MM-dd من الخلفية). */
  delegationDate?: string;
  delegationText?: string;
  depositBookNumber?: string;
  depositBookDate?: string;
  assignedLawyerId?: number | null;
  assignedLawyerName?: string | null;
  /** تاريخ إعادة الملف إلى الدائرة المنيبة عند إتمام الإنابة (yyyy-MM-dd). */
  returnDate?: string;
  /** إحدى حالات DelegationStatusCatalog: بانتظار رئيس القسم/محالة/مسجلة أصولًا/منفذ إنابة. */
  status: string;
  createdAt: string;
  createdByName?: string;
  /** محامي الملف المنيب الذي سطّر الإنابة (صاحب صلاحية تعديلها/حذفها ما دامت معلّقة). */
  createdById: number;
  assets: DelegationAssetDto[];
  /** هل غطى بدل المبيع كامل المديونية؟ يحدده محامي المناب عند الإتمام — null قبل الإتمام. */
  saleCoversFullDebt?: boolean | null;
}

/** تسطير/تعديل إنابة: التواريخ نصوص حرة تُفسَّر في الخلفية؛ الخارجية تتطلب الفرع المناب. */
export interface UpsertDelegationRequest {
  delegatedCourt?: string | null;
  isExternal: boolean;
  externalBranchId?: number | null;
  delegationDate?: string | null;
  delegationText?: string | null;
  depositBookNumber?: string | null;
  depositBookDate?: string | null;
  /** معرفات أصول الملف المنيب موضوع الإنابة. */
  assetIds: number[];
}

/** اعتماد الإنابة: تعيين المحامي المختص من رئيس القسم (يُنشأ الملف المناب تلقائيًا). */
export interface AssignDelegationRequest {
  assignedLawyerId: number;
}

/** تسجيل الإنابة أصولًا من محامي الملف المناب: رقم أساس الإنابة وتاريخ قيدها. */
export interface RegisterDelegationRequest {
  fileNumber: string;
  fileYear: string;
  /** تاريخ قيد الإنابة (نص حر). */
  fileRegistrationDate: string;
}

/** بدل المبيع لأصل مباعٍ بالمزاد ضمن إتمام الإنابة (بالليرة السورية). */
export interface DelegationSaleDto {
  delegationAssetId: number;
  salePrice: number;
}

/** إتمام الإنابة من محامي الملف المناب: بيع الأموال موضوع الإنابة وتاريخ إعادة الملف. */
export interface CompleteDelegationRequest {
  /** تاريخ إعادة الملف إلى الدائرة المنيبة (نص حر). */
  returnDate: string;
  sales: DelegationSaleDto[];
  /** تاريخ قرار الإحالة القطعية (نص حر) — يُحفظ على الملف المنيب عند تفعيله «منفذ جبريا». */
  forcedExecutionDate: string;
  /** هل غطى بدل المبيع كامل المديونية؟ يحدده محامي المناب عند الإتمام. */
  saleCoversFullDebt?: boolean | null;
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
  /** طبيعة المقترض/المنفذ عليه: شخص طبيعي (natural) أو شخص اعتباري (legal). */
  borrowerNature?: PartyNature;
  /** رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  borrowerRegistrationNumber?: string;
  /** من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية. */
  borrowerRepresentedBy?: string;
  contractType?: string;
  contractTypeSelector?: string;
  contractNumber?: string;
  contractDate?: string;
  /** ملحق العقد (للعقد المصرفي فقط، اختياري): نوعه ورقمه وتاريخه. */
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
  applicant?: string;
  /** نسخة تسريع: معرّف قيد أول جهة طالب مرتبطة بالسجل — تُحدَّث عند الحفظ (للفلترة في البوابة). */
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
  /** صفة الملف: تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل. */
  generalEntitySide?: GeneralEntitySide;
  /** حالة وضع «الجهة العامة منفذ عليها»: متداول (فارغ) / منفذ / مشطوب. */
  executedStatus?: string;
  /** تاريخ الشطب (يُدخل عند اختيار «مشطوب»). */
  struckOffDate?: string;
  /** رقم ورود اخطار التجديد عند إعادة ملف مشطوب إلى المتداول (اختياري). */
  renewalFileReceiptNumber?: string;
  /** تاريخ ورود اخطار التجديد عند إعادة الملف المشطوب (اختياري). */
  renewalFileReceiptDate?: string;
  /** رقم الملف الجديد عند إعادة الملف المشطوب (إلزامي) — يعود الملف به لسنة الإعادة. */
  renewalFileNumber?: string;
  /** نوع الملف الجديد عند إعادة الملف المشطوب (اختياري). */
  renewalFileType?: string;
  /** سنة الإعادة (إلزامية في نظام «طالبة تنفيذ» عند إعادة ملف مشطوب). */
  renewalYear?: number;
  /** تاريخ التجديد عند إعادة الملف المشطوب (اختياري). */
  renewalDate?: string;
  /** وصف/بيان إضافي في وضع «الجهة العامة منفذ عليها». */
  executedDescription?: string;
  /** تاريخ ورود الاخطار في وضع «الجهة العامة منفذ عليها». */
  fileReceiptDate?: string;
  /** رقم ورود الإخطار التنفيذي في وضع «الجهة العامة منفذ عليها». */
  fileReceiptNumber?: string;
  /** المبلغ المطلوب دفعه من الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount?: number;
  /** عملة المبلغ المطلوب الأول في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedRequiredCurrency?: string;
  /** المبلغ المطلوب الثاني (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount2?: number;
  /** عملة المبلغ المطلوب الثاني في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedRequiredCurrency2?: string;
  /** المبلغ المطلوب الثالث (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedRequiredAmount3?: number;
  /** عملة المبلغ المطلوب الثالث في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedRequiredCurrency3?: string;
  /** المبلغ الذي دفعته الجهة العامة في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount?: number;
  /** عملة المبلغ المدفوع الأول في وضع «الجهة العامة منفذ عليها» (افتراضيًا ليرة سورية). */
  executedPaidCurrency?: string;
  /** المبلغ المدفوع الثاني (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount2?: number;
  /** عملة المبلغ المدفوع الثاني (افتراضيًا ليرة سورية). */
  executedPaidCurrency2?: string;
  /** المبلغ المدفوع الثالث (اختياري) في وضع «الجهة العامة منفذ عليها». */
  executedPaidAmount3?: number;
  /** عملة المبلغ المدفوع الثالث (افتراضيًا ليرة سورية). */
  executedPaidCurrency3?: string;
  /** تاريخ ايداعه حساب الجهة العامة في وضع «عرض وايداع». */
  executedDepositDate?: string;
  /** تاريخ التنفيذ في وضع «الجهة العامة منفذ عليها». */
  executedExecutionDate?: string;
  /** تاريخ قرار الإحالة القطعية في «منفذ جبريا» (نص حر). */
  forcedExecutionDate?: string;
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  /** الجهات العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (واحدة أو أكثر). */
  applicantPublicEntities?: ApplicantPublicEntityDto[];
  /** رقم ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (اختياري). */
  fileArrivalNumber?: string;
  /** تاريخ ورود الملف في وضع «الجهة العامة طالبة تنفيذ» (نص حر). */
  fileArrivalDate?: string;
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

/** مبلغ مجمّع بعملة محددة (ليرة سورية / دولار أمريكي / يورو)، بقيمة غير صفرية. */
export interface CurrencyAmountDto {
  currency: string;
  amount: number;
}

/**
 * توزيع حالة على نوعَي العقد (مصرفي/عادي) مع مبالغ كل نوع مجمّعة حسب العملة الفعلية،
 * بالترتيب الثابت: ليرة سورية، دولار أمريكي، يورو.
 */
export interface ManagerContractSplitDto {
  bankingCount: number;
  ordinaryCount: number;
  bankingAmounts: CurrencyAmountDto[];
  ordinaryAmounts: CurrencyAmountDto[];
}

export interface ManagerStatsDto {
  totalFiles: number;
  active: number;
  drafts: number;
  deferred: number;
  /** توزيع «متداول للصالح» (الدولة طالبة التنفيذ) مصرفي/عادي مع مبالغه بالعملات. */
  activeSplit: ManagerContractSplitDto;
  /** توزيع «تحت رفع» مصرفي/عادي مع مبالغه بالعملات. */
  draftsSplit: ManagerContractSplitDto;
  /** توزيع «التريث» مصرفي/عادي مع مبالغه بالعملات. */
  deferredSplit: ManagerContractSplitDto;
  /** إجمالي مبالغ ملفات «طالبة التنفيذ» (دون المنفذ) مجمّعة حسب العملة. */
  totalAmounts: CurrencyAmountDto[];
  /** المبالغ المطلوب دفعها من الجهات العامة في «متداول للضد» كلٌّ بعملتها (حتى ثلاثة). */
  tradingAgainstAmounts: CurrencyAmountDto[];
  settledCount: number;
  settledCollected: number;
  /** المبالغ المحصلة في «منفذ بالتسوية» مجمّعة حسب العملة الفعلية (كل عملة على حدة). */
  settledCollectedAmounts: CurrencyAmountDto[];
  forcibleCount: number;
  forcibleCollected: number;
  /** المبالغ المحصلة في «منفذ جبريا» مجمّعة حسب العملة الفعلية (كل عملة على حدة). */
  forcibleCollectedAmounts: CurrencyAmountDto[];
  /** عدد ملفات وضع «الجهة العامة منفذ عليها» المتداولة في الفترة (صف «متداول للضد»). */
  tradingAgainstCount: number;
  /** عدد ملفات وضع «الجهة العامة منفذ عليها» المنفذة في الفترة (بطاقة «منفذ للضد»). */
  executedAgainstCount: number;
  /** مجموع المبالغ التي دفعتها الجهات العامة في ملفات المنفذ (بطاقة «منفذ للضد»). */
  executedAgainstAmount: number;
  /** عدد ملفات «عرض وايداع» المتداولة في الفترة (سطر «عرض وايداع» داخل بطاقة «متداول للصالح»). */
  depositTradingCount: number;
  /** عدد ملفات «عرض وايداع» المنفذة في الفترة (سطر «عرض وايداع» داخل بطاقة «منفذ للصالح»). */
  depositExecutedCount: number;
  /** مجموع المبالغ المودعة لملفات «عرض وايداع» المنفذة في الفترة. */
  depositExecutedAmount: number;
  periodYear: number;
  periodQuarter: number | null;
  periodMonth: number | null;
  /** إحصاء استئنافات المحامي وفق فلاتر الفترة — null عند غياب أي استئناف (تُخفى البطاقة). */
  appeals?: AppealsStatsDto | null;
}

/** عدادات بطاقة «الاستئنافات» في لوحة المحامي. */
export interface AppealsStatsDto {
  pendingCount: number;
  decidedInFavor: number;
  decidedAgainst: number;
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

export type HeadAlertTargetType = 'document' | 'lawyer' | 'branch' | 'head';

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
  /** الاستئناف المرتبط بالتنبيه — للانتقال المباشر إلى تفاصيله. */
  appealId?: number;
  /** كتاب المطالعة المرتبط (تنبيه الرد) — للانتقال المباشر إلى صفحة الكتاب. */
  reviewLetterId?: number | null;
  createdAt: string;
  createdByName?: string;
}

/** تذكير إجراء على استئناف يتابعه المحامي (بطاقة التذكيرات). */
export interface AppealReminderDto {
  actionId: number;
  appealId: number;
  documentId: number;
  appealTitle: string;
  actionText: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  dueDate: string;
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
  /** اسم العرض الموحد — اسم المقترض، أو اسم طالب العرض لملفات العائلتين Executed + Deposit. */
  displayName?: string;
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

/** نوع وقعة الملف: شطب/تجديد (وضع «منفذ عليه») أو إجراء تغيير حالة (نظام «طالبة تنفيذ»). */
export type OccurrenceType = 'struck-off' | 'renewal' | 'deferred' | 'settled' | 'forcible' | 'revert' | 'entity-change';

/** وقعة واحدة من «وقوعات الملف»: شطب/تجديد أو إجراء تغيير حالة (تريث/منفذ/تراجع). */
export interface DocumentOccurrenceDto {
  id: number;
  occurrenceType: OccurrenceType;
  /** التسمية العربية للوقعة (شطب / تجديد / تريث / منفذ بالتسوية / منفذ جبريا / تراجع). */
  occurrenceTypeLabel: string;
  /** تاريخ الوقعة: تاريخ الشطب أو التجديد أو الإجراء. */
  eventDate?: string;
  /** الرقم المعني بالوقعة: الرقم القديم المُشطوب أو الرقم الجديد للتجديد. */
  fileNumber?: string;
  /** نوع الملف الجديد عند التجديد. */
  fileType?: string;
  /** سنة الوقعة: سنة الشطب أو سنة الإعادة للتجديد. */
  year?: number;
  /** رقم ورود اخطار التجديد عند التجديد. */
  receiptNumber?: string;
  /** تاريخ ورود اخطار التجديد عند التجديد. */
  receiptDate?: string;
  /** حقول إجراءات تغيير الحالة (مفاتيح الخدمة: tarith*، baraet*، sayer*، collectedAmount*، execSubStatus، soldAssetIds). */
  details?: Record<string, string>;
  /** اسم من أدخل الوقعة. */
  createdByName?: string;
}

/** إضافة/تعديل وقعة يدويًا (التواريخ نصوص حرة بصيغة «1/8/2026»). */
export interface UpsertOccurrenceRequest {
  occurrenceType: OccurrenceType;
  eventDate?: string;
  fileNumber?: string;
  fileType?: string;
  year?: number;
  receiptNumber?: string;
  receiptDate?: string;
  /** حقول إجراءات تغيير الحالة (نظام «طالبة تنفيذ»). */
  details?: Record<string, string>;
}

/* ── الاستئنافات على الملف التنفيذي ──────────────────────────────────── */

/** اتجاه الاستئناف: مستأنِفين (نحن) أو مستأنف علينا. */
export type AppealDirection = 'appellants' | 'against-us';

/** حالة الاستئناف: منظور (قيد النظر) / محسوم / مشطوب. */
export type AppealStatus = 'pending' | 'decided' | 'struck-off';

/** نتيجة الاستئناف المحسوم: للصالح / للضد. */
export type AppealOutcome = 'in-favor' | 'against';

/** طرف ضمن استئناف (لقطة وقت الإنشاء): نوعه المرجعي ومعرّفه واسمه المعروض. */
export interface AppealPartyDto {
  kind: string;
  partyId: number;
  name: string;
}

/** اختيار المستأنف من الواجهة (يُعاد بناء الاسم من الملف الأساس على الخادم). */
export interface AppealPartySelectionDto {
  kind: string;
  partyId: number;
}

/** استئناف كامل التفاصيل (التواريخ نصية بصيغة yyyy-MM-dd من الخلفية). */
export interface AppealDto {
  id: number;
  documentId: number;
  documentLabel?: string;
  fileNumber?: string;
  fileType?: string;
  fileYear?: string;
  court?: string;
  direction: AppealDirection;
  directionLabel: string;
  status: AppealStatus;
  statusLabel: string;
  appealTypeLabel?: string;
  appellants: AppealPartyDto[];
  appellees: AppealPartyDto[];
  appealedDecisionText?: string;
  appealedDecisionSummary?: string;
  appealedDecisionDate?: string;
  inspectionBookNumber?: string;
  inspectionBookDate?: string;
  groundsSummary?: string;
  noticeNumber?: string;
  noticeDate?: string;
  appellateCourt?: string;
  appealBaseNumber?: string;
  appealYear?: string;
  depositBookNumber?: string;
  depositBookDate?: string;
  defenseOpinion?: string;
  registrationDate?: string;
  decisionNumber?: string;
  decisionDate?: string;
  decisionRuling?: string;
  outcome?: AppealOutcome;
  outcomeLabel?: string;
  struckOffDate?: string;
  struckOffDecisionNumber?: string;
  notes?: string;
  /** يحتاج تدوير رقم أساسه لسنة التدوير الحالية (يظهر بالأحمر). */
  needsRotation: boolean;
  /** رقم الأساس المعروض: سجل السنة الحالية إن وُجد وإلا الرقم الأصلي المسجّل. */
  currentBaseNumber?: string;
  assignedLawyerId?: number;
  assignedLawyerName?: string;
  createdAt: string;
  createdByName?: string;
  createdById: number;
}

/** تسطير/تعديل استئناف قبل الإسناد (التواريخ نصوص حرة بصيغة «1/8/2026»). */
export interface UpsertAppealRequest {
  direction: AppealDirection;
  appellants?: AppealPartySelectionDto[];
  appealTypeLabel?: string;
  appealedDecisionText?: string;
  appealedDecisionSummary?: string;
  appealedDecisionDate?: string;
  inspectionBookNumber?: string;
  inspectionBookDate?: string;
  groundsSummary?: string;
  noticeNumber?: string;
  noticeDate?: string;
  appellateCourt?: string;
  appealBaseNumber?: string;
  appealYear?: string;
  depositBookNumber?: string;
  depositBookDate?: string;
  defenseOpinion?: string;
  notes?: string;
}

/** تحديث حقول القيد للاستئناف — المحامي المتابع. */
export interface UpdateAppealRegistrationRequest {
  appealTypeLabel?: string;
  appellateCourt?: string;
  appealBaseNumber?: string;
  appealYear?: string;
  registrationDate?: string;
}

/** حسم الاستئناف برقم قرار الحسم وتاريخه ومنطوقه ونتيجته. */
export interface DecideAppealRequest {
  decisionNumber: string;
  decisionDate: string;
  decisionRuling: string;
  outcome: AppealOutcome;
}

/** شطب الاستئناف بتاريخ الشطب ورقم قرار الشطب. */
export interface StrikeAppealRequest {
  struckOffDecisionNumber: string;
  struckOffDate: string;
}

/** إسناد الاستئناف إلى محامٍ للمتابعة — رئيس القسم. */
export interface AssignAppealRequest {
  assignedLawyerId: number;
}

/** نقل استئناف مفرد بين محامي الفرع — رئيس القسم. */
export interface TransferAppealRequest {
  targetLawyerId: number;
}

/** نقل كل استئنافات محامٍ إلى محامٍ آخر ضمن الفرع نفسه. */
export interface TransferAllAppealsRequest {
  sourceLawyerId: number;
  targetLawyerId: number;
}

/** إدخال رقم الأساس الاستئنافي لسنة التدوير الحالية. */
export interface AppealBaseNumberEntry {
  baseNumber?: string;
}

export interface SaveAppealBaseNumbersRequest {
  entries: AppealBaseNumberEntry[];
}

/** سجل رقم أساس استئنافي لسنة سابقة أو حالية. */
export interface AppealBaseNumberHistoryDto {
  year: number;
  baseNumber: string;
}

/** إجراء/ملاحظة على الاستئناف مع تذكيره الاختياري (مدة + لون). */
export interface AddAppealActionRequest {
  type?: string;
  text: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
}

export interface UpdateAppealActionRequest extends AddAppealActionRequest {}

export interface AppealActionDto {
  id: number;
  type: string;
  text: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  createdByName?: string;
  createdAt: string;
}

/* ── كتب المطالعة ─────────────────────────────────────────────────────── */

export type ReviewLetterMessageKind = 'letter' | 'addendum' | 'reply';

/** سياق الملف المرتبط بصيغة العرض: مطالعة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ.. */
export interface ReviewLetterFileContext {
  executedName: string;
  fileNumber?: string | null;
  fileType?: string | null;
  fileYear?: string | null;
  court?: string | null;
}

/** رسالة واحدة ضمن كتاب المطالعة (الأصل letter أو لاحق addendum أو رد reply). */
export interface ReviewLetterMessageDto {
  id: number;
  kind: ReviewLetterMessageKind;
  bodyHtml: string;
  messageNumber: string;
  messageDate: string;
  authorId: number;
  authorName: string;
  authorRole: 'lawyer' | 'head';
}

/** سطر كتاب في القائمة؛ fileContext فارغ للكتاب العام غير المرتبط بملف. */
export interface ReviewLetterListItemDto {
  id: number;
  letterNumber: string;
  letterDate: string;
  isAnswered: boolean;
  documentId?: number | null;
  fileContext?: ReviewLetterFileContext | null;
  lawyerName: string;
  snippet: string;
  lastKind: ReviewLetterMessageKind;
  /** فيه ردّ رئيس قسم لم يطّلع عليه محامي الكتاب بعد. */
  hasUnseenReply: boolean;
  messagesCount: number;
  updatedAt: string;
}

export interface ReviewLetterDto {
  id: number;
  letterNumber: string;
  letterDate: string;
  isAnswered: boolean;
  documentId?: number | null;
  fileContext?: ReviewLetterFileContext | null;
  branchId: number;
  lawyerName: string;
  /** فيه ردّ لم يُطَّلع بعد — يُعلَّم مقروءًا تلقائيًا عند فتح المحامي للصفحة. */
  hasUnseenReply?: boolean;
  messages: ReviewLetterMessageDto[];
  createdAt: string;
}

export interface CreateReviewLetterRequest {
  documentId?: number | null;
  bodyHtml: string;
}

export interface AddReviewLetterAddendumRequest {
  bodyHtml: string;
}

export interface ReplyReviewLetterRequest {
  bodyHtml: string;
}

/* ── سجل تعديلات الملف على مستوى الحقول ───────────────────────────────── */

/** تغيّر حقل واحد: القيمة قبل التعديل وبعده بتسمية عربية مجمّدة. */
export interface DocumentFieldChangeDto {
  fieldLabel: string;
  fieldKey: string;
  oldValue?: string | null;
  newValue?: string | null;
}

/** مجموعة تعديلات واحدة (إدخال تدقيق) بكل حقولها المتغيرة. */
export interface DocumentChangeGroupDto {
  auditLogId: number;
  actionType: string;
  userName?: string | null;
  timestamp: string;
  changes: DocumentFieldChangeDto[];
}

/* ── السجل المرجعي للجهات العامة (بوابة الجهات — المرحلة 1) ───────────── */

/** نوع الجهة (كتالوج الخمسة المعتمد). */
export type PublicEntityType =
  | 'ministry'
  | 'administration'
  | 'authority'
  | 'foundation'
  | 'company';

/** حالة قيد الجهة: نهائي (يظهر للمندوبين) أو بانتظار المراجعة. */
export type PublicEntityStatus = 'final' | 'pending';

/** صيغة مناداة ممثل الجهة القانونية: إضافة لوظيفته / إضافة لمنصبه. */
export type CitationFormula = 'add-to-job' | 'add-to-position';

/** قيد جهة في السجل بمستوى محافظة + فرع. */
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
  /** أدخلها محامٍ وهي بانتظار مراجعة رئيس القسم (نموذج الحوكمة الجديد). */
  needsReview?: boolean;
  /** تسمية التغطية الجغرافية (تظهر بدل المحافظة في البطاقات والبحث). */
  coverageLabel?: string | null;
  /** قيد «الجهة الأم» (بلا فرع): يغطي كل المحافظات ويظهر مرة واحدة — وفروعه تحته. */
  isParentEntity?: boolean;
}

/** نتيجة قائمة/بحث السجل المصدّرة. */
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
  /** جعل القيد قيد «الجهة الأم» (بلا فرع) — يُخزَّن مرة واحدة ويغطي كل المحافظات. */
  isParentEntity?: boolean;
}

/** أي حقل يُترك undefined يبقى كما هو؛ canonicalName يعني إعادة تسمية جماعية. حقول المرسوم للتعديلات العامة بمرسوم. */
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

/** كتابة متمايزة واحدة لنص جهة في الاستيراد مع عدّاد ملفاتها. */
export interface ImportVariantDto {
  text: string;
  /** مصدر الكتابة: applicant = طالب تنفيذ، executed = منفذ عليها. */
  side: 'applicant' | 'executed';
  governorate?: string | null;
  documentCount: number;
}

/** مرشّح استيراد يجمع كتابات متطابقة بعد التطبيع تحت اسم مقترح. */
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

/* ── النقل (MoveEntry) ──────────────────────────────────────────────── */

/** طلب نقل قيد جهة من هوية إلى أخرى أو طيّه في قيد قائم. */
export interface MoveEntryRequest {
  targetGroupId?: number | null;
  targetEntryId?: number | null;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  note?: string | null;
}

/** طلب نقل جميع قيود مجموعة إلى مجموعة أخرى (تبعية كاملة). */
export interface MoveAllEntriesRequest {
  sourceGroupId: number;
  targetGroupId: number;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  note?: string | null;
}

/** نتيجة نقل قيد واحد. */
export interface MoveEntryResponse {
  entryId: number;
  fromGroupId: number;
  toGroupId: number;
  affectedDocuments: number;
  changeEventId: number;
}

/** نتيجة نقل جميع قيود مجموعة. */
export interface MoveAllEntriesResponse {
  sourceGroupId: number;
  targetGroupId: number;
  entriesMoved: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* ── الدمج N←1 (د5 §4) ─────────────────────────────────────────── */

/** طلب معاينة الدمج قبل الاعتماد. */
export interface MergePreviewRequest {
  survivorGroupId: number;
  absorbedGroupIds: number[];
}

/** قيد مُهمَل في المعاينة مع مسار امتصاصه. */
export interface AbsorbedEntryPreviewDto {
  entryId: number;
  governorate: string;
  branchName: string;
  documentCount: number;
  mappedToEntryId: number;
  conflictsWithSurvivor: boolean;
}

/** هوية أم مُهمَلة في المعاينة. */
export interface AbsorbedGroupPreviewDto {
  groupId: number;
  name: string;
  entries: AbsorbedEntryPreviewDto[];
  totalDocuments: number;
  aliases: string[];
}

/** نتيجة معاينة الدمج. */
export interface MergePreviewResponse {
  survivorName: string;
  absorbedGroups: AbsorbedGroupPreviewDto[];
  totalAffectedDocuments: number;
  warnings: string[];
}

/** طلب اعتماد الدمج. */
export interface MergeCommitRequest {
  survivorGroupId: number;
  absorbedGroupIds: number[];
  unifyTexts?: boolean;
}

/** نتيجة الدمج. */
export interface MergeCommitResponse {
  absorbedGroupsCount: number;
  entriesMigrated: number;
  aliasesAdded: number;
  totalAffectedDocuments: number;
  changeEventId: number;
}

/* ── قائمة المجموعات (الهويات الأم) وتوحيد التسمية N←1 ───────────────── */

export interface PublicEntityGroupDto {
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  isActive: boolean;
  entryCount: number;
  governorates: string[];
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

export interface UnifyPreviewResponse {
  targetName: string;
  absorbedGroups: AbsorbedGroupUnifyPreviewDto[];
  totalEntriesToMove: number;
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
  changeEventId: number;
}

/* ── سجل تغييرات الجهات (د5 §7) ───────────────────────────────────── */

export interface EntityChangeEventDto {
  id: number;
  entryId?: number | null;
  groupId?: number | null;
  actionKind: string;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  payloadJson: string;
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

/* ── بوابة مندوب الجهة العامة (المرحلة 3) ────────────────────────────── */

/** نطاق المندوب: هوية أم بكل قيودها أو قيد بعينه. */
export interface PortalScopeDto {
  scopeType: 'group' | 'entry';
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  entries: PortalScopeEntryDto[];
}

export interface PortalScopeEntryDto {
  id: number;
  governorate: string;
  branchName: string;
  isActive: boolean;
}

/** ملف في قائمة البوابة — قراءة فقط بلا حقول داخلية للمحامين. */
export interface PortalFileListItemDto {
  id: number;
  documentType: string;
  isDraft: boolean;
  borrowerName?: string | null;
  applicant?: string | null;
  executedEntitiesSummary: string;
  amountNumeric: number;
  currency?: string | null;
  execStatus?: string | null;
  createdAt: string;
  updatedAt: string;
}

export type PortalFilesResponse = PagedResult<PortalFileListItemDto>;

/** استئناف قرائي على بطاقة استئنافات البوابة. */
export interface PortalAppealDto {
  id: number;
  direction: string;
  status: string;
  appealTypeLabel?: string | null;
  appealBaseNumber?: string | null;
  appealYear?: string | null;
  createdAt: string;
  decisionDate?: string | null;
  decisionRuling?: string | null;
}

export interface CreateDelegateRequest {
  username: string;
  fullName: string;
  password: string;
  portalGroupId?: number | null;
  portalEntryId?: number | null;
}

export interface UpdateDelegateRequest {
  fullName?: string | null;
  isActive?: boolean | null;
  newPassword?: string | null;
  portalGroupId?: number | null;
  portalEntryId?: number | null;
}

export interface DelegateDto {
  id: number;
  username: string;
  fullName: string;
  isActive: boolean;
  portalGroupId?: number | null;
  portalGroupName?: string | null;
  portalEntryId?: number | null;
  portalEntryLabel?: string | null;
  createdAt: string;
}

/* ── إحصاءات الجهة (المرحلة 4) ───────────────────────────────────────── */

export interface PortalMonthlyCountDto {
  year: number;
  month: number;
  files: number;
}

/** توزيع الارتباط على قيود النطاق؛ قد يُحتسب الملف تحت أكثر من قيد. */
export interface PortalEntryStatDto {
  entryId: number;
  governorate: string;
  branchName: string;
  files: number;
}

export interface PortalCurrencyStatDto {
  currency: string;
  files: number;
  totalAmount: number;
}

export interface PortalStatsDto {
  totalFiles: number;
  draftFiles: number;
  circulatingFiles: number;
  executedFiles: number;
  deferredFiles: number;
  pendingAppeals: number;
  closedAppeals: number;
  /** آخر 12 شهرًا متصلة حتى الشهر الحالي شاملة الأشهر الصفرية. */
  monthly: PortalMonthlyCountDto[];
  perEntry: PortalEntryStatDto[];
  topCurrencies: PortalCurrencyStatDto[];
}



