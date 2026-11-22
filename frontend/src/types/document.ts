import type { components } from '../api/generated';

/**
 * الكفيل: العقود المولَّدة من `OpenAPI`، مع تضييق `nature` إلى الاتحاد المعروف
 * (المولَّد يصدّره `string | null`) و`heirs` إلى `HeirDto` (P6-06).
 */
export type GuarantorDto = Omit<components['schemas']['GuarantorDto'], 'nature' | 'heirs'> & {
  nature?: PartyNature | null;
  heirs?: HeirDto[] | null;
};

/** وريث لمنفذ عليه متوفى (المقترض أو أحد الكفلاء). نوعه مولَّد من OpenAPI (P6-06). */
export type HeirDto = components['schemas']['HeirDto'];

/**
 * مال مرهون ضمن قائمة الأموال المرهونة (منقول أو غير منقول). الملاك قائمة أسماء (واحد أو أكثر)
 * بترتيب الاختيار، وتُستخدم في النصوص والتوليد مجمعةً، ويُوجَّه توليد 005/006 لكل وريثٍ من الورثة.
 * الحقول غير المعنية بنوع الأصل تبقى فارغة.
 *
 * نوعه مولَّد من OpenAPI (P6-06) — `owners` قائمة أسماء أو `null` (كما في `AssetDto.Owners`
 * الخلفي)، و`assetKind` نص حر يُطبَّع في الخدمة. تفاصيل كل حقل موثّقة في
 * `backend/src/DocGenerator.Application/DTOs/DocumentPartyDtos.cs`.
 */
export type AssetDto = components['schemas']['AssetDto'];

/** إجراء تنفيذي/ملاحظة — نوعه مولَّد من OpenAPI (P6-06). */
export type ExecutionActionDto = components['schemas']['ExecutionActionDto'];

/** صفة الملف الثابتة (تُثبَّت عند الإنشاء ولا تتغير أثناء التعديل). */
export type GeneralEntitySide = 'applicant' | 'executed' | 'deposit';

/** طبيعة طرف الملف: شخص طبيعي أو شخص اعتباري. */
export type PartyNature = 'natural' | 'legal';

/** طبيعة المنفذ عليه في وضع «منفذ عليه»: جهة عامة أو شخص اعتباري. */
export type EntityNature = 'public' | 'legal';

/** وريث لمورثٍ متوفى في وضع «منفذ عليه» (اسم ثلاثي). نوعه مولَّد من OpenAPI (P6-06). */
export type ExecutedHeirDto = components['schemas']['ExecutedHeirDto'];

/**
 * طالب التنفيذ في وضع «منفذ عليه» مع ورثة مورثه المتوفى (إن اختير «إضافة لتركة» أو «أصالة وإضافة»).
 * حقوله مولَّدة من OpenAPI (P6-06)، مع تضييق `nature` إلى الاتحاد المعروف
 * (المولَّد يصدّره `string | null`).
 */
export type ExecutionApplicantDto = Omit<
  components['schemas']['ExecutionApplicantDto'],
  'nature'
> & {
  nature?: PartyNature | null;
};

/** المنفذ عليه في وضع «منفذ عليه»: جهة عامة أو شخص اعتباري. نوعه مولَّد مع تضييق `nature` (P6-06). */
export type ExecutedPublicEntityDto = Omit<components['schemas']['ExecutedPublicEntityDto'], 'nature'> & {
  nature?: EntityNature | null;
};

/** الجهة العامة طالبة التنفيذ في وضع «الجهة العامة طالبة تنفيذ». نوعها مولَّد من OpenAPI (P6-06). */
export type ApplicantPublicEntityDto = components['schemas']['ApplicantPublicEntityDto'];

/** صفة المتدخل في الملف: شخص طبيعي / شخص اعتباري / جهة عامة. */
export type InterventionNature = 'natural' | 'legal' | 'public';

