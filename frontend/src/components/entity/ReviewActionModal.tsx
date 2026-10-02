export function ActionModal({
  title,
  subtitle,
  onClose,
  children,
  footer,
}: {
  title: string;
  subtitle?: string;
  onClose: () => void;
  children: React.ReactNode;
  footer: React.ReactNode;
}) {
  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div className="min-w-0">
            <h3 className="text-lg font-bold text-gray-800">{title}</h3>
            {subtitle && (
              <p className="text-xs text-gray-500 mt-0.5 truncate">{subtitle}</p>
            )}
          </div>
          <button
            onClick={onClose}
            aria-label="إغلاق"
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
          >
            ×
          </button>
        </div>
        <div className="overflow-y-auto p-5 grow overscroll-contain">{children}</div>
        <div className="px-5 py-4 border-t border-gray-100 flex flex-wrap items-center justify-end gap-2">
          {footer}
        </div>
      </div>
    </div>
  );
}

export function CloseButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
    >
      إغلاق
    </button>
  );
}
