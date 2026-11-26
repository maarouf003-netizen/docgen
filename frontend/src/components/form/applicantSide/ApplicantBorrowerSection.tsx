import type { HeirDto, DocumentUpsertRequest } from '../../../types';
import {
  ADDRESS_TYPE_OPTIONS,
  PARTY_NATURE_OPTIONS,
  addressLabelOf,
  hasHeirName,
  hasRepresentative,
  hasStoredRepresentative,
} from '../documentFormConstants';
import { HeirsEditor } from '../HeirsEditors';
import { RepresentativeEditor } from '../RepresentativeEditor';
import { FormSectionTitle } from '../FormSectionTitle';
import type { FormSet, makeFieldHelpers } from '../formFields';

/** مطابقة مفاتيح محرر الممثل العام (بلا بادئة) إلى مفاتيح الممثل الشرعي للمقترض (ببادئة borrower). */
const BORROWER_REP_KEYS: Record<string, keyof DocumentUpsertRequest> = {
  representativeName: 'borrowerRepresentativeName',
  representativeFather: 'borrowerRepresentativeFather',
  representativeFamily: 'borrowerRepresentativeFamily',
  representativeCapacity: 'borrowerRepresentativeCapacity',
  representativeAddressType: 'borrowerRepresentativeAddressType',
  representativeAddress: 'borrowerRepresentativeAddress',
};

type FieldHelper = ReturnType<typeof makeFieldHelpers>['field'];
type OptionSelectHelper = ReturnType<typeof makeFieldHelpers>['optionSelectField'];

interface ApplicantBorrowerSectionProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  lock: boolean;
  isOrdinary: boolean;
  field: FieldHelper;
  optionSelectField: OptionSelectHelper;
  borrowerHeirs: HeirDto[];
  onBorrowerHeirSet: (i: number, key: keyof HeirDto, value: string) => void;
  onBorrowerHeirAdd: () => void;
  onBorrowerHeirRemove: (i: number) => void;
  onBorrowerRepActivate: () => void;
  onBorrowerRepRemove: () => void;
  partyRepLocked: (key: string) => boolean;
}

/**
 * بيانات المقترض/المنفذ عليه (طبيعي/اعتباري) + ورثته + ممثله الشرعي،
 * بقواعد إخفاء العنوان نفسها — منقولة حرفيًا من `ApplicantSideSections`.
 */
