import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { getNumberReuseWarning, type NumberReuseWarning } from '../api/numberWarning';
import NumberReuseWarningModal from './NumberReuseWarningModal';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import { StatusChangeTargetFields } from './StatusChangeTargetFields';
import { COLLECTED_AMOUNT_KEYS, COLLECTED_CURRENCY_KEYS, emptyFields } from './statusChangeModel';
import type { StatusFields } from './statusChangeModel';
import { useCurrentYear } from '../hooks/useCurrentYear';
import { FUTURE_FILE_YEAR_MESSAGE, isFutureFileYear } from './form/documentFormConstants';
import type { DocumentResponse } from '../types';
import { SelectInput } from './form/FormInputs';
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
  SUB_STATUS_PARTIAL,
  allowedTargetsOf,
  currentStateOf,
  filterTargetsByPartial,
  isPartialExecSubStatus,
} from '../utils/documentStatus';

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
  const [numberWarning, setNumberWarning] = useState<NumberReuseWarning | null>(null);

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
  // سقف سنة الإعادة (تصبح سنة الملف): الجارية خادميًا — المستقبل مرفوض خلفيةً أيضًا.
  const { currentYear } = useCurrentYear();

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
        if (isFutureFileYear(normalize(fields.renewalYear), currentYear)) {
          throw new Error(FUTURE_FILE_YEAR_MESSAGE);
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

  const submit = async (warningToken?: string) => {
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
      // عدّاد التزامن المتفائل (RF-010): يُرسَل مع كل انتقال — غيابه توافق (بلا فحص مبكر).
      const version = doc.version ?? null;
      const fields = warningToken ? { ...payload, warningToken } : payload;
      if (target === STATUS_ACTION_REVERT) {
        await api.post(`/documents/${doc.id}/revert-status`, { fields, version });
      } else if (target === STATUS_ACTION_COMPLETE_SALE) {
        await api.post(`/documents/${doc.id}/consider-executed-by-delegation`, {
          fields,
          version,
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
            ? { ...payload, renewalYear: Number(payload.renewalYear), version,
                ...(warningToken ? { warningToken } : {}) }
            : { ...payload, version, ...(warningToken ? { warningToken } : {}) };
        await api.post(`/documents/${doc.id}/return-referred-to-start`, returnBody);
      } else {
        await api.post(`/documents/${doc.id}/status`, { status: target, fields, version });
      }
      setNumberWarning(null);
      onChanged();
      onClose();
    } catch (err) {
      const reuse = getNumberReuseWarning(err);
      if (reuse) setNumberWarning(reuse);
      else setError(getApiErrorMessage(err));
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

              <StatusChangeTargetFields
                target={target}
                fields={fields}
                set={set}
                collectedSlots={collectedSlots}
                setCollectedSlots={setCollectedSlots}
                doc={doc}
                toggleEstate={toggleEstate}
              />

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
                  onClick={() => void submit()}
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

      {numberWarning && (
        <NumberReuseWarningModal
          message={numberWarning.message}
          busy={busy}
          onCancel={() => setNumberWarning(null)}
          onConfirm={() => {
            const token = numberWarning.warningToken;
            setNumberWarning(null);
            void submit(token);
          }}
        />
      )}
    </div>
  );
}
