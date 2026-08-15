import { useEffect, useRef } from 'react';

const TOAST_DURATION = 5000;

export function Toast({
  type,
  message,
  onClose,
}: {
  type: 'error' | 'success';
  message: string;
  onClose: () => void;
}) {
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    const timer = window.setTimeout(() => onCloseRef.current(), TOAST_DURATION);
    return () => window.clearTimeout(timer);
  }, [message]);

  return (
    <div
      role="alert"
      className="fixed top-3 inset-x-0 z-[60] flex justify-center px-4"
      dir="rtl"
    >
      <div
        className={`flex items-center gap-3 w-full max-w-md rounded-lg px-4 py-3 text-sm font-bold text-white shadow-lg ${
          type === 'error' ? 'bg-red-600' : 'bg-emerald-700'
        }`}
      >
        <span className="flex-1">{message}</span>
        <button
          type="button"
          onClick={onClose}
          aria-label="إغلاق الرسالة"
          className="shrink-0 text-white/80 hover:text-white text-xl leading-none min-h-11 min-w-11"
        >
          ×
        </button>
      </div>
    </div>
  );
}
