/** عدادات بطاقة «الاستئنافات» في لوحة المحامي. */
export interface AppealsStatsDto {
  pendingCount: number;
  decidedInFavor: number;
  decidedAgainst: number;
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
  /** رقم الملف الفعّال (من أرقام أساس الملف نفسه) المعروض في الاستئناف. */
  documentEffectiveNumber?: string | null;
  /** سنة الرقم الفعّال المعروض للملف في الاستئناف. */
  documentEffectiveYear?: string | null;
  /** وسم جودة اللقطات: إحدى لقطتي الأطراف تالفة فعُرضت فارغة — تُعرض موسومة. */
  partiesDegraded: boolean;
  /** حالة الإحالة (`Owned` أو `ForwardedToHead`) — للبحث والعرض (المرحلة 5ب). */
  forwardState?: string;
  /** شعبة دائرة الملف المالكة (`undefined` = ملك القسم) — للبحث والعرض. */
  sectionId?: number | null;
  /** دائرة الملف — للبحث والعرض. */
  executionCircuitId?: number | null;
  /** رمز التزامن التفاؤلي — يُرسَل في طلبات الكتابة الداعمة لكشف السباق. */
  version?: number;
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
  /** رمز التزامن التفاؤلي (من `AppealDto.version`) — اختياري، غيابه يعني فحص القاعدة فقط. */
  version?: number;
}

/** نقل استئناف مفرد بين محامي الفرع — رئيس القسم. */
export interface TransferAppealRequest {
  targetLawyerId: number;
  /** رمز التزامن التفاؤلي (من `AppealDto.version`) — اختياري. */
  version?: number;
}

/** إحالة استئناف من رئيس الشعبة لرئيس القسم (اتجاه واحد) — السبب اختياري. */
export interface ForwardAppealRequest {
  reason?: string | null;
  /** رمز التزامن التفاؤلي (من `AppealDto.version`) — اختياري. */
  version?: number;
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
