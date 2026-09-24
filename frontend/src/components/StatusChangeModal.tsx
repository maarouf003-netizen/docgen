import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import { assetDisplayName } from '../utils/assetDisplay';
import { isAuctionableKind } from './form/documentFormConstants';
import type { AssetDto, DocumentResponse } from '../types';
import { FieldInput, SelectInput } from './form/FormInputs';
import MultiAmountEditor from './MultiAmountEditor';
import {
  EXEC_STATUS_DEFERRED,
  EXEC_STATUS_FORCIBLY,
  EXEC_STATUS_REFERRED_TO_START,
  EXEC_STATUS_SETTLED,
  EXEC_STATUS_STRUCK_OFF,
  STATUS_ACTION_COMPLETE_SALE,
  STATUS_ACTION_RETURN_CIRCULATING,
  STATUS_ACTION_RETURN_PARTIAL,
  STATUS_ACTION_REVERT,
  SUB_STATUS_FULL,
  SUB_STATUS_PARTIAL,
  allowedTargetsOf,
  currentStateOf,
  filterTargetsByPartial,
  isPartialExecSubStatus,
} from '../utils/documentStatus';

/** نمط التاريخ الحر المعتمد: «مثال: 1/8/2026» (يوم/شهر/سنة) لكل حقول التواريخ. */
const DATE_PLACEHOLDER = 'مثال: 1/8/2026';

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
  forcedExecutionDate: string;
  forcedTransferDate: string;
  forcedTransferNoticeNumber: string;
  collectedAmount?: number;
  collectedAmount2?: number;
  collectedAmount3?: number;
  collectedCurrency: string;
  collectedCurrency2: string;
  collectedCurrency3: string;
  struckOffDate: string;
  soldAssetIds: number[];
  noFundsDemandNumber: string;
  noFundsDemandDate: string;
  startReferralNumber: string;
  startReferralDate: string;
  renewalFileNumber: string;
  renewalFileType: string;
  renewalDate: string;
  renewalYear: string;
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
    execSubStatus: SUB_STATUS_FULL,
    forcedExecutionDate: '',
    forcedTransferDate: '',
    forcedTransferNoticeNumber: '',
    collectedCurrency: 'ليرة سورية',
    collectedCurrency2: 'دولار أمريكي',
    collectedCurrency3: 'يورو',
    struckOffDate: '',
    soldAssetIds: [],
    noFundsDemandNumber: '',
    noFundsDemandDate: '',
    startReferralNumber: '',
    startReferralDate: '',
    renewalFileNumber: '',
    renewalFileType: '',
    renewalDate: '',
    renewalYear: '',
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
  // «الملف المناب» (حالة الإنابة): لا يغيّر حالته بنفسه — مناب متداول يُشطب فحسب (C1)،
  // والموروث-تريث والمسترد بلا خيارات ورسالة L6 المفصلة، والحالة النهائية بلا مخرج.
  const isTarget = Boolean(doc.sourceDelegationId);
  // اللازمة (§2-10): «المنفذ جزئيا» على «محال» يحدد وجهة العودة (منفذ جزئيا)؛ والإحالة إلى
  // البداية من «منفذ جبريا» تخص الجزئي فحسب، أما من متداول/تريث فهي مفتوحة لهما دائمًا.
  const partial = isPartialExecSubStatus(doc.execSubStatus);
  const targets = filterTargetsByPartial(allowedTargetsOf(state, isTarget, partial), state, partial);
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
    const ids = fields.soldAssetIds.includes(id)
      ? fields.soldAssetIds.filter((x) => x !== id)
      : [...fields.soldAssetIds, id];
    set('soldAssetIds', ids);
  };

  const normalize = (s: string) => normalizeArabicDigits(s);

  const buildPayload = (): Record<string, string> => {
    const payload: Record<string, string> = {};
    if (target === EXEC_STATUS_DEFERRED) {
      if (!fields.tarithNumber.trim() || !fields.tarithDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب التريث على الأقل');
      }
      payload.tarithNumber = normalize(fields.tarithNumber);
      payload.tarithDate = normalize(fields.tarithDate);
      if (fields.tarithRegNumber) payload.tarithRegNumber = normalize(fields.tarithRegNumber);
      if (fields.tarithRegDate) payload.tarithRegDate = normalize(fields.tarithRegDate);
    } else if (target === EXEC_STATUS_SETTLED) {
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
    } else if (target === EXEC_STATUS_FORCIBLY) {
      payload.execSubStatus = fields.execSubStatus;
      if (!fields.forcedExecutionDate.trim()) {
        throw new Error('يجب إدخال تاريخ قرار الإحالة القطعية');
      }
      payload.forcedExecutionDate = normalize(fields.forcedExecutionDate);
      for (let i = 0; i < collectedSlots; i++) {
        const amount = fields[COLLECTED_AMOUNT_KEYS[i]];
        if (amount != null) {
          payload[COLLECTED_AMOUNT_KEYS[i]] = String(amount);
          payload[COLLECTED_CURRENCY_KEYS[i]] = fields[COLLECTED_CURRENCY_KEYS[i]];
        }
      }
      if (fields.soldAssetIds.length === 0) {
        throw new Error('اختر الأموال التي جرى بيعها بالمزاد العلني على الأقل');
      }
      payload.soldAssetIds = fields.soldAssetIds.join(',');
    } else if (target === STATUS_ACTION_COMPLETE_SALE) {
      if (!fields.forcedTransferDate.trim()) {
        throw new Error('يجب إدخال تاريخ تحويل بدل المبيع للجهة العامة');
      }
      payload.forcedTransferDate = normalize(fields.forcedTransferDate);
      if (fields.forcedTransferNoticeNumber.trim()) {
        payload.forcedTransferNoticeNumber = normalize(fields.forcedTransferNoticeNumber);
      }
    } else if (target === EXEC_STATUS_STRUCK_OFF) {
      if (!fields.struckOffDate.trim()) {
        throw new Error('يجب إدخال تاريخ الشطب');
      }
      payload.struckOffDate = normalize(fields.struckOffDate);
    } else if (target === STATUS_ACTION_REVERT) {
      if (!fields.sayerNumber.trim() || !fields.sayerDate.trim()
        || !fields.sayerRegNumber.trim() || !fields.sayerRegDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب الجهة العامة بالسير بالملف وورودهما');
      }
      payload.sayerNumber = normalize(fields.sayerNumber);
      payload.sayerDate = normalize(fields.sayerDate);
      payload.sayerRegNumber = normalize(fields.sayerRegNumber);
      payload.sayerRegDate = normalize(fields.sayerRegDate);
    } else if (target === EXEC_STATUS_REFERRED_TO_START) {
      // إلزاميّا الدخول: كتاب المطالعة بعدم وجود أموال (رقم + تاريخ)؛ كتاب الإحالة اختياري.
      if (!fields.noFundsDemandNumber.trim() || !fields.noFundsDemandDate.trim()) {
        throw new Error('يجب إدخال رقم وتاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها');
      }
      payload.noFundsDemandNumber = normalize(fields.noFundsDemandNumber);
      payload.noFundsDemandDate = normalize(fields.noFundsDemandDate);
      if (fields.startReferralNumber.trim()) payload.startReferralNumber = normalize(fields.startReferralNumber);
      if (fields.startReferralDate.trim()) payload.startReferralDate = normalize(fields.startReferralDate);
    } else if (
      target === STATUS_ACTION_RETURN_CIRCULATING ||
      target === STATUS_ACTION_RETURN_PARTIAL
    ) {
      // العودتان بنفس نقطة return-referred-to-start — التجديد موحّد (قرار 12): إن أُدخل
      // رقم جديد وجب معه تاريخ التجديد وسنة الإعادة، ولو تُرك فارغًا لم يكن هناك تجديد.
      const renewalNumber = fields.renewalFileNumber.trim();
      if (renewalNumber) {
        if (!fields.renewalDate.trim() || !fields.renewalYear.trim()) {
          throw new Error('عند إدخال رقم ملف جديد يجب إدخال تاريخ التجديد وسنة الإعادة معًا');
        }
        payload.renewalFileNumber = normalize(renewalNumber);
        payload.renewalDate = normalize(fields.renewalDate);
        payload.renewalYear = normalize(fields.renewalYear);
        if (fields.renewalFileType.trim()) payload.renewalFileType = normalize(fields.renewalFileType);
      }
      // شطب الملف السابق (اختياري) — لعودة-المتداول فقط (الخادم يتجاهله في عودة-جزئيا).
      if (target === STATUS_ACTION_RETURN_CIRCULATING && fields.struckOffDate.trim()) {
        payload.struckOffDate = normalize(fields.struckOffDate);
      }
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
      if (target === STATUS_ACTION_REVERT) {
        await api.post(`/documents/${doc.id}/revert-status`, { fields: payload });
      } else if (target === STATUS_ACTION_COMPLETE_SALE) {
        await api.post(`/documents/${doc.id}/consider-executed-by-delegation`, {
          fields: payload,
        });
      } else if (
        target === STATUS_ACTION_RETURN_CIRCULATING ||
        target === STATUS_ACTION_RETURN_PARTIAL
      ) {
        // العودتان من «محال الى البداية» تحملان نفس النقطة — الخادم يوجّه اللازمة بنفسه.
        // تقبض النقطة جسمًا سطحيًا (ReturnReferredToStartRequest: RenewalRequest) لا
        // { fields }، فعلى الحقول أن تأتي جذرية، وسنة الإعادة رقمًا (System.Text.Json
        // الصارم لا يحوّل نصًا إلى int؟).
        const returnBody =
          payload.renewalYear !== undefined
            ? { ...payload, renewalYear: Number(payload.renewalYear) }
            : payload;
        await api.post(`/documents/${doc.id}/return-referred-to-start`, returnBody);
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
            <p className="font-medium text-gray-800">
              {state}
              {state === EXEC_STATUS_REFERRED_TO_START && isPartialExecSubStatus(doc.execSubStatus)
                ? ` (${SUB_STATUS_PARTIAL})`
                : ''}
            </p>
          </div>

          {targets.length === 0 ? (
            <p className="text-gray-600 text-sm">
              {isTarget
                ? 'لا توجد حالات متاحة — حالة الملف المناب تلحق حالة الملف المنيب في اعتباره منفذ أو تريث'
                : `الملف في حالة «${state}»؛ الإعادة من المشطوب تتم من صفحة «الملفات المشطوبة».`}
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

              {target === EXEC_STATUS_DEFERRED && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <FieldInput id="tarithNumber" label="رقم كتاب التريث" value={fields.tarithNumber} onChange={(v) => set('tarithNumber', v)} />
                  <FieldInput id="tarithDate" label="تاريخ كتاب التريث" value={fields.tarithDate} onChange={(v) => set('tarithDate', v)} placeholder={DATE_PLACEHOLDER} />
                  <FieldInput id="tarithRegNumber" label="رقم ورود كتاب التريث" value={fields.tarithRegNumber} onChange={(v) => set('tarithRegNumber', v)} />
                  <FieldInput id="tarithRegDate" label="تاريخ ورود كتاب التريث" value={fields.tarithRegDate} onChange={(v) => set('tarithRegDate', v)} placeholder={DATE_PLACEHOLDER} />
                </div>
              )}

              {target === EXEC_STATUS_SETTLED && (
                <div className="grid gap-4">
                  <div className="grid sm:grid-cols-2 gap-3">
                    <FieldInput id="baraetNumber" label="رقم كتاب براءة الذمة" value={fields.baraetNumber} onChange={(v) => set('baraetNumber', v)} />
                    <FieldInput id="baraetDate" label="تاريخ كتاب براءة الذمة" value={fields.baraetDate} onChange={(v) => set('baraetDate', v)} placeholder={DATE_PLACEHOLDER} />
                    <FieldInput id="baraetRegNumber" label="رقم ورود كتاب براءة الذمة" value={fields.baraetRegNumber} onChange={(v) => set('baraetRegNumber', v)} />
                    <FieldInput id="baraetRegDate" label="تاريخ ورود كتاب براءة الذمة" value={fields.baraetRegDate} onChange={(v) => set('baraetRegDate', v)} placeholder={DATE_PLACEHOLDER} />
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

              {target === EXEC_STATUS_FORCIBLY && (
                <div className="grid gap-4">
                  <SelectInput
                    id="execSubStatus"
                    label="نوع التنفيذ"
                    value={fields.execSubStatus}
                    onChange={(v) => set('execSubStatus', v)}
                    options={[SUB_STATUS_PARTIAL, SUB_STATUS_FULL]}
                  />
                  <FieldInput
                    id="forcedExecutionDate"
                    label="تاريخ قرار الإحالة القطعية"
                    value={fields.forcedExecutionDate}
                    onChange={(v) => set('forcedExecutionDate', v)}
                    placeholder={DATE_PLACEHOLDER}
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
                      الأموال المباعة بالمزاد العلني
                    </p>
                    {(() => {
                      const auctionable = (doc.assets ?? []).filter(
                        (r): r is AssetDto & { id: number } => r.id != null && isAuctionableKind(r.assetKind),
                      );
                      return auctionable.length === 0 ? (
                        <p className="text-gray-400 text-sm">لا توجد أموال قابلة للبيع في الملف</p>
                      ) : (
                        <div className="border border-gray-300 rounded-lg p-3 space-y-2">
                          {auctionable.map((r) => {
                            const checked = fields.soldAssetIds.includes(r.id);
                            return (
                              <label key={r.id} className="flex items-center gap-2 min-h-11 cursor-pointer">
                                <input
                                  type="checkbox"
                                  checked={checked}
                                  onChange={() => toggleEstate(r.id)}
                                  className="w-4 h-4"
                                />
                                <span className="text-sm text-gray-800">{assetDisplayName(r)}</span>
                              </label>
                            );
                          })}
                        </div>
                      );
                    })()}
                  </div>
                </div>
              )}

              {target === STATUS_ACTION_COMPLETE_SALE && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <FieldInput
                    id="forcedTransferDate"
                    label="تاريخ تحويل بدل المبيع للجهة العامة"
                    value={fields.forcedTransferDate}
                    onChange={(v) => set('forcedTransferDate', v)}
                    placeholder={DATE_PLACEHOLDER}
                  />
                  <FieldInput
                    id="forcedTransferNoticeNumber"
                    label="رقم إشعار التحويل (اختياري)"
                    value={fields.forcedTransferNoticeNumber}
                    onChange={(v) => set('forcedTransferNoticeNumber', v)}
                  />
                </div>
              )}

              {target === EXEC_STATUS_STRUCK_OFF && (
                <FieldInput id="struckOffDate" label="تاريخ الشطب" value={fields.struckOffDate} onChange={(v) => set('struckOffDate', v)} placeholder={DATE_PLACEHOLDER} />
              )}

              {target === STATUS_ACTION_REVERT && (
                <div className="grid sm:grid-cols-2 gap-3">
                  <FieldInput id="sayerNumber" label="رقم كتاب الجهة العامة بالسير بالملف" value={fields.sayerNumber} onChange={(v) => set('sayerNumber', v)} />
                  <FieldInput id="sayerDate" label="تاريخ كتاب الجهة العامة بالسير بالملف" value={fields.sayerDate} onChange={(v) => set('sayerDate', v)} placeholder={DATE_PLACEHOLDER} />
                  <FieldInput id="sayerRegNumber" label="رقم ورود كتاب بالسير بالملف" value={fields.sayerRegNumber} onChange={(v) => set('sayerRegNumber', v)} />
                  <FieldInput id="sayerRegDate" label="تاريخ ورود كتاب بالسير بالملف" value={fields.sayerRegDate} onChange={(v) => set('sayerRegDate', v)} placeholder={DATE_PLACEHOLDER} />
                </div>
              )}

              {target === EXEC_STATUS_REFERRED_TO_START && (
                <div className="grid gap-4">
                  <div className="grid sm:grid-cols-2 gap-3">
                    <FieldInput
                      id="noFundsDemandNumber"
                      label="رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها"
                      value={fields.noFundsDemandNumber}
                      onChange={(v) => set('noFundsDemandNumber', v)}
                    />
                    <FieldInput
                      id="noFundsDemandDate"
                      label="تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها"
                      value={fields.noFundsDemandDate}
                      onChange={(v) => set('noFundsDemandDate', v)}
                      placeholder={DATE_PLACEHOLDER}
                    />
                    <FieldInput
                      id="startReferralNumber"
                      label="رقم كتاب الإحالة (اختياري)"
                      value={fields.startReferralNumber}
                      onChange={(v) => set('startReferralNumber', v)}
                    />
                    <FieldInput
                      id="startReferralDate"
                      label="تاريخ كتاب الإحالة (اختياري)"
                      value={fields.startReferralDate}
                      onChange={(v) => set('startReferralDate', v)}
                      placeholder={DATE_PLACEHOLDER}
                    />
                  </div>
                  {(() => {
                    const auctionable = (doc.assets ?? []).filter(
                      (r): r is AssetDto & { id: number } => r.id != null && isAuctionableKind(r.assetKind),
                    );
                    return auctionable.length === 0 ? null : (
                      <div
                        role="note"
                        className="rounded-lg bg-amber-50 border border-amber-200 px-3 py-2 text-sm text-amber-800"
                      >
                        وجد في الملف أموال قابلة للبيع بالمزاد العلني — تأكد من عدم وجود أموال
                        للتنفيذ عليها قبل الإحالة إلى البداية.
                      </div>
                    );
                  })()}
                </div>
              )}

              {(target === STATUS_ACTION_RETURN_CIRCULATING ||
                target === STATUS_ACTION_RETURN_PARTIAL) && (
                <div className="grid gap-4">
                  <div
                    role="note"
                    className="rounded-lg bg-purple-50 border border-purple-200 px-3 py-2 text-sm text-purple-800"
                  >
                    {target === STATUS_ACTION_RETURN_CIRCULATING
                      ? 'أعيد السير بالملف إلى المتداول بعد موافاة قسم التنفيذ بأموال للتنفيذ عليها.'
                      : 'يعود الملف إلى «منفذ جبريا» محافظًا على المبلغ المحصل العائد — أكمل بيانات التنفيذ الجبري عند الحاجة.'}
                  </div>
                  <div className="grid sm:grid-cols-2 gap-3">
                    <FieldInput
                      id="renewalFileNumber"
                      label="رقم الملف الجديد (اختياري)"
                      value={fields.renewalFileNumber}
                      onChange={(v) => set('renewalFileNumber', v)}
                    />
                    <FieldInput
                      id="renewalYear"
                      label="سنة الإعادة (اختياري)"
                      value={fields.renewalYear}
                      onChange={(v) => set('renewalYear', v)}
                      placeholder="مثال: 2026"
                    />
                    <FieldInput
                      id="renewalDate"
                      label="تاريخ التجديد (اختياري)"
                      value={fields.renewalDate}
                      onChange={(v) => set('renewalDate', v)}
                      placeholder={DATE_PLACEHOLDER}
                    />
                    <FieldInput
                      id="renewalFileType"
                      label="نوع الملف الجديد (اختياري)"
                      value={fields.renewalFileType}
                      onChange={(v) => set('renewalFileType', v)}
                    />
                    {target === STATUS_ACTION_RETURN_CIRCULATING && (
                      <FieldInput
                        id="struckOffDate"
                        label="تاريخ شطب الملف السابق (اختياري)"
                        value={fields.struckOffDate}
                        onChange={(v) => set('struckOffDate', v)}
                        placeholder={DATE_PLACEHOLDER}
                      />
                    )}
                  </div>
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
