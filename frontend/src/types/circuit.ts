/* ── سجل دوائر التنفيذ (BQ-004) ─────────────────────────────────────── */

/** دائرة تنفيذ واحدة مع عداداتها (ملفات + معلقات). */
export interface ExecutionCircuitDto {
  id: number;
  branchId: number;
  branchName?: string | null;
  name: string;
  isActive: boolean;
  fileCount: number;
  pendingCount: number;
  version: number;
  /** معرّف الشعبة المالكة — غائب/`null` يعني قسم الفرع (§8). */
  sectionId?: number | null;
  /** اسم الشعبة المالكة — للعرض فقط. */
  sectionName?: string | null;
}

/** إدخال/تسمية دائرة (الاسم فقط — الفرع من سياق رئيس القسم). */
export interface UpsertExecutionCircuitRequest {
  name?: string | null;
  version?: number | null;
}

/** تعطيل/تفعيل دائرة. */
export interface SetCircuitActiveRequest {
  isActive: boolean;
  version?: number | null;
}

/** إحالة دفعة ملفات من دائرة إلى محامٍ (نفس الفرع، ذري). */
export interface ReferCircuitFilesRequest {
  fileIds: number[];
  targetLawyerId: number;
  targetCircuitId: number;
}

/** صف ملف بانتظار إعادة القيد (المعروض حاليًا قديم من EffectiveFileIdentity). */
export interface PendingRegistrationDto {
  documentId: number;
  circuitId: number;
  circuitName?: string | null;
  borrowerName?: string | null;
  oldFileNumber?: string | null;
  oldFileType?: string | null;
  oldFileYear?: string | null;
}

/** حفظ ذري لإعادة القيد (ملك المحامي فقط + وحدانية). */
export interface CompleteRegistrationsRequest {
  entries: Array<{
    documentId: number;
    fileNumber?: string | null;
    fileType?: string | null;
    fileYear?: string | null;
  }>;
}

/** صف إحصائية دائرة (الدائرة × ملفاتها × محامون نشطون × معلقات) — مع عمود الشعبة (§12). */
export interface CircuitStatsDto {
  circuitId: number;
  circuitName: string;
  branchId: number;
  branchName?: string | null;
  isActive: boolean;
  fileCount: number;
  lawyerCount: number;
  pendingCount: number;
  /** شعبة الدائرة المالكة — غائب يعني القسم. */
  sectionId?: number | null;
  /** اسم الشعبة المالكة — للعرض فقط. */
  sectionName?: string | null;
  /** رمز التزامن التفاؤلي — يُرسَل في نقل الدائرة لكشف السباق. */
  version?: number | null;
}
