import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import type { DocumentResponse, RealEstateDto } from '../types';
import { FieldInput, SelectInput } from './form/FormInputs';
import MultiAmountEditor from './MultiAmountEditor';

/** الحالة الحالية لنظام «طالبة تنفيذ» (مطابقة لآلة الحالات في الخلفية). */
function currentStateOf(doc: DocumentResponse): string {
  if (doc.execStatus === 'مشطوب' || doc.executedStatus === 'مشطوب') return 'مشطوب';
  if (doc.execStatus === 'تريث') return 'تريث';
  if (doc.execStatus === 'منفذ بالتسوية') return 'منفذ بالتسوية';
  if (doc.execStatus === 'منفذ جبريا') return 'منفذ جبريا';
  return doc.isDraft ? 'تحت رفع' : 'متداول';
}

/** الانتقالات المسموحة من الحالة الحالية عبر نافذة «تغيير الحالة» (المتداول يُسجَّل من التعديل). */
function allowedTargetsOf(state: string): string[] {
  switch (state) {
    case 'تحت رفع':
      return ['تريث', 'منفذ بالتسوية'];
    case 'متداول':
      return ['تريث', 'منفذ بالتسوية', 'منفذ جبريا', 'مشطوب'];
    case 'تريث':
      return ['منفذ بالتسوية', 'تراجع'];
    case 'منفذ بالتسوية':
      return ['تراجع'];
    case 'منفذ جبريا':
      return ['تراجع'];
    default:
      return [];
  }
}

const COLLECTED_AMOUNT_KEYS = ['collectedAmount', 'collectedAmount2', 'collectedAmount3'] as const;
const COLLECTED_CURRENCY_KEYS = ['collectedCurrency', 'collectedCurrency2', 'collectedCurrency3'] as const;

type StatusFields = {
  tarithNumber: string;
  tarithDate: string;
  tarithRegNumber: string;
  tarithRegDate: string;
  baraetNumber: string;
  baraetDate: string;
  baraetRegNumber: string;
  baraetRegDate: string;
  sayerNumber: string;
  sayerDate: string;
  sayerRegNumber: string;
  sayerRegDate: string;
  execSubStatus: string;
  collectedAmount?: number;
  collectedAmount2?: number;
  collectedAmount3?: number;
  collectedCurrency: string;
  collectedCurrency2: string;
  collectedCurrency3: string;
  struckOffDate: string;
  soldEstateIds: number[];
};

function emptyFields(): StatusFields {
  return {
    tarithNumber: '',
    tarithDate: '',
    tarithRegNumber: '',
    tarithRegDate: '',
    baraetNumber: '',
    baraetDate: '',
    baraetRegNumber: '',
    baraetRegDate: '',
    sayerNumber: '',
    sayerDate: '',
    sayerRegNumber: '',
    sayerRegDate: '',
    execSubStatus: 'منفذ كاملا',
    collectedCurrency: 'ليرة سورية',
    collectedCurrency2: 'دولار أمريكي',
    collectedCurrency3: 'يورو',
    struckOffDate: '',
    soldEstateIds: [],
  };
}

