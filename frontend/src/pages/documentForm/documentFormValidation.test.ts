import { describe, expect, it } from 'vitest';
import {
  emptyApplicantPublicEntity,
  emptyExecutedPublicEntity,
  emptyGuarantor,
  emptyHeir,
  FUTURE_FILE_YEAR_MESSAGE,
} from '../../components/form/documentFormConstants';
import { createEmptyUpsertForm } from './documentFormPayload';
import { isExecutedSubmit, validateDocumentForm } from './documentFormValidation';
import type { DocumentFormValidationInput } from './documentFormValidation';
import type {
  ApplicantPublicEntityDto,
  DocumentUpsertRequest,
  ExecutedPublicEntityDto,
  ExecutionCircuitDto,
} from '../../types';

const circuit = (id: number): ExecutionCircuitDto => ({
  id,
  branchId: 1,
  name: 'دائرة',
  isActive: true,
  fileCount: 0,
  pendingCount: 0,
  version: 0,
  deletedFileCount: 0,
});

function validationInput(overrides: Partial<DocumentFormValidationInput> = {}): DocumentFormValidationInput {
  return {
    form: createEmptyUpsertForm(2026, 'دمشق'),
    isEdit: false,
    isMirror: false,
    currentYear: 2026,
    branchCircuits: [],
    applicantPublicEntities: [emptyApplicantPublicEntity()],
    executedPublicEntities: [emptyExecutedPublicEntity()],
    borrowerHeirs: [],
    guarantors: [emptyGuarantor()],
    wasOriginallyStruckOff: false,
    ...overrides,
  };
}

function applicantWithRegistry(): ApplicantPublicEntityDto {
  return { ...emptyApplicantPublicEntity(), name: 'جهة', registryId: 1 };
}

function executedWithRegistry(): ExecutedPublicEntityDto {
  return { ...emptyExecutedPublicEntity(), entityName: 'جهة', registryId: 2 };
}

describe('isExecutedSubmit', () => {
  it('يشمل «منفذ عليها» و«عرض وايداع» فقط', () => {
    expect(isExecutedSubmit({ generalEntitySide: 'applicant' })).toBe(false);
    expect(isExecutedSubmit({ generalEntitySide: 'executed' })).toBe(true);
    expect(isExecutedSubmit({ generalEntitySide: 'deposit' })).toBe(true);
    expect(isExecutedSubmit({})).toBe(false);
  });
});

describe('validateDocumentForm · الدائرة', () => {
  it('يرفض الحفظ ويطلب تركيز حقل الدائرة عند وجود سجل بلا اختيار', () => {
    const result = validateDocumentForm(validationInput({
      branchCircuits: [circuit(1)],
    }));
    expect(result).toEqual({ message: 'دائرة التنفيذ مطلوبة — اختر من السجل', focusCircuit: true });
  });

  it('يقبل بلا دائرة حين يكون السجل فارغًا أو تعذّر تحميله (انتقالي)', () => {
    expect(validateDocumentForm(validationInput({ branchCircuits: [] }))).toBeNull();
    expect(validateDocumentForm(validationInput({ branchCircuits: null }))).toBeNull();
  });

  it('لا يفرض الدائرة على الملف المناب', () => {
    const result = validateDocumentForm(validationInput({
      isMirror: true,
      branchCircuits: [circuit(1)],
    }));
    expect(result).toBeNull();
  });

  it('يقبل عند اختيار دائرة حتى لو وُجد سجل', () => {
    const form = { ...createEmptyUpsertForm(2026, 'دمشق'), executionCircuitId: 4, court: 'دائرة' };
    const result = validateDocumentForm(validationInput({
      form,
      branchCircuits: [circuit(4)],
    }));
    expect(result).toBeNull();
  });
});

