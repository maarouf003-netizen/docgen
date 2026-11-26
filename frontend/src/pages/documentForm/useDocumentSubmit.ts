import { api } from '../../api/client';
import { getNumberReuseWarning } from '../../api/numberWarning';
import { idempotencyHeaders } from '../../utils/idempotency';
import { buildDocumentPayload } from './documentFormPayload';
import { validateDocumentForm } from './documentFormValidation';
import type { FormEvent } from 'react';
import type { useDocumentFormState } from './useDocumentFormState';
import type { useDocumentParties } from './useDocumentParties';
import type { useDocumentReferences } from './useDocumentReferences';

interface UseDocumentSubmitInput {
  id: string | undefined;
  isEdit: boolean;
  currentYear: number;
  navigate: (to: string) => void;
  state: ReturnType<typeof useDocumentFormState>;
  parties: ReturnType<typeof useDocumentParties>;
  references: Pick<ReturnType<typeof useDocumentReferences>, 'branchCircuits' | 'circuitPickerButtonRef'>;
}

/**
 * إرسال النموذج: التحقق الأمامي ثم بناء الحمولة النقية ثم `POST/PUT`.
 * السلوك (رسائل الرفض، تركيز حقل الدائرة، معالجة تحذير إعادة استخدام الرقم،
 * مفتاح عدم التكرار للإنشاء فقط) منقول حرفيًا من `DocumentForm.submit`.
 */
export function useDocumentSubmit({
  id,
  isEdit,
  currentYear,
  navigate,
  state,
  parties,
  references,
}: UseDocumentSubmitInput) {
  const { form, setError, setBusy, setNumberWarning } = state;
  const {
    guarantors,
    borrowerHeirs,
    assets,
    executionApplicants,
    executedPublicEntities,
    executedNaturalPersons,
    applicantPublicEntities,
  } = parties;

  const submit = async (e?: FormEvent, warningToken?: string) => {
    e?.preventDefault();
    setError('');

    const validation = validateDocumentForm({
      form,
      isEdit,
      isMirror: parties.isMirror,
      currentYear,
      branchCircuits: references.branchCircuits,
      applicantPublicEntities,
      executedPublicEntities,
      borrowerHeirs,
      guarantors,
      wasOriginallyStruckOff: state.wasOriginallyStruckOff,
    });
    if (validation) {
      setError(validation.message);
      if (validation.focusCircuit) references.circuitPickerButtonRef.current?.focus();
      return;
    }

    setBusy(true);
    try {
      const payload = buildDocumentPayload({
        form,
        isEdit,
        isOrdinary: form.contractTypeSelector === 'عادي',
        currentYear,
        guarantors,
        borrowerHeirs,
        assets,
        executionApplicants,
        executedPublicEntities,
        executedNaturalPersons,
        applicantPublicEntities,
      });
      if (warningToken) payload.warningToken = warningToken;
      if (isEdit) await api.put(`/documents/${id}`, payload);
      else await api.post('/documents', payload, idempotencyHeaders(state.idempotencyKey));
      navigate('/documents');
    } catch (err: unknown) {
      const reuse = getNumberReuseWarning(err);
      if (reuse) {
        setNumberWarning(reuse);
        return;
      }
      const msg = (err as { response?: { data?: { message?: string } } }).response?.data?.message;
      setError(msg || 'حدث خطأ في حفظ المستند');
    } finally {
      setBusy(false);
    }
  };

  return { submit };
}
