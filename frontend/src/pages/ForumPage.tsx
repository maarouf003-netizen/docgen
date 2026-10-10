import { useCallback, useEffect, useRef, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import { dayLocalKey, formatDate } from '../utils/dates';
import type { ForumMessageDto, ForumMessagesPageDto, ForumReaderDto } from '../types';
import MessageBubble from '../components/forum/MessageBubble';
import MessageComposer from '../components/forum/MessageComposer';
import { FORUM_BODY_MAX, loadForumDraft, saveForumDraft } from '../components/forum/forumDraft';
import PinnedBanner from '../components/forum/PinnedBanner';
import ReadersDialog from '../components/forum/ReadersDialog';
import { Toast } from '../components/Toast';

const PAGE_LIMIT = 30;
const POLL_MS = 60_000;
const NEAR_BOTTOM_PX = 160;

function prefersReducedMotion(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

function isNearBottom(): boolean {
  const doc = document.documentElement;
  return window.innerHeight + window.scrollY >= doc.scrollHeight - NEAR_BOTTOM_PX;
}

function scrollToBottom(smooth: boolean): void {
  window.scrollTo({ top: document.documentElement.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
}

/** عنوان فاصل اليوم: اليوم / أمس / التاريخ الكامل. */
function dayLabel(key: string): string {
  const today = dayLocalKey(new Date().toISOString());
  const yesterday = dayLocalKey(new Date(Date.now() - 24 * 3_600_000).toISOString());
  if (key === today) return 'اليوم';
  if (key === yesterday) return 'أمس';
  return formatDate(key);
}

/**
 * منتدى المحامين — تيار واحد مسطّح (واتساب RTL): ذيل أولي + «تحميل المزيد»
 * للأعلى بمفتاح `Id` + استطلاع 60 ثانية للجديد (مجمّد بخفاء التبويب) +
 * تعليم القراءة عند الفتح والوصول + شريط المثبّتة + بحث نصي.
 */
export default function ForumPage() {
  const { user } = useAuth();
  const userId = user?.id;
  const isAdmin = user?.role === 'admin';

  const [items, setItems] = useState<ForumMessageDto[]>([]);
  const [hasOlder, setHasOlder] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');
  const [q, setQ] = useState('');
  const debouncedQ = useDebouncedValue(q, 400);
  const searching = debouncedQ.trim().length > 0;

  const [pinned, setPinned] = useState<ForumMessageDto | null>(null);
  const [quote, setQuote] = useState<ForumMessageDto | null>(null);
  const [editing, setEditing] = useState<ForumMessageDto | null>(null);
  const [draft, setDraft] = useState<string>(() => loadForumDraft());
  const [sending, setSending] = useState(false);
  const [formError, setFormError] = useState('');

  const [readersOpen, setReadersOpen] = useState(false);
  const [readersMessage, setReadersMessage] = useState<ForumMessageDto | null>(null);
  const [readers, setReaders] = useState<ForumReaderDto[]>([]);
  const [readersLoading, setReadersLoading] = useState(false);

  const [toast, setToast] = useState<{ type: 'error' | 'success'; message: string } | null>(null);
  const [highlightId, setHighlightId] = useState<number | null>(null);
  const [newCount, setNewCount] = useState(0);

  const lastMarkedRef = useRef(0);
  const itemsRef = useRef(items);
  itemsRef.current = items;
  // مسودة ما قبل التعديل: الكتابة أثناء التعديل تطغى على `localStorage`،
  // فتُحفَظ الأصلية هنا وتُستعاد عند الإلغاء أو بعد الحفظ (لا ضياع صامت).
  const draftBackup = useRef<string | null>(null);
  const draftRef = useRef<string>(loadForumDraft());
  const searchingRef = useRef(searching);
  searchingRef.current = searching;
  const highlightTimer = useRef(0);

  const markRead = useCallback(async (upToId: number) => {
    if (upToId <= lastMarkedRef.current) return;
    lastMarkedRef.current = upToId;
    try {
      await api.post('/forum/read', { upToMessageId: upToId });
    } catch {
      // فشل التوثيق لا يكسر التيار — الشارة تبقى على آخر قيمة معروفة.
      lastMarkedRef.current = 0;
    }
  }, []);

  const fetchPinned = useCallback(async () => {
    try {
      const r = await api.get<ForumMessageDto | ''>('/forum/pinned');
      setPinned(r.data === '' || r.data == null ? null : (r.data as ForumMessageDto));
    } catch {
      setPinned(null);
    }
  }, []);

  // الذيل الأولي + إعادة التحميل عند تغيّر البحث — يمرّر للأسفل ويوثّق القراءة.
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    const params: Record<string, unknown> = { limit: PAGE_LIMIT };
    if (searching) params.q = debouncedQ.trim();
    api
      .get<ForumMessagesPageDto>('/forum/messages', { params, signal: controller.signal })
      .then((r) => {
        setItems(r.data.items);
        setHasOlder(r.data.hasOlder);
        setNewCount(0);
        const max = r.data.items.reduce((m, x) => Math.max(m, x.id), 0);
        if (max > 0) void markRead(max);
        if (!searching) {
          requestAnimationFrame(() => scrollToBottom(!prefersReducedMotion()));
        }
      })
      .catch((err) => {
        if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') return;
        setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [debouncedQ, searching, markRead]);

  useEffect(() => {
    void fetchPinned();
  }, [fetchPinned]);

  // استطلاع الجديد كل دقيقة (مجمّد بخفاء التبويب وأثناء البحث) + فوري عند الظهور.
  useEffect(() => {
    const poll = async () => {
      if (searchingRef.current) return;
      if (typeof document !== 'undefined' && document.visibilityState === 'hidden') return;
      const current = itemsRef.current;
      if (current.length === 0) return;
      const lastId = current[current.length - 1].id;
      try {
        const r = await api.get<ForumMessagesPageDto>('/forum/messages', {
          params: { limit: PAGE_LIMIT, after: lastId },
        });
        if (r.data.items.length === 0) return;
        // المثبّتة قد تتغير عن بُعد (تثبيت/إسقاط/حذف) — تُحدَّث مع كل استطلاع.
        void fetchPinned();
        setItems((prev) => {
          const known = new Set(prev.map((m) => m.id));
          const fresh = r.data.items.filter((m) => !known.has(m.id));
          return fresh.length === 0 ? prev : [...prev, ...fresh];
        });
        const max = r.data.items.reduce((m, x) => Math.max(m, x.id), lastId);
        if (isNearBottom()) {
          requestAnimationFrame(() => scrollToBottom(!prefersReducedMotion()));
          void markRead(max);
        } else {
          setNewCount((n) => n + r.data.items.length);
          void markRead(max);
        }
      } catch {
        /* الاستطلاع يبقى صامتًا — الشارة والزر اليدوي يغطيان الفشل. */
      }
    };
    const timer = window.setInterval(poll, POLL_MS);
    const onVisible = () => {
      if (document.visibilityState === 'visible') void poll();
    };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [markRead, fetchPinned]);

  useEffect(() => () => window.clearTimeout(highlightTimer.current), []);

  const flashHighlight = useCallback((id: number) => {
    setHighlightId(id);
    window.clearTimeout(highlightTimer.current);
    highlightTimer.current = window.setTimeout(() => setHighlightId(null), 2500);
  }, []);

  const jumpToMessage = useCallback(
    async (id: number) => {
      const el = document.getElementById(`forum-message-${id}`);
      if (el) {
        el.scrollIntoView({
          block: 'center',
          behavior: prefersReducedMotion() ? 'auto' : 'smooth',
        });
        flashHighlight(id);
        return;
      }
      // خارج النافذة المحمّلة: مطاردة محدودة للأعلى حتى العثور أو النفاد.
      let attempts = 0;
      let oldest: number | undefined = itemsRef.current[0]?.id;
      let found = false;
      setLoadingMore(true);
      try {
        while (!found && oldest !== undefined && attempts < 5) {
          attempts += 1;
          const r = await api.get<ForumMessagesPageDto>('/forum/messages', {
            params: { limit: PAGE_LIMIT, before: oldest },
          });
          // نسخة محلية قبل الإغلاق: `r` الموعودة داخل الحلقة لا تُأسر في الإغلاق مباشرة.
          const page: ForumMessagesPageDto = r.data;
          const fresh: ForumMessageDto[] = page.items;
          if (fresh.length === 0) break;
          if (fresh.some((m) => m.id === id)) found = true;
          setItems((prev) => [...fresh, ...prev]);
          oldest = fresh[0].id;
          if (!page.hasOlder) {
            setHasOlder(false);
            break;
          }
        }
      } catch {
        setToast({ type: 'error', message: 'تعذر تحميل الرسالة المقتبسة' });
      } finally {
        setLoadingMore(false);
      }
      requestAnimationFrame(() => {
        document
          .getElementById(`forum-message-${id}`)
          ?.scrollIntoView({ block: 'center', behavior: prefersReducedMotion() ? 'auto' : 'smooth' });
        flashHighlight(id);
      });
    },
    [flashHighlight],
  );

  const loadOlder = useCallback(async () => {
    const oldest = itemsRef.current[0]?.id;
    if (!oldest || loadingMore) return;
    setLoadingMore(true);
    const prevHeight = document.documentElement.scrollHeight;
    try {
      const params: Record<string, unknown> = { limit: PAGE_LIMIT, before: oldest };
      if (searchingRef.current) params.q = debouncedQ.trim();
      const r = await api.get<ForumMessagesPageDto>('/forum/messages', { params });
      setItems((prev) => [...r.data.items, ...prev]);
      setHasOlder(r.data.hasOlder);
      // تثبيت موضع التمرير بعد الإلحاق العلوي (بلا قفزة بصرية).
      requestAnimationFrame(() => {
        window.scrollBy(0, document.documentElement.scrollHeight - prevHeight);
      });
    } catch (err) {
      setToast({ type: 'error', message: getApiErrorMessage(err) });
    } finally {
      setLoadingMore(false);
    }
  }, [debouncedQ, loadingMore]);

  const handleDraftChange = useCallback((value: string) => {
    draftRef.current = value;
    setDraft(value);
    saveForumDraft(value);
  }, []);

  /** استعادة مسودة ما قبل التعديل (أو الفراغ) بعد انتهاء وضع التعديل. */
  const restorePreEditDraft = useCallback(() => {
    const backup = draftBackup.current ?? '';
    draftBackup.current = null;
    draftRef.current = backup;
    setDraft(backup);
    saveForumDraft(backup);
  }, []);

  const handleSend = useCallback(async () => {
    const text = draft.trim();
    if (!text || draft.length > FORUM_BODY_MAX || sending) return;
    setSending(true);
    setFormError('');
    try {
      if (editing) {
        const r = await api.put<ForumMessageDto>(`/forum/messages/${editing.id}`, { body: text });
        setItems((prev) => prev.map((m) => (m.id === editing.id ? r.data : m)));
        setEditing(null);
        restorePreEditDraft();
      } else {
        const r = await api.post<ForumMessageDto>('/forum/messages', {
          body: text,
          quotedMessageId: quote?.id ?? null,
        });
        setItems((prev) => [...prev, r.data]);
        setQuote(null);
        requestAnimationFrame(() => scrollToBottom(!prefersReducedMotion()));
        void markRead(r.data.id);
        draftRef.current = '';
        setDraft('');
        saveForumDraft('');
      }
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSending(false);
    }
  }, [draft, sending, editing, quote, markRead, restorePreEditDraft]);

  const handleCopy = useCallback(async (message: ForumMessageDto) => {
    try {
      await navigator.clipboard.writeText(message.body);
      setToast({ type: 'success', message: 'نُسخت الرسالة' });
    } catch {
      setToast({ type: 'error', message: 'تعذر النسخ — انسخ النص يدويًا' });
    }
  }, []);

  const handleDelete = useCallback(
    async (message: ForumMessageDto) => {
      if (!window.confirm('حذف هذه الرسالة نهائيًا؟ لا يمكن التراجع.')) return;
      try {
        await api.delete(`/forum/messages/${message.id}`);
        setItems((prev) => prev.filter((m) => m.id !== message.id));
        if (pinned?.id === message.id) setPinned(null);
      } catch (err) {
        setToast({ type: 'error', message: getApiErrorMessage(err) });
      }
    },
    [pinned],
  );

  const handleTogglePin = useCallback(async (message: ForumMessageDto) => {
    try {
      const r = await api.post<ForumMessageDto>(`/forum/messages/${message.id}/pin`);
      setItems((prev) => prev.map((m) => (m.id === message.id ? r.data : m)));
      // الوحدانية: أي مثبتة سابقة في النافذة تسقط محليًا فورًا.
      if (r.data.isPinned) {
        setItems((prev) => prev.map((m) => (m.id === message.id ? m : { ...m, isPinned: false })));
        setPinned(r.data);
      } else if (pinned?.id === message.id) {
        setPinned(null);
      }
    } catch (err) {
      setToast({ type: 'error', message: getApiErrorMessage(err) });
    }
  }, [pinned]);

  const handleShowReaders = useCallback(async (message: ForumMessageDto) => {
    setReadersMessage(message);
    setReaders([]);
    setReadersLoading(true);
    setReadersOpen(true);
    try {
      const r = await api.get<ForumReaderDto[]>(`/forum/messages/${message.id}/readers`);
      setReaders(r.data);
    } catch (err) {
      setToast({ type: 'error', message: getApiErrorMessage(err) });
      setReadersOpen(false);
    } finally {
      setReadersLoading(false);
    }
  }, []);

  const handleEdit = useCallback(
    (message: ForumMessageDto) => {
      // تجميد المسودة الجارية قبل تلويثها بنص التعديل (تُستعاد لاحقًا).
      if (draftBackup.current === null) draftBackup.current = draftRef.current;
      setEditing(message);
      setQuote(null);
      draftRef.current = message.body;
      setDraft(message.body);
      saveForumDraft(message.body);
      document.getElementById('forum-composer')?.focus();
    },
    [],
  );

  // فواصل الأيام فوق التيار المرتب تصاعديًا.
  const stream: { key: string; label: string | null; message: ForumMessageDto }[] = [];
  let lastDay = '';
  for (const m of items) {
    const day = dayLocalKey(m.createdAt);
    const label = day !== lastDay ? dayLabel(day) : null;
    lastDay = day;
    stream.push({ key: `${day}-${m.id}`, label, message: m });
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h1 className="text-xl font-bold text-gray-900 text-wrap-balance">منتدى المحامين</h1>
        <div className="min-w-0 flex-1 sm:max-w-xs">
          <label htmlFor="forum-search" className="sr-only">
            بحث في نص الرسائل…
          </label>
          <input
            id="forum-search"
            type="text"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="بحث في نص الرسائل…"
            autoComplete="off"
            name="forum-search"
            className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2.5 min-h-11 text-sm focus:border-emerald-600 focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
        </div>
      </div>

      <p role="note" className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
        تنبيه مهني: لا تشارك بيانات ملفات حساسة في المنتدى.
      </p>

      {pinned && (
        <PinnedBanner
          pinned={pinned}
          onJump={() => void jumpToMessage(pinned.id)}
          onUnpin={() => void handleTogglePin(pinned)}
          canUnpin={isAdmin}
        />
      )}

      {loading ? (
        <div aria-busy="true" aria-label="جارِ تحميل الرسائل" className="flex flex-col gap-3">
          {[0, 1, 2].map((i) => (
            <div key={i} className="animate-pulse rounded-2xl border border-gray-200 bg-white p-3">
              <div className="h-3 w-1/3 rounded bg-gray-200" />
              <div className="mt-2 h-4 w-3/4 rounded bg-gray-100" />
            </div>
          ))}
        </div>
      ) : error ? (
        <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-6 text-center">
          <p className="text-sm font-bold text-red-700">{error}</p>
          <button
            type="button"
            onClick={() => {
              setError('');
              setLoading(true);
              api
                .get<ForumMessagesPageDto>('/forum/messages', { params: { limit: PAGE_LIMIT } })
                .then((r) => {
                  setItems(r.data.items);
                  setHasOlder(r.data.hasOlder);
                })
                .catch((err) => setError(getApiErrorMessage(err)))
                .finally(() => setLoading(false));
            }}
            className="mt-3 rounded-lg bg-red-600 px-4 py-2 min-h-11 text-sm font-bold text-white focus-visible:ring-2 focus-visible:ring-red-600 focus-visible:ring-offset-2"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-white px-4 py-10 text-center">
          <p className="text-sm text-gray-600">
            {searching ? 'لا نتائج مطابقة لبحثك…' : 'لا رسائل بعد — كن أول من يكتب…'}
          </p>
        </div>
      ) : (
        <div role="log" aria-live="polite" aria-label="تيار رسائل المنتدى" className="flex flex-col gap-4">
          {hasOlder && (
            <div className="flex justify-center">
              <button
                type="button"
                onClick={() => void loadOlder()}
                disabled={loadingMore}
                className="rounded-full border border-gray-300 bg-white px-4 py-2 min-h-11 text-sm font-bold text-emerald-800 shadow-sm disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
              >
                {loadingMore ? 'جارِ التحميل…' : 'تحميل المزيد ↑'}
              </button>
            </div>
          )}
          {stream.map(({ key, label, message }) => (
            <div key={key} className="flex flex-col gap-2">
              {label && (
                <div className="flex justify-center">
                  <span className="rounded-full bg-gray-200 px-3 py-1 text-[11px] font-bold text-gray-600 tabular-nums">
                    {label}
                  </span>
                </div>
              )}
              <MessageBubble
                message={message}
                mine={userId === message.authorId}
                isAdmin={isAdmin}
                highlighted={highlightId === message.id}
                onCopy={(m) => void handleCopy(m)}
                onQuote={setQuote}
                onEdit={handleEdit}
                onDelete={(m) => void handleDelete(m)}
                onTogglePin={(m) => void handleTogglePin(m)}
                onShowReaders={(m) => void handleShowReaders(m)}
                onJumpToQuote={(id) => void jumpToMessage(id)}
              />
            </div>
          ))}
        </div>
      )}

      {newCount > 0 && (
        <div className="sticky bottom-24 z-10 flex justify-center">
          <button
            type="button"
            onClick={() => {
              setNewCount(0);
              scrollToBottom(!prefersReducedMotion());
              const max = itemsRef.current.reduce((m, x) => Math.max(m, x.id), 0);
              if (max > 0) void markRead(max);
            }}
            className="rounded-full bg-emerald-700 px-4 py-2.5 min-h-11 text-sm font-bold text-white shadow-lg tabular-nums focus-visible:ring-2 focus-visible:ring-emerald-600 focus-visible:ring-offset-2"
          >
            {newCount} رسائل جديدة ↓
          </button>
        </div>
      )}

      <div className="sticky bottom-0 -mx-1 bg-gray-100/95 pb-1 pt-1 backdrop-blur">
        {formError && (
          <div role="alert" className="mb-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs font-bold text-red-700">
            {formError}
          </div>
        )}
        <MessageComposer
          quote={quote}
          onClearQuote={() => setQuote(null)}
          draft={draft}
          onDraftChange={handleDraftChange}
          onSend={() => void handleSend()}
          sending={sending}
          editing={editing}
          onCancelEdit={() => {
            setEditing(null);
            restorePreEditDraft();
          }}
        />
      </div>

      {readersOpen && readersMessage && (
        <ReadersDialog
          readers={readers}
          loading={readersLoading}
          onClose={() => setReadersOpen(false)}
        />
      )}
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}
    </div>
  );
}