describe('validateDocumentForm · رقم الملف والتواريخ', () => {
  it('يرفض «منفذ عليه» بلا رقم أو سنة ملف', () => {
    const form = { ...createEmptyUpsertForm(2026, 'دمشق'), generalEntitySide: 'executed' as const };
    expect(validateDocumentForm(validationInput({ form }))).toEqual({
      message: 'ملف «الجهة العامة منفذ عليها» يجب أن يكون مقيدًا برقم وسنة الملف',
    });
  });

  it('يسمّي «عرض وايداع» في رسالة رقم الملف', () => {
    const form = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      generalEntitySide: 'deposit' as const,
      fileNumber: '',
      fileYear: '',
    };
    expect(validateDocumentForm(validationInput({ form }))?.message).toContain('عرض وايداع');
  });

  it('يرفض رقمًا وسنة بلا تاريخ قيد في وضع طالبة التنفيذ', () => {
    const form: DocumentUpsertRequest = {
      ...createEmptyUpsertForm(2026, 'دمشق'), fileNumber: '500', fileYear: '2026',
    };
    expect(validateDocumentForm(validationInput({ form }))).toEqual({
      message: 'تاريخ قيد الملف مطلوب عند إدخال رقم الملف وسنة الملف',
    });
  });

  it('يرفض سنة مستقبلية ولو كانت بأرقام عربية', () => {
    const future = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      fileNumber: '1',
      fileRegistrationDate: '1/1/2026',
      fileYear: '٢٠٢٧',
    };
    expect(validateDocumentForm(validationInput({ form: future }))).toEqual({
      message: FUTURE_FILE_YEAR_MESSAGE,
    });
  });

  it('يقبل سنة جارية أو ماضية برقم وتاريخ قيد', () => {
    const form = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      fileNumber: '1',
      fileYear: '2025',
      fileRegistrationDate: '1/1/2025',
    };
    expect(validateDocumentForm(validationInput({ form }))).toBeNull();
  });

  it('يشترط رقم الملف الجديد عند إعادة مشطوب إلى متداول', () => {
    const form = { ...createEmptyUpsertForm(2026, 'دمشق'), executedStatus: '', renewalFileNumber: '' };
    expect(validateDocumentForm(validationInput({ form, isEdit: true, wasOriginallyStruckOff: true }))).toEqual({
      message: 'رقم الملف الجديد مطلوب عند إعادة الملف المشطوب إلى المتداول',
    });

    const withNumber = { ...form, renewalFileNumber: '600' };
    expect(validateDocumentForm(validationInput({
      form: withNumber, isEdit: true, wasOriginallyStruckOff: true,
    }))).toBeNull();
  });
});

describe('validateDocumentForm · ربط السجل المرجعي', () => {
  it('يرفض جهة طالبة مُعبأة بلا رابط سجل', () => {
    const form = { ...createEmptyUpsertForm(2026, 'دمشق'), fileNumber: '1', fileRegistrationDate: '1/1/2026' };
    const result = validateDocumentForm(validationInput({
      form,
      applicantPublicEntities: [{ ...emptyApplicantPublicEntity(), name: 'جهة بلا رابط' }],
    }));
    expect(result).toEqual({
      message: 'يجب اختيار جميع الجهات العامة الطالبة للتنفيذ من السجل المرجعي قبل الحفظ',
    });
  });

  it('يقبل الجهات المرتبطة ويتجاهل الصفوف الفارغة', () => {
    const form = { ...createEmptyUpsertForm(2026, 'دمشق'), fileNumber: '1', fileRegistrationDate: '1/1/2026' };
    expect(validateDocumentForm(validationInput({
      form,
      applicantPublicEntities: [applicantWithRegistry(), emptyApplicantPublicEntity()],
    }))).toBeNull();
  });

  it('يرفض جهة منفذ عليها مُعبأة بلا رابط سجل', () => {
    const form = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      generalEntitySide: 'executed' as const,
      fileNumber: '1',
      fileYear: '2026',
    };
    const result = validateDocumentForm(validationInput({
      form,
      executedPublicEntities: [{ ...emptyExecutedPublicEntity(), nature: 'public', entityName: 'جهة' }],
    }));
    expect(result).toEqual({
      message: 'يجب اختيار جميع الجهات العامة المنفذ عليها من السجل المرجعي قبل الحفظ',
    });
    expect(validateDocumentForm(validationInput({
      form,
      executedPublicEntities: [executedWithRegistry()],
    }))).toBeNull();
  });
});

describe('validateDocumentForm · تعارض الممثل والورثة', () => {
  it('يرفض اجتماع الورثة مع الممثل الشرعي للمقترض في وضع طالبة التنفيذ', () => {
    const form = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      borrowerRepresentativeName: 'ممثل',
    };
    const result = validateDocumentForm(validationInput({
      form,
      borrowerHeirs: [{ ...emptyHeir(), name: 'وريث' }],
    }));
    expect(result?.message).toContain('لا يجتمع الممثل الشرعي مع الورثة — المقترض');
  });

  it('يسمّي الكفيل برقمه عند التعارض', () => {
    const result = validateDocumentForm(validationInput({
      guarantors: [{
        ...emptyGuarantor(),
        guarantorNumber: 3,
        representativeName: 'ممثل',
        heirs: [{ ...emptyHeir(), name: 'وريث' }],
      }],
    }));
    expect(result?.message).toContain('الكفيل 3');
  });

  it('لا يفحص أطراف «منفذ عليها» ولا الوريث الفارغ', () => {
    const form = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      generalEntitySide: 'executed' as const,
      fileNumber: '1',
      fileYear: '2026',
      borrowerRepresentativeName: 'ممثل',
    };
    expect(validateDocumentForm(validationInput({ form, borrowerHeirs: [emptyHeir()] }))).toBeNull();
  });
});
