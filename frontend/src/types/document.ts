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
  /** معرّف قيد طالب التنفيذ الاعتباري (الجهة العامة) في السجل المرجعي (اختياري). */
  registryId?: number | null;
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
  /** معرّف قيد الجهة في السجل المرجعي (من نافذة الاختيار) — يُفكّ بزر «فك الربط». */
  registryId?: number | null;
}

/** الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ» (اسم الجهة + فرعها + محافظتها). */
export interface ApplicantPublicEntityDto {
  id?: number;
  name?: string;
  branch?: string;
  /** المحافظة التي تتبع لها الجهة (مثل: دمشق/اللاذقية) — تُملأ تلقائيًا من فرع المحامي وقابلة للتعديل. */
  governorate?: string;
  /** معرّف قيد الجهة في السجل المرجعي (من نافذة الاختيار) — يُفكّ حصرًا بزر «فك الربط». */
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
  /** عدّاد التزامن المتفائل (RF-010) — يُعاد إرساله في `version` عند الحفظ. */
  version: number;
  createdById?: number;
  branchId?: number;
documentType?: string;
  isDraft: boolean;
  /** الملف منابٌ من ملف آخر (إنابة سطرها محامي الملف المنيب): الحقول الجوهرية مقفلة ومُزامنة. */
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
  /** ملاحظات السند التنفيذي (القرارات الأخرى) تحت «المتضمن» — نص حر اختياري. */
  inclusionNotes?: string;
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
  /** دائرة التنفيذ المرجعية (سجل الدوائر) — تُعرض نصًا عبر court للتوافق. */
  executionCircuitId?: number | null;
  /** علم «بانتظار إعادة القيد» — بانتظار المحامي لإدخال الرقم الجديد. */
  needsRegistration?: boolean;
  applicant?: string;
  lawyer?: string;
  /** اسم المحامي الذي أُحيل منه الملف إلى المحامي الحالي (فارغ إذا لم يُنقل). */
  referredFromLawyer?: string;
  /** لحظة إحالة الملف إلى المحامي الحالي. */
  referredAt?: string;
fileNumber?: string;
  /** الرقم الظاهر: آخر رقم أساس ≤ سنة قيد الملف إن وُجد، وإلا رقم الملف الأصلي. */
  displayFileNumber?: string;
  /** سنة الرقم الفعّال المرافقة لـ displayFileNumber. */
  displayFileYear?: string;
  fileType?: string;
  fileYear?: string;
  fileIncoming?: string;
  fileIncomingDate?: string;
  underFilingNumber?: string;
  fileRegistrationDate?: string;
  branchName?: string;
  administrativeBranchName?: string;
  /** حالة العرض الموحدة المشتقة من الخلفية (منفذ/تريث/تحت رفع/متداول/متداول / منفذ جزئيا/مشطوب/محال الى البداية). */
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
  /** رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها في «محال الى البداية». */
  noFundsDemandNumber?: string;
  /** تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها (يُعرض بصيغة yyyy-MM-dd). */
  noFundsDemandDate?: string;
  /** رقم كتاب الإحالة في «محال الى البداية». */
  startReferralNumber?: string;
  /** تاريخ كتاب الإحالة (يُعرض بصيغة yyyy-MM-dd). */
  startReferralDate?: string;
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
  /** هل للملف استئناف منظور واحد على الأقل — لشارة «استئناف» بجانب نتائج البحث (المحسوم والمشطوب لا يُظهران الشارة). */
  hasAppeals?: boolean;
  /** معرف أول استئناف منظور على الملف عند وجوده (للانتقال إلى تفاصيل الاستئناف). */
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
  /** ملاحظات السند التنفيذي (القرارات الأخرى) تحت «المتضمن» — نص حر اختياري. */
  inclusionNotes?: string;
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
  /** دائرة التنفيذ (سجل الدوائر): إلزامية — الخادم يشتق court منها ويتجاهل النص دائمًا. */
  executionCircuitId?: number | null;
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
  /** عدّاد التزامن المتفائل (RF-010) كما قُرئ عند الجلب — غيابه = قبول بلا فحص مبكر. */
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
/** اسم العرض الموحد — اسم المقترض، أو اسم طالب العرض لملفات العائلتين Executed + Deposit. */
  displayName?: string;
  /** الرقم الفعّال المعروض في صفحة التدوير (المحلل المركزي: آخر رقم أساس ≤ سنة قيد الملف، وإلا رقم الملف). */
  effectiveFileNumber?: string;
  /** سنة الرقم الفعّال المعروض في صفحة التدوير. */
  effectiveFileYear?: string;
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

/**
 * نوع وقعة الملف: شطب/تجديد (وضع «منفذ عليه») أو إجراء تغيير حالة (نظام «طالبة تنفيذ»)
 * أو تغيير جهة آلي أو نقل دوائر (`circuit-*` ولإبلاغ الطرف المقابل `*-file-moved`).
 */
export type OccurrenceType = 'struck-off' | 'renewal' | 'deferred' | 'settled' | 'forcible' | 'revert' | 'recovered' | 'entity-change' | 'referred-to-start' | 'circuit-referred' | 'circuit-reregistered' | 'circuit-renamed' | 'source-file-moved' | 'mirror-file-moved';

/** وقعة واحدة من «وقوعات الملف»: شطب/تجديد أو إجراء تغيير حالة (تريث/منفذ/تراجع) أو تغيير جهة آلي. */
export interface DocumentOccurrenceDto {
  id: number;
  occurrenceType: OccurrenceType;
  /** التسمية العربية للوقعة (شطب / تجديد / تريث / منفذ بالتسوية / منفذ جبريا / تراجع / تغيير جهة). */
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
  /** السرد النصي الحر للوقعة الآلية (نوع «تغيير جهة»): نص الحدث مع مرجعه — يُعرض بدل القاموس. */
  detailsText?: string | null;
/** اسم من أدخل الوقعة. */
  createdByName?: string;
  /** مصدر الوقعة: "system" (سجّلها النظام آليًا — لا تُعدَّل/تُحذف من الواجهة) أو "manual" (إدخال يدوي). */
  source?: 'system' | 'manual';
  /** اسم دائرة المصدر نصًا مجمدًا للتاريخ (يُملأ عند الإحالة). */
  fromCircuitName?: string | null;
  /** اسم دائرة الوجهة نصًا مجمدًا للتاريخ (يُملأ عند الإحالة). */
  toCircuitName?: string | null;
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
