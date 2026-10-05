import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../../api/client';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import type { ExecutionCircuitDto } from '../../types';

/**
 * نافذة اختيار دائرة التنفيذ من السجل (نمط PublicEntityPickerModal):
 * حقل مقفل + زر اختيار من سجل + شارة ارتباط. تُستخدم في نموذج الملف
 * (دوائر الفرع) وفي تسطير الإنابة (دوائر المحافظة).
 */
export default function CircuitPickerModal({
  title = 'اختيار دائرة التنفيذ',
  fetchUrl,
  initialSelectedId = null,
  onSelect,
  onClose,
}: {
  title?: string;
  fetchUrl: '/execution-circuits/for-lawyer' | '/execution-circuits/for-delegation';
  initialSelectedId?: number | null;
  onSelect: (circuit: ExecutionCircuitDto) => void;
  onClose: () => void;
}) {
  const [circuits, setCircuits] = useState<ExecutionCircuitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [loadError, setLoadError] = useState('');
  const searchRef = useRef<HTMLInputElement>(null);
  const dialogRef = useFocusTrap<HTMLDivElement>();

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setLoadError('');
      try {
        const r = await api.get<ExecutionCircuitDto[]>(fetchUrl);
        if (!cancelled) setCircuits(r?.data ?? []);
      } catch {
        if (!cancelled) setLoadError('تعذّر تحميل قائمة الدوائر — حاول لاحقًا');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [fetchUrl]);

  useEffect(() => {
    searchRef.current?.focus();
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const filtered = useMemo(() => {
    const term = search.trim();
    if (!term) return circuits;
    return circuits.filter((c) => c.name.includes(term));
  }, [circuits, search]);

  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">{title}</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>
        <div className="px-5 py-4 space-y-3">
          <input
            ref={searchRef}
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="ابحث باسم الدائرة…"
            aria-label="البحث باسم الدائرة"
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          />
          {loading ? (
            <p className="text-sm text-gray-500 py-6 text-center">جارِ تحميل الدوائر…</p>
          ) : loadError ? (
            <p className="text-sm text-red-600" role="alert">
              {loadError}
            </p>
          ) : filtered.length === 0 ? (
            <p className="text-sm text-gray-500 py-6 text-center">
              {circuits.length === 0 ? 'لا توجد دوائر مسجلة بعد — راجع رئيس القسم لإدخال الدوائر' : 'لا توجد نتائج مطابقة'}
            </p>
          ) : (
            <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 max-h-72 overflow-y-auto overscroll-contain">
              {filtered.map((c) => (
                <li key={c.id}>
                  <button
                    type="button"
                    onClick={() => onSelect(c)}
                    aria-label={`اختيار ${c.name}`}
                    className="w-full flex items-center justify-between gap-2 px-3 py-2 min-h-11 text-start hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  >
                    <span className="text-sm text-gray-800">{c.name}</span>
                    {initialSelectedId === c.id && (
                      <span className="rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                        المختارة ✓
                      </span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}
          <div className="flex justify-end pt-1">
            <button
              type="button"
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
