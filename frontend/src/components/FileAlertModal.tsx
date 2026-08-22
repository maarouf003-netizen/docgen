import { useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useTimeout } from '../hooks/useTimeout';

export default function FileAlertModal({
  documentId,
  documentTitle,
  recipientName,
  onClose,
  onSent,
}: {
  documentId: number;
  documentTitle?: string;
  recipientName?: string;
  onClose: () => void;
  onSent?: () => void;
}) {
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useTimeout(onClose, success ? 700 : null);

  const submit = async () => {
    if (!message.trim()) {
      setError('نص التنبيه مطلوب');
      return;
    }
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      await api.post('/alerts', {
        targetType: 'document',
        documentId,
        targetLawyerId: null,
        message: message.trim(),
      });
      setSuccess('تم توجيه التنبيه إلى المحامي المختص');
      onSent?.();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="توجيه تنبيه"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-gray-800">توجيه تنبيه للملف</h3>
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
            <p className="text-xs text-gray-500 mb-1">الملف</p>
            <p className="font-medium text-gray-800">{documentTitle || `ملف رقم ${documentId}`}</p>
            {recipientName ? (
              <>
                <p className="text-xs text-gray-500 mt-2 mb-1">المحامي المختص (المستلم)</p>
                <p className="font-medium text-gray-800">{recipientName}</p>
              </>
            ) : null}
          </div>

          <label htmlFor="file-alert-message" className="block text-xs font-medium text-gray-600 mb-1">
            نص التنبيه
          </label>
          <textarea
            id="file-alert-message"
            value={message}
            onChange={(e) => {
              setMessage(e.target.value);
              setError('');
            }}
            rows={3}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-500"
            placeholder="اكتب نص التنبيه..."
          />

          {error && <p className="text-red-600 text-sm mt-3">{error}</p>}
          {success && <p className="text-emerald-700 text-sm mt-3">{success}</p>}

          <div className="mt-5 flex flex-wrap gap-2">
            <button
              onClick={submit}
              disabled={saving}
              className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {saving ? 'جارِ الإرسال...' : 'إرسال التنبيه'}
            </button>
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
