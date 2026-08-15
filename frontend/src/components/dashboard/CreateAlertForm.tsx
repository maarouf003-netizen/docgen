import type { FormEvent } from 'react';
import type { HeadAlertTargetType, LawyerListItem } from '../../types';
import { TARGET_TYPE_LABELS } from './dashboardFormat';

/**
 * نموذج إصدار تنبيه لرئيس القسم: رسالة خاصة لمحامٍ أو تعميم لجميع محامي الفرع.
 * Mobile-first مع أهداف لمس 44px+.
 */
export function CreateAlertForm({
  targetType,
  onTargetTypeChange,
  lawyers,
  lawyerId,
  onLawyerIdChange,
  message,
  onMessageChange,
  submitting,
  error,
  onSubmit,
  onCancel,
}: {
  targetType: HeadAlertTargetType;
  onTargetTypeChange: (t: HeadAlertTargetType) => void;
  lawyers: LawyerListItem[];
  lawyerId: string;
  onLawyerIdChange: (v: string) => void;
  message: string;
  onMessageChange: (v: string) => void;
  submitting: boolean;
  error: string;
  onSubmit: (e: FormEvent) => void;
  onCancel: () => void;
}) {
  return (
    <form onSubmit={onSubmit} className="px-4 sm:px-5 py-4 border-b border-gray-100 grid gap-4">
      <div role="group" aria-label="نوع التنبيه" className="flex flex-wrap gap-2">
        {(['lawyer', 'branch'] as HeadAlertTargetType[]).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => onTargetTypeChange(t)}
            aria-pressed={targetType === t}
            className={`min-h-11 px-4 rounded-xl text-sm font-medium transition-colors ${
              targetType === t
                ? 'bg-emerald-600 text-white'
                : 'text-gray-600 border border-gray-200 bg-white hover:bg-gray-50'
            }`}
          >
            {TARGET_TYPE_LABELS[t]}
          </button>
        ))}
      </div>

      {targetType === 'lawyer' ? (
        <div>
          <label htmlFor="alert-lawyer" className="block text-xs font-medium text-gray-600 mb-1">
            المحامي
          </label>
          <select
            id="alert-lawyer"
            value={lawyerId}
            onChange={(e) => onLawyerIdChange(e.target.value)}
            className="w-full min-h-11 rounded-xl border border-gray-200 bg-white px-3 text-sm"
          >
            <option value="">اختر محامياً...</option>
            {lawyers.map((l) => (
              <option key={l.id} value={l.id}>
                {l.fullName}
              </option>
            ))}
          </select>
        </div>
      ) : null}

      <div>
        <label htmlFor="alert-message" className="block text-xs font-medium text-gray-600 mb-1">
          نص التنبيه
        </label>
        <textarea
          id="alert-message"
          value={message}
          onChange={(e) => onMessageChange(e.target.value)}
          rows={3}
          className="w-full min-h-11 border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      </div>

      {error ? <p className="text-red-600 text-sm">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          disabled={submitting}
          className="min-h-11 px-4 rounded-lg bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white text-sm font-medium"
        >
          {submitting ? 'جارِ الإرسال...' : 'إرسال التنبيه'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="min-h-11 px-4 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50"
        >
          إلغاء
        </button>
      </div>
    </form>
  );
}
