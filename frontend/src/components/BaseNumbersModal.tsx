import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import type { BaseNumberHistoryDto } from '../types';

export default function BaseNumbersModal({
  documentId,
  documentTitle,
  fileType,
  onClose,
}: {
  documentId: number;
  documentTitle?: string;
  fileType?: string;
  onClose: () => void;
}) {
  const [entries, setEntries] = useState<BaseNumberHistoryDto[] | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    setEntries(null);
    setError('');
    api
      .get<BaseNumberHistoryDto[]>(`/documents/${documentId}/base-numbers`)
      .then((r) => {
        if (!cancelled) setEntries(r.data ?? []);
      })
      .catch((err) => {
        if (!cancelled) setError(getApiErrorMessage(err));
      });
    return () => {
      cancelled = true;
    };
  }, [documentId]);

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="أرقام الأساس للسنوات السابقة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-gray-800">أرقام الأساس السابقة</h3>
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
          </div>

          {error && <p className="text-red-600 text-sm mb-3">{error}</p>}

          {entries === null && !error && <p className="text-gray-500">جارِ التحميل...</p>}

          {entries !== null && entries.length === 0 && !error && (
            <p className="text-gray-400 text-sm">لا توجد أرقام أساس مسجلة لهذا الملف</p>
          )}

          {entries !== null && entries.length > 0 && (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-xs text-gray-500">
                  <th className="py-2 pr-2 border-b border-gray-200 font-medium">السنة</th>
                  <th className="py-2 pr-2 border-b border-gray-200 font-medium">رقم الأساس</th>
                  <th className="py-2 border-b border-gray-200 font-medium">النوع</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {entries.map((entry) => (
                  <tr key={entry.year}>
                    <td className="py-2 pr-2 text-gray-700">{entry.year}</td>
                    <td className="py-2 pr-2 font-medium text-gray-800">{entry.baseNumber}</td>
                    <td className="py-2 text-gray-700">{fileType || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <div className="mt-5 flex justify-end">
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إغلاق
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