/**
 * متدخل في الملف (شخص طبيعي/اعتباري/جهة عامة) مع بيانات طلب التدخل.
 * نوعه مولَّد من OpenAPI (P6-06) مع تضييق `nature` إلى الاتحاد المعروف وإبقاء
 * `heirs`/`version` اختياريين للتوافق مع الحمولات القائمة.
 */
export type DocumentInterventionDto = Omit<
  components['schemas']['DocumentInterventionDto'],
  'nature' | 'heirs' | 'version'
> & {
  nature: InterventionNature;
  heirs?: HeirDto[];
  version?: number;
};

/**
 * طلب إضافة/تعديل متدخل — حقوله مولَّدة من OpenAPI (P6-06)، مع تضييق `nature` إلى
 * الاتحاد المعروف. `heirs` يبقى اختياريًا واجهةً (الخلفية تقبل غيابه وتعتبره فارغًا)،
 * والتواريخ نصوص حرة تُفسَّر خادميًا.
 */
export type DocumentInterventionUpsertRequest = Omit<
  components['schemas']['DocumentInterventionUpsertRequest'],
  'nature' | 'heirs'
> & {
  nature: InterventionNature;
  heirs?: HeirDto[];
};

/** سجل تعاقب محامٍ على الملف: منشئ الملف (create) أو إحالة (transfer) — مولَّد مع تضييق `kind` (P6-06). */
export type DocumentAssignmentDto = Omit<
  components['schemas']['DocumentAssignmentDto'],
  'kind'
> & {
  kind: 'create' | 'transfer';
};

/** الشخص الطبيعي المنفذ عليه في وضع «منفذ عليه» مع ورثة مورثه المتوفى (إن اختير «إضافة لتركة» أو «أصالة وإضافة»). نوعه مولَّد من OpenAPI (P6-06). */
export type ExecutedNaturalPersonDto = components['schemas']['ExecutedNaturalPersonDto'];

/** إجراء/ملاحظة تُزرع مع إنشاء المستند — نوعه مولَّد من OpenAPI (P6-06). */
export type InitialActionRequest = components['schemas']['AddExecutionActionRequest'];

/** ملحق السند التنفيذي — صف واحد بترتيبه (Sequence يبدأ من 1). نوعه مولَّد من OpenAPI (P6-06). */
export type DocumentAnnexDto = components['schemas']['DocumentAnnexDto'];

/** مصدر تاريخ «آخر نشاط» المحسوب خلفيًا لبطاقة تذكيرات الشطب (يُعرض صراحةً عند عدم كونه إجراءً). */
export type LastActivitySource = 'action' | 'registration' | 'created';

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
  /** تاريخ إعادة الملف للدائرة المنيبة من صف الإنابة المصدر (yyyy-MM-dd) — للمناب المنفذ. */
  delegationReturnDate?: string | null;
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
  /** ملحق العقد (للعقد المصرفي فقط، اختياري): نوعه ورقمه وتاريخه — أول ملحق للتوافق. */
  annexType?: string;
  annexNumber?: string;
  annexDate?: string;
  /** ملاحق السند التنفيذي (المصدر الحقيقي — حتى 5). */
  annexes?: DocumentAnnexDto[];
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
  /**
   * آخر نشاط للملف (بطاقة «تذكيرات الشطب»): أحدث إجراء مسجّل (الملاحظات لا تُحتسب)،
   * وإلا تاريخ القيد، وإلا تاريخ الإدخال.
   */
  lastActivityDate?: string;
  /** مصدر آخر نشاط: action | registration | created (يُعلن بصدق في العرض). */
  lastActivitySource?: LastActivitySource;
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
  /** المتدخلون في الملف (شخص طبيعي/اعتباري/جهة عامة) مع بيانات طلب التدخل. */
  interventions?: DocumentInterventionDto[];
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

export type DocumentUpsertRequest = Omit<
  components['schemas']['DocumentUpsertRequest'],
  'borrowerNature' | 'generalEntitySide' | 'initialActions'
