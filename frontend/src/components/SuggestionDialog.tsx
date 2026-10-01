import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';

/**
 * حوار إرسال اقتراح تطوير: رسالة نصية + إرسال/إلغاء، تركيز أول خطأ،
 * ورسالة نجاح — يصل للمشرف فقط.
 */
export function SuggestionDialog({ onClose, onSent }: { onClose: () => void; onSent: () => void }) {
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [sending, setSending] = useState(false);
  const panelRef = useRef<HTMLFormElement>(null);
  const messageRef = useRef<HTMLTextAreaElement>(null);
  // `onClose` يأتي دالةً مضمّنة من الأب (هوية جديدة كل render) — عبر مرجع
  // يركّب التأثير مرة واحدة فلا يُعاد التركيز ولا المستمع مع كل استعلام يحل.
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    messageRef.current?.focus();
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onCloseRef.current();
        return;
      }
      if (e.key !== 'Tab' || !panelRef.current) return;
      const focusables = Array.from(
        panelRef.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex="-1"])',
        ),
      ).filter((el) => el.offsetParent !== null);
      if (focusables.length === 0) return;
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement as HTMLElement | null;
      if (e.shiftKey && (active === first || !panelRef.current.contains(active))) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previouslyFocused?.focus?.();
    };
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!message.trim()) {
      setError('نص الاقتراح مطلوب');
      messageRef.current?.focus();
      return;
    }
    setError('');
    setSending(true);
    try {
      await api.post('/app-suggestions', { message: message.trim() });
      onSent();
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center"
      role="dialog"
      aria-modal="true"
      aria-label="إرسال اقتراح لتطوير التطبيق"
    >
      <button
        onClick={onClose}
        aria-label="إغلاق حوار الاقتراح"
        tabIndex={-1}
        className="absolute inset-0 bg-black/50 w-full h-full cursor-default"
      />
      <form
        ref={panelRef}
        onSubmit={submit}
        className="relative bg-white w-full sm:max-w-lg max-h-[90dvh] overflow-y-auto overscroll-contain rounded-t-3xl sm:rounded-3xl shadow-xl p-4 sm:p-5"
      >
        <h3 className="font-bold text-gray-900 mb-1">إرسال اقتراح لتطوير التطبيق</h3>
        <p className="text-xs text-gray-500 mb-3">يصل الاقتراح إلى المشرف فقط.</p>
        <label className="block mb-2.5" htmlFor="suggestion-message">
          <span className="block text-xs text-gray-600 mb-1">نص الاقتراح *</span>
          <textarea
            ref={messageRef}
            id="suggestion-message"
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            placeholder="مثال: أضيفوا تصدير التقرير الشهري إلى إكسل…"
            name="suggestion-message"
            autoComplete="off"
            rows={4}
            maxLength={2000}
            className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
        </label>
        {error ? (
          <p role="alert" className="text-red-700 text-xs mb-2">
            {error}
          </p>
        ) : null}
        <div className="flex gap-2">
          <button
            type="submit"
            disabled={sending}
            className="min-h-11 px-4 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-medium disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            {sending ? 'جارٍ الإرسال…' : 'إرسال الاقتراح'}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="min-h-11 px-4 rounded-lg border border-gray-200 text-sm hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            إلغاء
          </button>
        </div>
      </form>
    </div>
  );
}
