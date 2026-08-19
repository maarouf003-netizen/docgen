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
}

/** الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (اسم الجهة + فرعها + محافظتها). */
export interface ApplicantPublicEntityDto {
  id?: number;
  name?: string;
  branch?: string;
  /** المحافظة التي تتبع لها الجهة (مثل: دمشق/اللاذقية) — تُملأ تلقائيًا من فرع المحامي وقابلة للتعديل. */
  governorate?: string;
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
}

/** أصل موضوع إنابة: وصف قراءة (النوع + وصفه) وبدل المبيع عند البيع (بالليرة السورية). */
export interface DelegationAssetDto {
  id: number;
  assetKind: string;
  assetLabel: string;
  salePrice?: number | null;
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
  sendBookNumber?: string;
  sendBookDate?: string;
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
  sendBookNumber?: string | null;
  sendBookDate?: string | null;
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
export type OccurrenceType = 'struck-off' | 'renewal' | 'deferred' | 'settled' | 'forcible' | 'revert';

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

