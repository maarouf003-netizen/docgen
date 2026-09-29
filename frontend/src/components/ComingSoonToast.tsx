import { useEffect } from 'react';
import { useTimeout } from '../hooks/useTimeout';

/** مدة الظهور التلقائي قبل الاختفاء (بالميلي ثانية). */
export const COMING_SOON_TIMEOUT_MS = 4000;

/**
 * تنبيه «الميزة قيد البناء حاليا» للبنود المؤجلة (المنتدى/المكتبة):
 * يختفي تلقائيًا ويدويًا (زر إغلاق + `Escape`)، ويُعلن لقارئ الشاشة عبر `role="status"`.
 */
export function ComingSoonToast({ feature, onClose }: { feature: string; onClose: () => void }) {
  useTimeout(onClose, COMING_SOON_TIMEOUT_MS);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  return (
    <div className="fixed bottom-20 lg:bottom-6 inset-x-0 z-50 flex justify-center px-4 pointer-events-none">
      <div
        role="status"
        className="pointer-events-auto flex items-center gap-3 bg-gray-900 text-white rounded-2xl shadow-xl px-4 py-2.5 max-w-md w-full sm:w-auto"
      >
        <p className="text-sm flex-1 min-w-0">
          «{feature}» — الميزة قيد البناء حاليا
        </p>
        <button
          type="button"
          onClick={onClose}
          aria-label="إغلاق التنبيه"
          className="shrink-0 min-h-11 min-w-11 rounded-lg text-gray-300 hover:text-white hover:bg-white/10 focus-visible:ring-2 focus-visible:ring-white"
        >
          ✕
        </button>
      </div>
    </div>
  );
}
