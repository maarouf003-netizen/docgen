import {
  FUTURE_FILE_YEAR_MESSAGE,
  hasHeirName,
  hasStoredRepresentative,
  isFutureFileYear,
  representativeHeirConflictMessage,
} from '../../components/form/documentFormConstants';
import type {
  ApplicantPublicEntityDto,
  DocumentUpsertRequest,
  ExecutedPublicEntityDto,
  ExecutionCircuitDto,
  GuarantorDto,
  HeirDto,
} from '../../types';

export interface DocumentFormValidationInput {
  form: DocumentUpsertRequest;
  isEdit: boolean;
  isMirror: boolean;
  currentYear: number;
  branchCircuits: ExecutionCircuitDto[] | null;
  applicantPublicEntities: ApplicantPublicEntityDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  borrowerHeirs: HeirDto[];
  guarantors: GuarantorDto[];
  wasOriginallyStruckOff: boolean;
}

export type DocumentFormValidationResult =
  | { message: string; focusCircuit: true }
  | { message: string; focusCircuit?: false }
  | null;

/** هل الحفظ في وضع «منفذ عليه/عرض وايداع» (تُقيَّد فيه الأطراف الأخرى)؟ */
export function isExecutedSubmit(form: Pick<DocumentUpsertRequest, 'generalEntitySide'>): boolean {
  return form.generalEntitySide === 'executed' || form.generalEntitySide === 'deposit';
}

/**
 * تحقق ما قبل الإرسال، بترتيب الرسائل نفسه المعتمد في النموذج الأصلي.
 * لا يلمس الحالة: يُعيد أول رسالة رفض (مع طلب تركيز حقل الدائرة عند لزومه)،
 * و`null` عند صلاحية الإرسال. الحارس الخلفي يبقى المرجع القاطع.
 */
export function validateDocumentForm(input: DocumentFormValidationInput): DocumentFormValidationResult {
  const { form, isEdit, isMirror, currentYear, branchCircuits } = input;
  const executedSubmit = isExecutedSubmit(form);
  const depositSubmit = form.generalEntitySide === 'deposit';
  const sideLabel = depositSubmit ? 'عرض وايداع' : 'الجهة العامة منفذ عليها';

  // سجل الدوائر: الدائرة إلزامية دائمًا وفي كل الأوضاع — تمنع الحفظ مع تركيز أول خطأ.
  // انتقالي: فرع بلا سجل (قائمة فارغة أو تعذّر تحميلها كبيئة الاختبارات) يُقبل بلا دائرة.
  const registryEnforced = (branchCircuits?.length ?? 0) > 0 || form.executionCircuitId != null;
  if (form.executionCircuitId == null && !isMirror && registryEnforced) {
    return { message: 'دائرة التنفيذ مطلوبة — اختر من السجل', focusCircuit: true };
  }

  if (executedSubmit && (!(form.fileNumber ?? '').trim() || !(form.fileYear ?? '').trim())) {
    return { message: `ملف «${sideLabel}» يجب أن يكون مقيدًا برقم وسنة الملف` };
  }

  if (
    !executedSubmit
    && (form.fileNumber ?? '').trim()
    && (form.fileYear ?? '').trim()
    && !(form.fileRegistrationDate ?? '').trim()
  ) {
    return { message: 'تاريخ قيد الملف مطلوب عند إدخال رقم الملف وسنة الملف' };
  }

  // سنة الملف لا تكون مستقبلية في أي وضع (القائمة تمنعها عادةً — وهذا حارس
  // لقيمة قديمة مخزنة أو عبث؛ الخلفية ترفضها أيضًا برسالتها).
  if (isFutureFileYear((form.fileYear ?? '').trim(), currentYear)) {
    return { message: FUTURE_FILE_YEAR_MESSAGE };
  }

  // العودة من مشطوب إلى متداول تستلزم رقم الملف الجديد (تجديد) — يتحقق مسبقًا
  // قبل إرسال النموذج ليطابق اشتراط الخلفية (رقم الملف الجديد إلزامي).
  if (
    isEdit
    && input.wasOriginallyStruckOff
    && !(form.executedStatus ?? '')
    && !(form.renewalFileNumber ?? '').trim()
  ) {
    return { message: 'رقم الملف الجديد مطلوب عند إعادة الملف المشطوب إلى المتداول' };
  }

  // حقول الجهات العامة نصّية مقفلة تُملأ حصرًا من السجل المرجعي: أي صفٍ مُعبأ بلا
  // رابطٍ للسجل يُمنع الحفظ (وهذا يغطي أيضًا المستندات القديمة غير المرتبطة فتتطلب
  // إعادة الاختيار قبل الحفظ). التحرير اليدوي مستحيل بعد القفل، فلم يبقَ مسار آخر.
  if (
    !executedSubmit
    && input.applicantPublicEntities.some((a) => (a.name ?? '').trim() && a.registryId == null)
  ) {
    return { message: 'يجب اختيار جميع الجهات العامة الطالبة للتنفيذ من السجل المرجعي قبل الحفظ' };
  }
  if (
    executedSubmit
    && input.executedPublicEntities.some(
      (e) => (e.nature ?? 'public') === 'public' && (e.entityName ?? '').trim() && e.registryId == null,
    )
  ) {
    return { message: 'يجب اختيار جميع الجهات العامة المنفذ عليها من السجل المرجعي قبل الحفظ' };
  }

  const repConflict = representativeHeirConflictMessage({
    borrowerNature: form.borrowerNature,
    // الفحص يطابق الحمولة المرسلة: أطراف الجهة الأخرى لا تُرسل ولا تُفحص.
    borrowerHasHeirs: !executedSubmit && input.borrowerHeirs.some(hasHeirName),
    borrowerHasRep: !executedSubmit && hasStoredRepresentative({
      representativeName: form.borrowerRepresentativeName,
      representativeFather: form.borrowerRepresentativeFather,
      representativeFamily: form.borrowerRepresentativeFamily,
    }),
    guarantors: executedSubmit ? [] : input.guarantors,
  });
  if (repConflict) {
    return { message: repConflict };
  }

  return null;
}