export default function StatusChangeModal({
  doc,
  onClose,
  onChanged,
}: {
  doc: DocumentResponse;
  onClose: () => void;
  onChanged: () => void;
}) {
  const state = currentStateOf(doc);
  const targets = allowedTargetsOf(state);
  const [target, setTarget] = useState<string>(targets[0] ?? '');
  const [fields, setFields] = useState<StatusFields>(emptyFields());
  const [collectedSlots, setCollectedSlots] = useState(1);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setTarget(targets[0] ?? '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  const set = <K extends keyof StatusFields>(key: K, value: StatusFields[K]) =>
    setFields((f) => ({ ...f, [key]: value }));

  const toggleEstate = (id: number) => {
    const ids = fields.soldEstateIds.includes(id)
      ? fields.soldEstateIds.filter((x) => x !== id)
      : [...fields.soldEstateIds, id];
    set('soldEstateIds', ids);
  };

  const normalize = (s: string) => normalizeArabicDigits(s);

  const buildPayload = (): Record<string, string> => {
    const payload: Record<string, string> = {};
    if (target === 'تريث') {
      if (!fields.tarithNumber.trim() || !fields.tarithDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب التريث على الأقل');
      }
      payload.tarithNumber = normalize(fields.tarithNumber);
      payload.tarithDate = normalize(fields.tarithDate);
      if (fields.tarithRegNumber) payload.tarithRegNumber = normalize(fields.tarithRegNumber);
      if (fields.tarithRegDate) payload.tarithRegDate = normalize(fields.tarithRegDate);
    } else if (target === 'منفذ بالتسوية') {
      if (!fields.baraetNumber.trim() || !fields.baraetDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب براءة الذمة على الأقل');
      }
      payload.baraetNumber = normalize(fields.baraetNumber);
      payload.baraetDate = normalize(fields.baraetDate);
      if (fields.baraetRegNumber) payload.baraetRegNumber = normalize(fields.baraetRegNumber);
      if (fields.baraetRegDate) payload.baraetRegDate = normalize(fields.baraetRegDate);
      for (let i = 0; i < collectedSlots; i++) {
        const amount = fields[COLLECTED_AMOUNT_KEYS[i]];
        if (amount != null) {
          payload[COLLECTED_AMOUNT_KEYS[i]] = String(amount);
          payload[COLLECTED_CURRENCY_KEYS[i]] = fields[COLLECTED_CURRENCY_KEYS[i]];
        }
      }
    } else if (target === 'منفذ جبريا') {
      payload.execSubStatus = fields.execSubStatus;
      for (let i = 0; i < collectedSlots; i++) {
        const amount = fields[COLLECTED_AMOUNT_KEYS[i]];
        if (amount != null) {
          payload[COLLECTED_AMOUNT_KEYS[i]] = String(amount);
          payload[COLLECTED_CURRENCY_KEYS[i]] = fields[COLLECTED_CURRENCY_KEYS[i]];
        }
      }
      if (fields.soldEstateIds.length === 0) {
        throw new Error('اختر العقارات التي جرى بيعها بالمزاد العلني على الأقل');
      }
      payload.soldEstateIds = fields.soldEstateIds.join(',');
    } else if (target === 'مشطوب') {
      if (!fields.struckOffDate.trim()) {
        throw new Error('يجب إدخال تاريخ الشطب');
      }
      payload.struckOffDate = normalize(fields.struckOffDate);
    } else if (target === 'تراجع') {
      if (!fields.sayerNumber.trim() || !fields.sayerDate.trim()
        || !fields.sayerRegNumber.trim() || !fields.sayerRegDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب الجهة العامة بالسير بالملف وورودهما');
      }
      payload.sayerNumber = normalize(fields.sayerNumber);
      payload.sayerDate = normalize(fields.sayerDate);
      payload.sayerRegNumber = normalize(fields.sayerRegNumber);
      payload.sayerRegDate = normalize(fields.sayerRegDate);
    }
    return payload;
  };

  const submit = async () => {
    setError('');
    let payload: Record<string, string>;
    try {
      payload = buildPayload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'بيانات غير مكتملة');
      return;
    }
    setBusy(true);
    try {
      if (target === 'تراجع') {
        await api.post(`/documents/${doc.id}/revert-status`, { fields: payload });
      } else {
        await api.post(`/documents/${doc.id}/status`, { status: target, fields: payload });
      }
      onChanged();
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="تغيير الحالة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">تغيير الحالة</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 mb-4">
            <p className="text-xs text-gray-500 mb-1">الحالة الحالية</p>
            <p className="font-medium text-gray-800">{state}</p>
          </div>

          {targets.length === 0 ? (
            <p className="text-gray-600 text-sm">
              الملف في حالة «{state}»؛ الإعادة من المشطوب تتم من صفحة «الملفات المشطوبة».
            </p>
          ) : (
            <>
              {error && <p className="text-red-600 text-sm mb-3">{error}</p>}

              <div className="mb-4">
                <SelectInput
                  id="status-target"
                  label="الإجراء"
                  value={target}
                  onChange={setTarget}
                  options={targets}
                />
              </div>

              {target === 'تريث' && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <FieldInput id="tarithNumber" label="رقم كتاب التريث" value={fields.tarithNumber} onChange={(v) => set('tarithNumber', v)} />
                  <FieldInput id="tarithDate" label="تاريخ كتاب التريث" value={fields.tarithDate} onChange={(v) => set('tarithDate', v)} />
                  <FieldInput id="tarithRegNumber" label="رقم ورود كتاب التريث" value={fields.tarithRegNumber} onChange={(v) => set('tarithRegNumber', v)} />
                  <FieldInput id="tarithRegDate" label="تاريخ ورود كتاب التريث" value={fields.tarithRegDate} onChange={(v) => set('tarithRegDate', v)} />
                </div>
              )}

              {target === 'منفذ بالتسوية' && (
                <div className="grid gap-4">
                  <div className="grid sm:grid-cols-2 gap-3">
                    <FieldInput id="baraetNumber" label="رقم كتاب براءة الذمة" value={fields.baraetNumber} onChange={(v) => set('baraetNumber', v)} />
                    <FieldInput id="baraetDate" label="تاريخ كتاب براءة الذمة" value={fields.baraetDate} onChange={(v) => set('baraetDate', v)} />
                    <FieldInput id="baraetRegNumber" label="رقم ورود كتاب براءة الذمة" value={fields.baraetRegNumber} onChange={(v) => set('baraetRegNumber', v)} />
                    <FieldInput id="baraetRegDate" label="تاريخ ورود كتاب براءة الذمة" value={fields.baraetRegDate} onChange={(v) => set('baraetRegDate', v)} />
                  </div>
                  <MultiAmountEditor
                    idPrefix="status-collected"
                    amountKeys={COLLECTED_AMOUNT_KEYS}
                    currencyKeys={COLLECTED_CURRENCY_KEYS}
                    values={fields}
                    onSet={(key, value) => set(key as keyof StatusFields, value as never)}
                    slots={collectedSlots}
                    onSlotsChange={setCollectedSlots}
                    firstLabel="المبلغ المحصل"
                    otherLabel={(i) => `المبلغ المحصل ${i + 1}`}
                  />
                </div>
              )}

              {target === 'منفذ جبريا' && (
                <div className="grid gap-4">
                  <SelectInput
                    id="execSubStatus"
                    label="نوع التنفيذ"
                    value={fields.execSubStatus}
                    onChange={(v) => set('execSubStatus', v)}
                    options={['منفذ جزئيا', 'منفذ كاملا']}
                  />
                  <MultiAmountEditor
                    idPrefix="status-collected"
                    amountKeys={COLLECTED_AMOUNT_KEYS}
                    currencyKeys={COLLECTED_CURRENCY_KEYS}
                    values={fields}
                    onSet={(key, value) => set(key as keyof StatusFields, value as never)}
                    slots={collectedSlots}
                    onSlotsChange={setCollectedSlots}
                    firstLabel="المبلغ المحصل"
                    otherLabel={(i) => `المبلغ المحصل ${i + 1}`}
                  />
                  <div>
                    <p className="block text-xs font-bold text-gray-600 mb-1">
                      العقارات المباعة بالمزاد العلني
                    </p>
                    {(doc.realEstates ?? []).filter((r) => r.id != null).length === 0 ? (
                      <p className="text-gray-400 text-sm">لا توجد عقارات مدخلة في الملف</p>
                    ) : (
                      <div className="border border-gray-300 rounded-lg p-3 space-y-2">
                        {doc.realEstates.filter((r): r is RealEstateDto & { id: number } => r.id != null).map((r) => {
                          const label = r.property ? `عقار ${r.property}${r.propertyNumber ? ` (رقم ${r.propertyNumber})` : ''}` : `عقار ${r.id}`;
                          const checked = fields.soldEstateIds.includes(r.id);
                          return (
                            <label key={r.id} className="flex items-center gap-2 min-h-11 cursor-pointer">
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={() => toggleEstate(r.id)}
                                className="w-4 h-4"
                              />
                              <span className="text-sm text-gray-800">{label}</span>
                            </label>
                          );
                        })}
                      </div>
                    )}
                  </div>
                </div>
              )}

              {target === 'مشطوب' && (
                <FieldInput id="struckOffDate" label="تاريخ الشطب" value={fields.struckOffDate} onChange={(v) => set('struckOffDate', v)} />
              )}

              {target === 'تراجع' && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <FieldInput id="sayerNumber" label="رقم كتاب الجهة العامة بالسير بالملف" value={fields.sayerNumber} onChange={(v) => set('sayerNumber', v)} />
                  <FieldInput id="sayerDate" label="تاريخ كتاب الجهة العامة بالسير بالملف" value={fields.sayerDate} onChange={(v) => set('sayerDate', v)} />
                  <FieldInput id="sayerRegNumber" label="رقم ورود كتاب بالسير بالملف" value={fields.sayerRegNumber} onChange={(v) => set('sayerRegNumber', v)} />
                  <FieldInput id="sayerRegDate" label="تاريخ ورود كتاب بالسير بالملف" value={fields.sayerRegDate} onChange={(v) => set('sayerRegDate', v)} />
                </div>
              )}

              <div className="mt-5 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={onClose}
                  className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                >
                  إلغاء
                </button>
                <button
                  type="button"
                  onClick={submit}
                  disabled={busy}
                  className="bg-blue-700 hover:bg-blue-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  {busy ? 'جارِ الحفظ...' : 'حفظ الحالة'}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
