import { describe, expect, it } from 'vitest';
import {
  bankingCurrencyKeys,
  emptyApplicantPublicEntity,
  emptyAsset,
  emptyExecutedHeir,
  emptyExecutedNaturalPerson,
  emptyExecutedPublicEntity,
  emptyExecutionApplicant,
  emptyGuarantor,
  emptyHeir,
  ordinaryCurrencyKeys,
  requiredCurrencyKeys,
} from '../../components/form/documentFormConstants';
import { buildDocumentPayload, createEmptyUpsertForm } from './documentFormPayload';
import type { BuildDocumentPayloadInput } from './documentFormPayload';
import type {
  AssetDto,
  DocumentUpsertRequest,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GuarantorDto,
  HeirDto,
  ApplicantPublicEntityDto,
} from '../../types';

function payloadInput(overrides: Partial<BuildDocumentPayloadInput> = {}): BuildDocumentPayloadInput {
  return {
    form: createEmptyUpsertForm(2026, 'الفرع الرئيسي - دمشق'),
    isEdit: false,
    isOrdinary: false,
    currentYear: 2026,
    guarantors: [emptyGuarantor()],
    borrowerHeirs: [],
    assets: [],
    executionApplicants: [emptyExecutionApplicant()],
    executedPublicEntities: [emptyExecutedPublicEntity()],
    executedNaturalPersons: [],
    applicantPublicEntities: [emptyApplicantPublicEntity()],
    ...overrides,
  };
}

describe('createEmptyUpsertForm', () => {
  it('يبني الافتراضيات النظامية نفسها في الإنشاء وإعادة الضبط', () => {
    const form = createEmptyUpsertForm(2026, 'فرع حلب');
    expect(form.fileYear).toBe('2026');
    expect(form.branchName).toBe('فرع حلب');
    expect(form.generalEntitySide).toBe('applicant');
    expect(form.contractTypeSelector).toBe('مصرفي');
    expect(form.currency).toBe('ليرة سورية');
    expect(form.currency2).toBe('دولار أمريكي');
    expect(form.borrowerAddressType).toBe('موطن مختار');
    expect(form.annexes).toEqual([]);
  });
});

