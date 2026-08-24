import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import RichTextEditor from '../RichTextEditor';
import type { ReviewLetterDto } from '../../types';

/**
 * نافذة تسطير كتاب مطالعة:
 * - من صفحة المطالعات: كتاب عام غير مرتبط بملف (documentId فارغ).
 * - من تفاصيل ملف: الكتاب مرتبط بذلك الملف حصرًا.
 * «حفظ وإرسال» تولّد الرقم والتاريخ تلقائيًا في الخلفية.
 */
export default function CreateReviewLetterModal({
  documentId,
  documentTitle,
  onClose,
  onCreated,
}: {
  documentId?: number | null;
  documentTitle?: string;
  onClose: () => void;
  onCreated?: (letter: ReviewLetterDto) => void;
}) {
  const [bodyHtml, setBodyHtml] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const submit = async () => {
    setSaving(true);
    setError('');
    try {
      const response = await api.post<ReviewLetterDto>('/review-letters', {
        documentId: documentId ?? null,
        bodyHtml,
      });
      onCreated?.(response.data);
      onClose();
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
      aria-label="تسطير مطالعة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">تسطير مطالعة</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          {documentTitle ? (
            <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 mb-4 text-sm text-gray-700">
              سيُربط هذا الكتاب بالملف:{' '}
              <span className="font-medium">{documentTitle}</span>
            </div>
          ) : (
            <div className="rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-2 mb-4 text-sm text-emerald-800">
              كتاب مطالعة عام غير مرتبط بملف
            </div>
          )}

          <label htmlFor="review-letter-body" className="block text-sm font-medium text-gray-700 mb-1.5">
            موضوع المطالعة
          </label>
          <RichTextEditor value={bodyHtml} onChange={setBodyHtml} placeholder="اكتب موضوع المطالعة…" />

          {error && (
            <p className="text-red-600 text-sm mt-3" role="alert">
              {error}
            </p>
          )}

          <div className="mt-5 flex flex-wrap gap-2">
            <button
              onClick={submit}
              disabled={saving}
              className="bg-[#800000] hover:bg-[#9e0e0e] disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
            >
              {saving ? 'جارِ الحفظ والإرسال…' : 'حفظ وإرسال'}
            </button>
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              إلغاء
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
