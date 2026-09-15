import { useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useCurrentYear } from '../hooks/useCurrentYear';
import { displayFileNumber, isExecutedLike } from '../utils/documentDisplay';
import { trimNull } from '../utils/serialization';
import type { DocumentResponse } from '../types';
import { RenewalFields, type RenewalFieldsValue } from './form/RenewalFields';

interface RenewalModalProps {
  /** الملف المشطوب المُعاد إلى المتداول. */
  doc: DocumentResponse;
  /** اسم الملف المعروض (المنفذ عليه أو المقترض بحسب الصفة). */
  name: string;
  /** نقطة إعادة الملف (مثل /documents/7/restore-struck-off). */
  endpoint: string;
  /** تسمية زر التأكيد. */
  confirmLabel: string;
  /** تسمية الزر أثناء جارٍ الإعادة. */
  busyLabel: string;
  onClose: () => void;
  /** يُستدعى بعد نجاح الإعادة فيُحدّث القائمة ويعرض رسالة النجاح. */
  onChanged: () => void;
}

/**
 * نافذة إعادة الملف المشطوب إلى المتداول: حقول التجديد في نافذة مستقلة
 * بدل التوسّع المضمَّن في صف/بطاقة جدول المشطوبة. تُرسل نفس نقطة الإعادة
 * ونفس الحمولة التي كانت تُرسل قبل الانتقال إلى النافذة (بلا تغيير خلفية).
 */
export default function RenewalModal({
  doc,
  name,
  endpoint,
  confirmLabel,
  busyLabel,
  onClose,
  onChanged,
}: RenewalModalProps) {
  const [renewal, setRenewal] = useState<RenewalFieldsValue>({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const onRenewalSet: (key: keyof RenewalFieldsValue, value: string) => void = (key, value) => {
    setRenewal((r) => ({
      ...r,
      [key]: key === 'renewalYear' ? (value.trim() ? Number(value.trim()) : undefined) : value,
    }));
    if (key === 'renewalFileNumber' || key === 'renewalYear') setError('');
  };

  // سنة الإعادة لعائلة «منفذ عليها/عرض وايداع» مقررة كسنة الخادم الحالية فقط (يُخفى حقلها).
  const hideRenewalYear = isExecutedLike(doc.generalEntitySide);
  const { currentYear } = useCurrentYear();

  const submit = async () => {
    if (!(renewal.renewalFileNumber ?? '').trim()) {
      setError('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب');
      return;
    }
    // نظام «طالبة تنفيذ»: سنة الإعادة إلزامية أيضًا عند إعادة الملف المشطوب.
    if (!isExecutedLike(doc.generalEntitySide) && renewal.renewalYear == null) {
      setError('سنة الإعادة مطلوبة عند إعادة ملف «طالبة تنفيذ» المشطوب');
      return;
    }
    setBusy(true);
    setError('');
    try {
      await api.post(endpoint, {
        renewalFileReceiptNumber: trimNull(renewal.renewalFileReceiptNumber),
        renewalFileReceiptDate: trimNull(renewal.renewalFileReceiptDate),
        renewalFileNumber: trimNull(renewal.renewalFileNumber),
        renewalFileType: trimNull(renewal.renewalFileType),
        // عائلة «منفذ عليها» لا ترسل سنة مدخلة بل سنة الخادم الحالية ليتسق مع رفض الخلفية الدفاعي.
        renewalYear: hideRenewalYear ? currentYear : renewal.renewalYear ?? undefined,
        renewalDate: trimNull(renewal.renewalDate),
      });
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
      aria-label="إعادة الملف إلى المتداول"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">إعادة الملف إلى المتداول</h3>
          <button
            type="button"
            onClick={onClose}
            disabled={busy}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11 disabled:opacity-40"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 mb-4">
            <p className="text-xs text-gray-500 mb-1">الملف المشطوب</p>
            <p className="font-medium text-gray-800">{name}</p>
            <p className="text-sm text-gray-600 mt-1">رقم الملف: {displayFileNumber(doc) || '—'}</p>
          </div>

          <RenewalFields value={renewal} onSet={onRenewalSet} stacked idPrefix="renewal-" hideYear={hideRenewalYear} />

          {error && <p className="text-red-600 text-sm mt-3">{error}</p>}

          <div className="mt-5 flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={busy}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={busy}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {busy ? busyLabel : confirmLabel}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}