describe('buildDocumentPayload · وضع طالبة التنفيذ', () => {
  it('يطبّع التواريخ ويحذف مفاتيح DateTime الفارغة ويقصّ الملاحق', () => {
    const form: DocumentUpsertRequest = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      borrowerBirth: ' ١/٨/٢٠٢٦ ',
      contractDate: '٢-٨-٢٠٢٦',
      fileRegistrationDate: '١/٨/٢٠٢٦',
      seizureDate: ' 1/8/2026 ',
      renewalDate: '٢٠٢٦-٠٨-٠١',
      annexes: [
        { sequence: 0, annexType: ' عقد ', annexNumber: ' ١٢ ', annexDate: '١/٨/٢٠٢٦' },
        { sequence: 1, annexType: '', annexNumber: '', annexDate: '  ' },
      ],
    };
    const payload = buildDocumentPayload(payloadInput({ form }));

    expect(payload.borrowerBirth).toBe('1/8/2026');
    expect(payload.contractDate).toBe('2-8-2026');
    expect(payload.fileRegistrationDate).toBe('1/8/2026');
    expect(payload.seizureDate).toBe('1/8/2026');
    expect(payload.renewalDate).toBe('2026-08-01');
    expect(payload).not.toHaveProperty('fileReceiptDate');
    expect(payload).not.toHaveProperty('struckOffDate');
    expect(payload.annexType).toBe('');
    expect(payload.annexNumber).toBe('');
    expect(payload.annexDate).toBe('');
    expect(payload.annexes).toEqual([
      { sequence: 0, annexType: 'عقد', annexNumber: '١٢', annexDate: '1/8/2026' },
      { sequence: 1, annexType: '', annexNumber: '', annexDate: '' },
    ]);
  });

  it('يحسب عملات الخانات غير مكررة ويجعل السند مصرفيًا افتراضيًا', () => {
    const form = createEmptyUpsertForm(2026, 'دمشق');
    const payload = buildDocumentPayload(payloadInput({ form }));
    expect(payload.contractTypeSelector).toBe('مصرفي');
    expect(payload.currency).toBe('ليرة سورية');
    expect(bankingCurrencyKeys).toContain('currency');
    expect(payload.currency2).not.toBe(payload.currency);
    expect(payload.inclusionCurrency).toBe('ليرة سورية');
    expect(ordinaryCurrencyKeys).toContain('inclusionCurrency');
    expect(requiredCurrencyKeys).toContain('executedRequiredCurrency');
  });

  it('يفلتر الأطراف والأصول الفارغة ويُرقّم الكفلاء موضعيًا في الوضع العادي', () => {
    const filledGuarantor: GuarantorDto = { ...emptyGuarantor(), name: 'كفيل', guarantorNumber: 7 };
    const emptyNamed: GuarantorDto = { ...emptyGuarantor(), name: '   ' };
    const heir: HeirDto = { ...emptyHeir(), name: 'وريث' };
    const emptyHeirRow: HeirDto = emptyHeir();
    const asset: AssetDto = { ...emptyAsset('عقار'), propertyNumber: '12', propertyDistrict: 'المزة' };
    const emptyAssetRow: AssetDto = emptyAsset('مركبة');
    const entity: ApplicantPublicEntityDto = { ...emptyApplicantPublicEntity(), name: 'جهة', registryId: 3 };
    const blankEntity: ApplicantPublicEntityDto = emptyApplicantPublicEntity();

    const payload = buildDocumentPayload(payloadInput({
      form: { ...createEmptyUpsertForm(2026, 'دمشق'), contractTypeSelector: 'عادي', fileNumber: '1' },
      isOrdinary: true,
      guarantors: [filledGuarantor, emptyNamed],
      borrowerHeirs: [heir, emptyHeirRow],
      assets: [asset, emptyAssetRow],
      applicantPublicEntities: [entity, blankEntity],
    }));

    expect(payload.guarantors).toHaveLength(1);
    expect(payload.guarantors[0].guarantorNumber).toBe(1);
    expect(payload.borrowerHeirs).toHaveLength(1);
    expect(payload.assets).toHaveLength(1);
    expect(payload.assets[0].property).toBe('12 المزة');
    expect(payload.applicantPublicEntities).toEqual([entity]);
    expect(payload.executionApplicants).toEqual([]);
  });

  it('يحتفظ برقم الكفيل المستقر في الوضع المصرفي', () => {
    const guarantor: GuarantorDto = { ...emptyGuarantor(), name: 'كفيل', guarantorNumber: 9 };
    const payload = buildDocumentPayload(payloadInput({ guarantors: [guarantor] }));
    expect(payload.guarantors[0].guarantorNumber).toBe(9);
  });

  it('يزرع الملاحظات كإجراء أولي في الإنشاء فقط ويقصّها', () => {
    const createForm = { ...createEmptyUpsertForm(2026, 'دمشق'), notes: '  ملاحظة  ' };
    const created = buildDocumentPayload(payloadInput({ form: createForm }));
    expect(created.initialActions).toEqual([{ type: 'note', text: 'ملاحظة' }]);

    const edited = buildDocumentPayload(payloadInput({ form: createForm, isEdit: true }));
    expect(edited.initialActions).toBeUndefined();

    const blank = buildDocumentPayload(payloadInput({
      form: { ...createEmptyUpsertForm(2026, 'دمشق'), notes: '   ' },
    }));
    expect(blank.initialActions).toBeUndefined();
  });

  it('يصفّر عنوان المقترض عند وجود وريث مخزَّن أو ممثل مخزَّن فقط', () => {
    const heirForm = { ...createEmptyUpsertForm(2026, 'دمشق'), borrowerAddress: 'عنوان', borrowerAddressType: 'عنوان' };
    const withHeir = buildDocumentPayload(payloadInput({
      form: heirForm,
      borrowerHeirs: [{ ...emptyHeir(), name: 'وريث' }],
    }));
    expect(withHeir.borrowerAddress).toBe('');
    expect(withHeir.borrowerAddressType).toBe('');

    const withDraftRepOnly = buildDocumentPayload(payloadInput({
      form: {
        ...heirForm,
        borrowerRepresentativeCapacity: 'ولي',
        borrowerRepresentativeAddressType: 'عنوان',
      },
    }));
    expect(withDraftRepOnly.borrowerAddress).toBe('عنوان');

    const withStoredRep = buildDocumentPayload(payloadInput({
      form: { ...heirForm, borrowerRepresentativeName: 'ممثل' },
    }));
    expect(withStoredRep.borrowerAddress).toBe('');
  });
});