> & {
  /** طبيعة المقترض/المنفذ عليه: شخص طبيعي (natural) أو شخص اعتباري (legal). */
  borrowerNature?: PartyNature;
  /** صفة الملف: تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل. */
  generalEntitySide?: GeneralEntitySide;
  /** إجراءات/ملاحظات تُزرع مع الإنشاء فقط — اختيارية واجهةً (الخلفية تقبل غيابها). */
  initialActions?: InitialActionRequest[];
};

/**
 * عنصر قائمة الملفات (P3-04b): حقول البطاقة + ملخصات الأطراف + آخر إجراء.
 * نوعه مولَّد من OpenAPI، مع إبقاء القوائم اختيارية و`generalEntitySide` اتحادًا
 * مغلقًا؛ و`DocumentResponse` يبقى متوافقًا بنيويًا (يُمرَّر للتفاصيل).
 */
export type DocumentListItemDto = Omit<
  components['schemas']['DocumentListItemDto'],
  | 'generalEntitySide'
  | 'isDraft'
  | 'hasAppeals'
  | 'needsRotation'
  | 'executionActions'
  | 'executedNaturalPersons'
  | 'executedPublicEntities'
  | 'executionApplicants'
  | 'applicantPublicEntities'
> & {
  generalEntitySide?: GeneralEntitySide | null;
  isDraft?: boolean;
  hasAppeals?: boolean;
  needsRotation?: boolean;
  executionActions?: ExecutionActionDto[];
  executedNaturalPersons?: ExecutedNaturalPersonDto[];
  executedPublicEntities?: ExecutedPublicEntityDto[];
  executionApplicants?: ExecutionApplicantDto[];
  applicantPublicEntities?: ApplicantPublicEntityDto[];
};

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

/** مدخل رقم أساس في طلب الحفظ — نوعه مولَّد من OpenAPI (P6-06). */
export type BaseNumberEntry = components['schemas']['BaseNumberEntry'];

/** طلب حفظ أرقام الأساس — نوعه مولَّد من OpenAPI (P6-06). */
export type SaveBaseNumbersRequest = components['schemas']['SaveBaseNumbersRequest'];

/** سجل رقم أساس واحد: السنة والرقم والدائرة والمصدر وحالة الإلغاء والفعّالية. */
export interface BaseNumberHistoryDto {
  year: number;
  baseNumber: string;
  id?: number;
  source?: string | null;
  circuitId?: number | null;
  circuitName?: string | null;
  isVoided?: boolean;
  voidReason?: string | null;
  isEffective?: boolean;
}

/** طلب تصحيح رقم أساس سنة محددة (rowId اختياري = الصف الفعّال) — مولَّد مع تضييق `baseNumber` (P6-06). */
export type CorrectBaseNumberRequest = Omit<
  components['schemas']['CorrectBaseNumberRequest'],
  'baseNumber'
> & {
  baseNumber: string;
};

/**
 * نوع وقعة الملف: شطب/تجديد (وضع «منفذ عليه») أو إجراء تغيير حالة (نظام «طالبة تنفيذ»)
 * أو تغيير جهة آلي أو نقل دوائر (`circuit-*` ولإبلاغ الطرف المقابل `*-file-moved`).
 */
export type OccurrenceType = 'struck-off' | 'renewal' | 'deferred' | 'settled' | 'forcible' | 'revert' | 'recovered' | 'entity-change' | 'referred-to-start' | 'intervention' | 'circuit-referred' | 'circuit-reregistered' | 'circuit-renamed' | 'source-file-moved' | 'mirror-file-moved';

/** وقعة واحدة من «وقوعات الملف»: شطب/تجديد أو إجراء تغيير حالة (تريث/منفذ/تراجع) أو تغيير جهة آلي. */
/**
 * وقعة واحدة من «وقوعات الملف» — نوعها مولَّد من OpenAPI (P6-06) مع تضييق
 * `occurrenceType` إلى الاتحاد المعروف و`source` إلى `system|manual`.
 */
export type DocumentOccurrenceDto = Omit<
  components['schemas']['DocumentOccurrenceDto'],
  'occurrenceType' | 'source'
> & {
  occurrenceType: OccurrenceType;
  source?: 'system' | 'manual' | null;
};

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