export function ApplicantBorrowerSection({
  form,
  set,
  lock,
  isOrdinary,
  field,
  optionSelectField,
  borrowerHeirs,
  onBorrowerHeirSet,
  onBorrowerHeirAdd,
  onBorrowerHeirRemove,
  onBorrowerRepActivate,
  onBorrowerRepRemove,
  partyRepLocked,
}: ApplicantBorrowerSectionProps) {
  // إظهار حقول العنوان يتبع المخزَّن فعليًا (كقرارات الحمولة): صف وريث فارغ
  // الاسم الثلاثي ليس وريثًا فيُبقي الحقول ظاهرة وقيمها محفوظة عند الحفظ.
  const borrowerHasHeirs = borrowerHeirs.some(hasHeirName);
  // زر ممثل الطرف يختفي فور وجود أي صف وريث (حتى فارغ) — بينما إخفاء العنوان يتبع
  // الوريث المسمّى فقط (صف فارغ ليس وريثًا فيُبقي العنوان وقيمته).
  const borrowerHasHeirRows = borrowerHeirs.length > 0;
  // إخفاء العنوان يتبع «المخزَّن» فعلًا (كحمولة toHeirPayload)؛ تفعيل الممثل بصفة
  // وحدها لا يُخفيه لأن الخلفية تُصفّر الشبح عند الحفظ فيعود العنوان بعد التحميل.
  const borrowerRepStored = hasStoredRepresentative({
    representativeName: form.borrowerRepresentativeName,
    representativeFather: form.borrowerRepresentativeFather,
    representativeFamily: form.borrowerRepresentativeFamily,
  });
  const borrowerHasRep = hasRepresentative({
    representativeName: form.borrowerRepresentativeName,
    representativeFather: form.borrowerRepresentativeFather,
    representativeFamily: form.borrowerRepresentativeFamily,
    representativeCapacity: form.borrowerRepresentativeCapacity,
  });

  return (
    <>
      <FormSectionTitle title={isOrdinary ? '👤 بيانات المنفذ عليه' : '👤 بيانات المقترض'} />
      <div className="grid md:grid-cols-3 gap-4">
        <select
          id="borrowerNature"
          aria-label="نوع الطرف"
          value={form.borrowerNature ?? 'natural'}
          onChange={(e) => set('borrowerNature', e.target.value)}
          disabled={lock}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed"
        >
          {PARTY_NATURE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </div>
      {form.borrowerNature === 'legal' ? (
        <div className="grid md:grid-cols-3 gap-4 mt-4">
          {field('الشخص الاعتباري', 'borrowerName')}
          {field('رقم تسجيله', 'borrowerRegistrationNumber')}
          {field('يمثلها', 'borrowerRepresentedBy')}
          {optionSelectField('نوع العنوان', 'borrowerAddressType', ADDRESS_TYPE_OPTIONS, form.borrowerAddressType ?? 'موطن مختار', (v) => set('borrowerAddressType', v))}
          <div className="md:col-span-2">{field(addressLabelOf(form.borrowerAddressType), 'borrowerAddress')}</div>
        </div>
      ) : (
        <>
          <div className="grid md:grid-cols-5 gap-4 mt-4">
            {field('الاسم', 'borrowerName')}
            {field('اسم الأب', 'borrowerFather')}
            {field('النسبة', 'borrowerFamily')}
            {field('اسم الأم', 'borrowerMother')}
            {field('مكان وتاريخ الولادة', 'borrowerBirth')}
            {field('مكان ورقم القيد', 'borrowerRegister')}
            {field('الرقم الوطني', 'borrowerNationalId')}
            {!borrowerHasHeirs && !borrowerRepStored && (
              <>
                {optionSelectField('نوع العنوان', 'borrowerAddressType', ADDRESS_TYPE_OPTIONS, form.borrowerAddressType ?? 'موطن مختار', (v) => set('borrowerAddressType', v))}
                <div className="md:col-span-2">{field(addressLabelOf(form.borrowerAddressType), 'borrowerAddress')}</div>
              </>
            )}
          </div>
          <HeirsEditor
            idPrefix="borrower"
            heirs={borrowerHeirs}
            onSet={onBorrowerHeirSet}
            onAdd={onBorrowerHeirAdd}
            onRemove={onBorrowerHeirRemove}
            locked={lock}
            allowAdd={!borrowerHasRep}
          />
          {!borrowerHasRep && !borrowerHasHeirRows && (
            <div className="mt-3 flex justify-end">
              <button
                type="button"
                onClick={onBorrowerRepActivate}
                className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
              >
                ＋ إضافة ممثل شرعي
              </button>
            </div>
          )}
          {!borrowerHasRep && borrowerHasHeirRows && (
            <p className="mt-3 text-xs font-medium text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2" role="note">
              لا يجتمع الممثل الشرعي مع الورثة — احذف الورثة لإضافة ممثل شرعي.
            </p>
          )}
          {borrowerHasRep && (
            <RepresentativeEditor
              idPrefix="borrower"
              mode="address"
              representative={{
                representativeName: form.borrowerRepresentativeName,
                representativeFather: form.borrowerRepresentativeFather,
                representativeFamily: form.borrowerRepresentativeFamily,
                representativeCapacity: form.borrowerRepresentativeCapacity,
                representativeAddressType: form.borrowerRepresentativeAddressType,
                representativeAddress: form.borrowerRepresentativeAddress,
              }}
              onSet={(key, value) => set(BORROWER_REP_KEYS[key] ?? 'borrowerRepresentativeName', value)}
              onRemove={onBorrowerRepRemove}
              locked={partyRepLocked('borrower')}
            />
          )}
        </>
      )}
    </>
  );
}