describe('buildDocumentPayload · وضع منفذ عليه/عرض وايداع', () => {
  it('يفرض السند العادي ويحذف أطراف الجهة الأخرى ويضبط سنة الإعادة', () => {
    const form: DocumentUpsertRequest = {
      ...createEmptyUpsertForm(2025, 'دمشق'),
      generalEntitySide: 'executed',
      contractTypeSelector: 'مصرفي',
      fileNumber: '500',
      fileYear: '2025',
      borrowerAddress: 'عنوان',
    };
    const executedEntity: ExecutedPublicEntityDto = {
      ...emptyExecutedPublicEntity(), entityName: 'جهة منفذ عليها', registryId: 5, governorate: 'دمشق',
    };
    const person: ExecutedNaturalPersonDto = { ...emptyExecutedNaturalPerson(), name: 'شخص' };
    const payload = buildDocumentPayload(payloadInput({
      form,
      guarantors: [{ ...emptyGuarantor(), name: 'كفيل' }],
      borrowerHeirs: [{ ...emptyHeir(), name: 'وريث' }],
      assets: [emptyAsset('عقار')],
      executedPublicEntities: [executedEntity, emptyExecutedPublicEntity()],
      executedNaturalPersons: [person, emptyExecutedNaturalPerson()],
      applicantPublicEntities: [{ ...emptyApplicantPublicEntity(), name: 'جهة طالبة' }],
    }));

    expect(payload.renewalYear).toBe(2026);
    expect(payload.contractTypeSelector).toBe('عادي');
    expect(payload.guarantors).toEqual([]);
    expect(payload.borrowerHeirs).toEqual([]);
    expect(payload.assets).toEqual([]);
    expect(payload.applicantPublicEntities).toEqual([]);
    expect(payload.borrowerAddress).toBe('');
    expect(payload.executedPublicEntities).toEqual([executedEntity]);
    expect(payload.executedNaturalPersons).toHaveLength(1);
  });

  it('يطبّق وضع «عرض وايداع» بالقواعد نفسها', () => {
    const form = { ...createEmptyUpsertForm(2026, 'دمشق'), generalEntitySide: 'deposit' as const };
    const payload = buildDocumentPayload(payloadInput({ form }));
    expect(payload.contractTypeSelector).toBe('عادي');
    expect(payload.renewalYear).toBe(2026);
  });

  it('يمسح الوكيل القانوني لممثل مخزَّن فقط ويفلتر ورثة طالبي التنفيذ', () => {
    const withRepresentative: ExecutionApplicantDto = {
      ...emptyExecutionApplicant(),
      name: 'طالب',
      legalRepresentative: 'وكيل',
      representativeName: 'ممثل',
      heirs: [
        { ...emptyExecutedHeir(), heirName: 'وريث' },
        emptyExecutedHeir(),
      ],
    };
    const draftRepresentative: ExecutionApplicantDto = {
      ...emptyExecutionApplicant(),
      name: 'طالب بلا اسم ممثل',
      legalRepresentative: 'وكيل',
      representativeCapacity: 'ولي',
      heirs: [emptyExecutedHeir()],
    };
    const form = {
      ...createEmptyUpsertForm(2026, 'دمشق'),
      generalEntitySide: 'executed' as const,
      fileNumber: '1',
      fileYear: '2026',
    };
    const payload = buildDocumentPayload(payloadInput({
      form,
      executionApplicants: [withRepresentative, draftRepresentative, emptyExecutionApplicant()],
    }));

    expect(payload.executionApplicants).toHaveLength(2);
    expect(payload.executionApplicants[0].legalRepresentative).toBe('');
    expect(payload.executionApplicants[0].heirs).toHaveLength(1);
    expect(payload.executionApplicants[0].heirs?.[0].heirName).toBe('وريث');
    expect(payload.executionApplicants[1].legalRepresentative).toBe('وكيل');
    expect(payload.executionApplicants[1].heirs).toEqual([]);
  });

  it('يحفظ عدّاد التزامن المتفائل من النموذج (RF-010)', () => {
    const payload = buildDocumentPayload(payloadInput({
      form: { ...createEmptyUpsertForm(2026, 'دمشق'), version: 7 },
      isEdit: true,
    }));
    expect(payload.version).toBe(7);
  });
});
