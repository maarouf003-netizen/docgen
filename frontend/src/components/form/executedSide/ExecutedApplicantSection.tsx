import type { ExecutedHeirDto, ExecutionApplicantDto, PartyNature } from '../../../types';
import {
  ADDRESS_TYPE_OPTIONS,
  PARTY_NATURE_OPTIONS,
  REPRESENTATION_TYPES,
  addressLabelOf,
  hasRepresentative,
  hasStoredRepresentative,
} from '../documentFormConstants';
import { ExecutedHeirsEditor } from '../HeirsEditors';
import { RepresentativeEditor } from '../RepresentativeEditor';
import { FormSectionTitle } from '../FormSectionTitle';

interface ExecutedApplicantSectionProps {
  isDeposit: boolean;
  isMirror: boolean;
  partyRepLocked: (key: string) => boolean;
  executionApplicants: ExecutionApplicantDto[];
  onApplicantSet: (i: number, key: keyof ExecutionApplicantDto, value: string) => void;
  onApplicantAdd: (nature?: PartyNature) => void;
  onApplicantRemove: (i: number) => void;
  onApplicantHeirSet: (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) => void;
  onApplicantHeirAdd: (i: number) => void;
  onApplicantHeirRemove: (i: number, hi: number) => void;
  onApplicantRepActivate: (i: number) => void;
  onApplicantRepRemove: (i: number) => void;
}

/** بطاقات طالب التنفيذ/العرض (طبيعي/اعتباري) مع الورثة والممثل الشرعي. */
export function ExecutedApplicantSection({
  isDeposit,
  isMirror,
  partyRepLocked,
  executionApplicants,
  onApplicantSet,
  onApplicantAdd,
  onApplicantRemove,
  onApplicantHeirSet,
  onApplicantHeirAdd,
  onApplicantHeirRemove,
  onApplicantRepActivate,
  onApplicantRepRemove,
}: ExecutedApplicantSectionProps) {
  const applicantLabel = isDeposit ? 'طالب العرض' : 'طالب التنفيذ';
  const applicantButtonLabel = isDeposit ? 'طالب عرض' : 'طالب التنفيذ';

  return (
    <>
      <FormSectionTitle title={`👤 ${applicantLabel}`} />
      {executionApplicants.map((a, i) => {
        const aHasRep = hasRepresentative(a);
        // الحقول التي تُصفَّر في الحمولة (الوكيل القانوني) تتبع «المخزَّن» لا «المفعَّل»:
        // صفة وحدها (بلا اسم) لا تُحفظ، فمسح/إخفاء قيمة لها فقدان صامت.
        const aRepStored = hasStoredRepresentative(a);
        const hasEstate = a.representationType === 'إضافة لتركة' || a.representationType === 'أصالة وإضافة';
        const aIsLegal = a.nature === 'legal';
        return (
          <div key={i} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center mb-3">
              <span className="font-medium text-gray-700 text-sm">{applicantLabel} {i + 1}</span>
              {!isMirror && executionApplicants.length > 1 && (
                <button type="button" onClick={() => onApplicantRemove(i)} className="text-red-500 text-xs hover:underline min-h-11">
                  ✖ حذف
                </button>
              )}
            </div>
            <div className="grid md:grid-cols-3 gap-3 mb-3">
              <select
                aria-label="نوع الطرف"
                disabled={isMirror}
                value={a.nature ?? 'natural'}
                onChange={(e) => onApplicantSet(i, 'nature', e.target.value)}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed"
              >
                {PARTY_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            {aIsLegal ? (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الشخص الاعتباري</label>
                  <input
                    aria-label="الشخص الاعتباري"
                    value={a.name ?? ''}
                    readOnly={isMirror}
                    aria-readonly={isMirror || undefined}
                    placeholder="اسم الشخص الاعتباري (شركة خاصة)…"
                    onChange={(e) => onApplicantSet(i, 'name', e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">رقم تسجيله</label>
                  <input aria-label="رقم تسجيله" value={a.registrationNumber ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, 'registrationNumber', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">يمثلها</label>
                  <input aria-label="يمثلها" value={a.representedBy ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, 'representedBy', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                  <select value={a.addressType ?? 'موطن مختار'} disabled={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, 'addressType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed">
                    {ADDRESS_TYPE_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(a.addressType)}</label>
                  <input aria-label="العنوان" value={a.address ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            ) : (
              <>
                <div className="grid md:grid-cols-3 gap-3">
                  {([['name', 'الاسم'], ['father', 'اسم الأب'], ['family', 'النسبة']] as const).map(([k, label]) => (
                    <div key={k}>
                      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                      <input value={a[k] ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  ))}
                </div>
                <div className="grid md:grid-cols-3 gap-3 mt-3">
                  {!aRepStored && (
                    <div className="md:col-span-2">
                      <label className="block text-xs font-bold text-gray-600 mb-1">الوكيل القانوني</label>
                      <input value={a.legalRepresentative ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, 'legalRepresentative', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  )}
                  <div className={aRepStored ? 'md:col-span-3' : undefined}>
                    <label className="block text-xs font-bold text-gray-600 mb-1">نوع التمثيل</label>
                    <div className="flex items-center gap-2">
                      <select value={a.representationType ?? 'أصالة'} disabled={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, 'representationType', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed">
                        {REPRESENTATION_TYPES.map((o) => (
                          <option key={o}>{o}</option>
                        ))}
                      </select>
                      {!aHasRep && (
                        <button
                          type="button"
                          onClick={() => onApplicantRepActivate(i)}
                          className="shrink-0 bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                        >
                          ＋ إضافة ممثل شرعي
                        </button>
                      )}
                    </div>
                  </div>
                </div>
                {hasEstate && (
                  <div className="mt-4 rounded-lg bg-white border border-gray-200 p-4 grid md:grid-cols-3 gap-3">
                    {([['deceasedName', 'اسم المورث المتوفى'], ['deceasedFather', 'اسم أب المورث'], ['deceasedFamily', 'نسبة المورث']] as const).map(([k, label]) => (
                      <div key={k}>
                        <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
<input value={a[k] ?? ''} readOnly={isMirror} aria-readonly={isMirror || undefined} onChange={(e) => onApplicantSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                      </div>
                    ))}
                    <div className="md:col-span-3">
                      <ExecutedHeirsEditor
                        idPrefix={`applicant-${i}`}
                        heirs={a.heirs ?? []}
                        onSet={(hi, k, v) => onApplicantHeirSet(i, hi, k, v)}
                        onAdd={() => onApplicantHeirAdd(i)}
                        onRemove={(hi) => onApplicantHeirRemove(i, hi)}
                        locked={isMirror}
                      />
                    </div>
                  </div>
                )}
                {aHasRep && (
                  <RepresentativeEditor
                    idPrefix={`applicant-${i}`}
                    mode="legalRep"
                    representative={a}
                    onSet={(key, value) => onApplicantSet(i, key as keyof ExecutionApplicantDto, value)}
                    onRemove={() => onApplicantRepRemove(i)}
                    locked={partyRepLocked(`applicant-${i}`)}
                  />
                )}
              </>
            )}
          </div>
        );
      })}
      {!isMirror && (
        <div className="flex flex-wrap gap-3 items-center">
          <button
            type="button"
            onClick={() => onApplicantAdd('natural')}
            className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة {applicantButtonLabel} (شخص طبيعي)
          </button>
          <button
            type="button"
            onClick={() => onApplicantAdd('legal')}
            className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            ＋ إضافة {applicantButtonLabel} (شخص اعتباري)
          </button>
        </div>
      )}
    </>
  );
}
