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
  /** الدائرة المنابة المرجعية (للداخلية فقط — الخارجية نص حر). */
  delegatedCircuitId?: number | null;
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
  /** رقم أساس الملف المناب الحالي. */
  targetFileNumber?: string | null;
  /** سنة الرقم المعروض للملف المناب. */
  targetFileYear?: string | null;
  /** نوع الملف المنيب (متداول/حقوق...) — يُعرض بجانب رقم أساسه في بطاقة «معلومات الملف المنيب». */
  sourceFileType?: string | null;
  /** دائرة التنفيذ المختصة بالملف المنيب (الدائرة المنيبة) — تُعرض في بطاقة «معلومات الملف المنيب» بدل الدائرة المنابة، كما هي مخزنة بلا بادئات. */
  sourceCourt?: string | null;
  /** حالة الملف المناب (ExecStatus) لشارة حالة المناب في «تشعبات الملف» — مسترد/تريث/منفذ إنابة. */
  targetExecStatus?: string | null;
  /**
   * هل تحجب هذه الإنابة أموالها عن أي تسطير جديد؟ (محسوب خلفيًا: السارية بمناب غير نهائي؛
   * المعلّقة بلا مناب حاجبة دائمًا) — يُستخدم لترشيح قائمة الأموال في نافذة التسطير.
   */
  blocksAssets?: boolean;
  /**
   * هل بلغ الملف المناب حالة نهائية (مشطوب/مسترد/منفذ إنابة)؟ — محسوب خلفيًا (مرآة
   * IsTargetTerminal) لإخفاء سطر «بانتظار الإتمام» للمناب النهائي؛ غائب/false قبل الاعتماد.
   */
  targetTerminal?: boolean;
  /** سبب رفض الدائرة الخطأ (§7.4) — يُعرض للمحامي في بطاقة الملف؛ غائب قبل أي رفض وبعد التصحيح. */
  rejectReason?: string | null;
  /** الشعبة الموجَّه لها طلب الإنابة الخارجية (§7.3) — غائب قبل التوجيه. */
  redirectedToSectionId?: number | null;
  /** اسم الشعبة الموجَّه لها الطلب — للعرض فقط. */
  redirectedToSectionName?: string | null;
  /** فرع الملف المناب الحي (قد يعبر الفروع — قرار §2.25) — قراءة سياقية داخل عرض المنيب. */
  targetBranchId?: number | null;
  /** اسم فرع الملف المناب الحي — للعرض فقط. */
  targetBranchName?: string | null;
  /** المحامي المالك الحالي للملف المناب — للعرض فقط. */
  targetLawyerName?: string | null;
  /** رمز التزامن التفاؤلي — يُرسَل في طلبات الكتابة الداعمة لكشف السباق. */
  version?: number;
}

/** تسطير/تعديل إنابة: التواريخ نصوص حرة تُفسَّر في الخلفية؛ الخارجية تتطلب الفرع المناب. */
export interface UpsertDelegationRequest {
  delegatedCourt?: string | null;
  /** الدائرة المنابة المرجعية (للداخلية — تُشتق منها الدائرة نصًا خدميًا). */
  delegatedCircuitId?: number | null;
  isExternal: boolean;
  externalBranchId?: number | null;
  delegationDate?: string | null;
  delegationText?: string | null;
  depositBookNumber?: string | null;
  depositBookDate?: string | null;
  /** معرفات أصول الملف المنيب موضوع الإنابة. */
  assetIds: number[];
}

/** اعتماد الإنابة: تعيين المحامي المختص من مالك الدائرة المنابة (يُنشأ الملف المناب تلقائيًا). */
export interface AssignDelegationRequest {
  assignedLawyerId: number;
  /** رمز التزامن التفاؤلي — اختياري، غيابه يعني فحص القاعدة فقط. */
  version?: number | null;
}

/** توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب — رئيس قسم الفرع المناب فقط (§7.3). */
export interface RedirectDelegationRequest {
  sectionId: number;
}

/** رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — السبب إلزامي. */
export interface RejectDelegationRequest {
  reason?: string | null;
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
