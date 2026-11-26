import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeDocumentResponse } from '../../utils/apiNormalization';
import { toUpsert } from '../../components/form/documentFormConstants';
import { EXEC_STATUS_STRUCK_OFF } from '../../utils/documentStatus';
import { newIdempotencyKey } from '../../utils/idempotency';
import { createEmptyUpsertForm } from './documentFormPayload';
import type { useDocumentParties } from './useDocumentParties';
import type { NumberReuseWarning } from '../../api/numberWarning';
import type {
  DocumentInterventionDto,
  DocumentResponse,
  DocumentUpsertRequest,
  GeneralEntitySide,
} from '../../types';

interface UseDocumentFormStateInput {
  id: string | undefined;
  isEdit: boolean;
  currentYear: number;
  branchName: string;
  parties: ReturnType<typeof useDocumentParties>;
}

/**
 * حالة النموذج الأساسية: نموذج `DocumentUpsertRequest`، التحميل/الحفظ، المبالغ،
 * المتدخلون، وتحميل ملف التعديل وتصفير الحالة. منقولة من `DocumentForm` مع
 * الحفاظ على تسلسل التصفير والتحميل وحارس سباق الجلب نفسه.
 */
export function useDocumentFormState({
  id,
  isEdit,
  currentYear,
  branchName,
  parties,
}: UseDocumentFormStateInput) {
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [numberWarning, setNumberWarning] = useState<NumberReuseWarning | null>(null);
  // مفتاح عدم التكرار (RF-011): واحد لكل فتح نموذج إدخال — يُرسَل في `POST` الإنشاء
  // فقط (التعديل `PUT` خارج النطاق)؛ والبصمة الخادمية تحمي تصحيح-ثم-إعادة-إرسال.
  const [idempotencyKey] = useState(newIdempotencyKey);
  // بوابة تحميل وضع التعديل: أثناء جلب ملف جديد لا يُعرض النموذج ولا يُتاح
  // الحفظ، فلا يرى المستخدم مال الملف السابق ولا يكتبه في الجديد —
  // يمنع التسرب العرضي والكتابي معًا عند تبديل الهوية (نفس المكوّن).
  // تُهيَّأ مفعّلة في التعديل لئلا يومض نموذج فارغ قبل أول جلب.
  const [loading, setLoading] = useState(isEdit);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [form, setForm] = useState<DocumentUpsertRequest>(() =>
    createEmptyUpsertForm(currentYear, branchName),
  );
  const [showInclusionAmount, setShowInclusionAmount] = useState(false);
  const [paidAmountSlots, setPaidAmountSlots] = useState(1);
  const [showRequiredAmount, setShowRequiredAmount] = useState(false);
  const [requiredAmountSlots, setRequiredAmountSlots] = useState(1);
  const [bankingAmountSlots, setBankingAmountSlots] = useState(1);
  const [ordinaryAmountSlots, setOrdinaryAmountSlots] = useState(1);
  const [wasOriginallyStruckOff, setWasOriginallyStruckOff] = useState(false);
  const [originalExecutedStatus, setOriginalExecutedStatus] = useState('');
  // المتدخلون: تُحمَّل مع الملف وتُدار عبر مسارات فرعية مستقلة (لا تدخل حمولة PUT)،
  // فلا يفقد حفظ المتدخل تعديلات النموذج غير المحفوظة والعكس.
  const [interventions, setInterventions] = useState<DocumentInterventionDto[]>([]);
  const [interventionModalOpen, setInterventionModalOpen] = useState(false);
  const [editingIntervention, setEditingIntervention] = useState<DocumentInterventionDto | null>(null);

  const { resetParties, applyLoadedDocument } = parties;

  // تصفير كامل لحالة النموذج المشتقة من ملف: يُستدعى قبل كل جلب (فلا يبقى
  // مال الملف السابق معروضًا ولا قابلًا للحفظ أثناء التحميل) ومن زر «مسح»
  // اليدوي — سلوك واحد موحد يمنع أي تسرب بين ملفين.
  const clearFormState = useCallback(() => {
    setForm(createEmptyUpsertForm(currentYear, branchName));
    resetParties();
    setInterventions([]);
    setInterventionModalOpen(false);
    setEditingIntervention(null);
    setShowInclusionAmount(false);
    setPaidAmountSlots(1);
    setShowRequiredAmount(false);
    setRequiredAmountSlots(1);
    setBankingAmountSlots(1);
    setOrdinaryAmountSlots(1);
    setWasOriginallyStruckOff(false);
    setOriginalExecutedStatus('');
  }, [resetParties, currentYear, branchName]);

  const loadDocument = useCallback(() => {
    if (id === undefined) return undefined;
    // قبل الجلب: تصفير الحالة وبوابة تحميل — فلا يُعرض مال السابق ولا يُحفظ به.
    clearFormState();
    setError('');
    setLoading(true);
    // حارس السباق: استجابة ملف سابق متأخرة تُتجاهل فلا تكتب ماله فوق الحالي.
    let cancelled = false;
    api
      .get<DocumentResponse>(`/documents/${id}`)
      .then((r) => {
        if (cancelled) return;
        const d = normalizeDocumentResponse(r.data);
        setForm(toUpsert(d));
        applyLoadedDocument(d);
        setShowInclusionAmount(
          Boolean(d.inclusionAmountNumeric || d.inclusionAmountWords),
        );
        // المبالغ المدفوعة (حتى ثلاثة) تظهر عند التعديل بحسب ما هو محفوظ منها.
        setPaidAmountSlots(
          Math.max(1, Number(Boolean(d.executedPaidAmount))
            + Number(Boolean(d.executedPaidAmount2))
            + Number(Boolean(d.executedPaidAmount3))),
        );
        // مبالغ «طالبة التنفيذ»: المصرفي (المطالب به) والعادي (المتضمن) حتى ثلاثة
        // بحسب ما هو محفوظ منها عند التعديل.
        setBankingAmountSlots(
          Math.max(1, Number(Boolean(d.amountNumeric))
            + Number(Boolean(d.amount2Numeric))
            + Number(Boolean(d.amount3Numeric))),
        );
        setOrdinaryAmountSlots(
          Math.max(1, Number(Boolean(d.inclusionAmountNumeric))
            + Number(Boolean(d.inclusionAmount2Numeric))
            + Number(Boolean(d.inclusionAmount3Numeric))),
        );
        // المبالغ المطلوب دفعها (حتى ثلاثة) تظهر عند التعديل بحسب ما هو محفوظ منها.
        setShowRequiredAmount(
          Boolean(d.executedRequiredAmount || d.executedRequiredAmount2 || d.executedRequiredAmount3),
        );
        setRequiredAmountSlots(
          Math.max(1, Number(Boolean(d.executedRequiredAmount))
            + Number(Boolean(d.executedRequiredAmount2))
            + Number(Boolean(d.executedRequiredAmount3))),
        );
        setInterventions(d.interventions ?? []);
        setWasOriginallyStruckOff(d.executedStatus === EXEC_STATUS_STRUCK_OFF);
        setOriginalExecutedStatus(d.executedStatus ?? '');
        setLoading(false);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(getApiErrorMessage(err));
        setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id, clearFormState, applyLoadedDocument]);

  useEffect(() => {
    if (!isEdit) return;
    return loadDocument();
  }, [id, isEdit, loadDocument]);

  const set = (key: keyof DocumentUpsertRequest, value: unknown) =>
    setForm((f) => ({ ...f, [key]: value }));

  // صفة الملف تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل. عند اختيار «الجهة العامة منفذ عليها»
  // أو «عرض وايداع» يُفرض نوع السند «عادي» فورًا (المصرفي محظور في هذا الوضع).
  const setSide = (side: GeneralEntitySide) =>
    setForm((f) => ({
      ...f,
      generalEntitySide: side,
      contractTypeSelector: side === 'executed' || side === 'deposit' ? 'عادي' : f.contractTypeSelector ?? 'مصرفي',
    }));

  const resetForm = () => {
    clearFormState();
    setError('');
  };

  const openAddIntervention = () => {
    setEditingIntervention(null);
    setInterventionModalOpen(true);
  };
  const openEditIntervention = (intervention: DocumentInterventionDto) => {
    setEditingIntervention(intervention);
    setInterventionModalOpen(true);
  };
  const applyInterventionSaved = (saved: DocumentInterventionDto) => {
    setInterventions((prev) =>
      prev.some((i) => i.id === saved.id)
        ? prev.map((i) => (i.id === saved.id ? saved : i))
        : [...prev, saved],
    );
    setInterventionModalOpen(false);
    setEditingIntervention(null);
  };

  // الممثل الشرعي للمقترض يخصّ حقول `form` (بخلاف بقية الأطراف في `useDocumentParties`).
  const activateBorrowerRep = () => {
    parties.markPartyRepDraft('borrower');
    setForm((f) => ({
      ...f,
      borrowerRepresentativeCapacity: 'ولي',
      borrowerRepresentativeAddressType: 'عنوان',
    }));
  };

  const removeBorrowerRep = () => {
    parties.clearPartyRepDraft('borrower');
    setForm((f) => ({
      ...f,
      borrowerRepresentativeName: '',
      borrowerRepresentativeFather: '',
      borrowerRepresentativeFamily: '',
      borrowerRepresentativeCapacity: '',
      borrowerRepresentativeAddressType: '',
      borrowerRepresentativeAddress: '',
    }));
  };

  return {
    form,
    set,
    setSide,
    error,
    setError,
    busy,
    setBusy,
    loading,
    deleteBusy,
    setDeleteBusy,
    numberWarning,
    setNumberWarning,
    idempotencyKey,
    showInclusionAmount,
    setShowInclusionAmount,
    paidAmountSlots,
    setPaidAmountSlots,
    showRequiredAmount,
    setShowRequiredAmount,
    requiredAmountSlots,
    setRequiredAmountSlots,
    bankingAmountSlots,
    setBankingAmountSlots,
    ordinaryAmountSlots,
    setOrdinaryAmountSlots,
    wasOriginallyStruckOff,
    originalExecutedStatus,
    interventions,
    setInterventions,
    interventionModalOpen,
    setInterventionModalOpen,
    editingIntervention,
    setEditingIntervention,
    clearFormState,
    loadDocument,
    resetForm,
    openAddIntervention,
    openEditIntervention,
    applyInterventionSaved,
    activateBorrowerRep,
    removeBorrowerRep,
  };
}
