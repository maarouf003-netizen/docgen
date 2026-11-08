import { useEffect, useRef } from 'react';

/**
 * نافذة تحذير إعادة استخدام رقم أساس: تعرض رسالة النطاق فقط (سنة/نوع/دائرة)
 * وتعيد الإرسال برمز التحذير الصادر من الخادم عند تأكيد المستخدم.
 */
export default function NumberReuseWarningModal({
  message,
  busy,
  onConfirm,
  onCancel,
}: {
  message: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const confirmRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    confirmRef.current?.focus();
  }, []);

  return (
    <div
      className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4 overscroll-contain"
      dir="rtl"
      role="alertdialog"
      aria-modal="true"
      aria-label="تأكيد استخدام رقم أساس مستخدم"
    >
      <div className="w-full max-w-md rounded-xl bg-white shadow-xl">
        <div className="border-b border-amber-200 bg-amber-50 px-5 py-4">
          <h3 className="text-lg font-bold text-amber-900">تأكيد رقم أساس مستخدم</h3>
        </div>
        <div className="space-y-4 px-5 py-4">
          <p className="text-sm leading-7 text-gray-700">{message}</p>
          <div className="flex flex-wrap justify-end gap-2">
            <button
              type="button"
              onClick={onCancel}
              disabled={busy}
              className="min-h-11 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 disabled:opacity-50"
            >
              إلغاء
            </button>
            <button
              ref={confirmRef}
              type="button"
              onClick={onConfirm}
              disabled={busy}
              className="min-h-11 rounded-lg bg-amber-700 px-5 py-2 text-sm font-medium text-white hover:bg-amber-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 disabled:opacity-50"
            >
              {busy ? 'جارِ الإدخال…' : 'إدخال على أي حال'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
