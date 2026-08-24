import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { formatDateTime } from '../utils/dates';
import { sanitizeRichText } from '../utils/richText';
import type { ReviewLetterDto, ReviewLetterMessageDto, ReviewLetterMessageKind } from '../types';
import RichTextEditor from '../components/RichTextEditor';
import ReviewStatusBadge from '../components/review/ReviewStatusBadge';
import {
  REVIEW_MESSAGE_KIND_LABELS,
  REVIEWS_UNSEEN_EVENT,
  reviewLetterTitle,
} from '../components/review/reviewDisplay';

const KIND_STYLES: Record<ReviewLetterMessageKind, { border: string; chip: string }> = {
  letter: {
    border: 'border-r-4 border-r-[#800000]',
    chip: 'bg-[#800000]/10 text-[#800000]',
  },
  addendum: {
    border: 'border-r-4 border-r-amber-500',
    chip: 'bg-amber-50 text-amber-700',
  },
  reply: {
    border: 'border-r-4 border-r-emerald-600',
    chip: 'bg-emerald-50 text-emerald-700',
  },
};

function MessageCard({ message }: { message: ReviewLetterMessageDto }) {
  const styles = KIND_STYLES[message.kind];
  return (
    <article className={`bg-white rounded-xl border border-gray-200 shadow-sm p-4 sm:p-5 ${styles.border}`}>
      <div className="flex items-center justify-between gap-2 flex-wrap mb-3">
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${styles.chip}`}>
          {REVIEW_MESSAGE_KIND_LABELS[message.kind]}
        </span>
        <div className="flex items-center gap-2 flex-wrap text-xs text-gray-500">
          <span className="font-mono font-semibold text-gray-700 tabular-nums" dir="ltr">
            {message.messageNumber}
          </span>
          <span aria-hidden="true">•</span>
          <time dateTime={message.messageDate} className="tabular-nums">
            {formatDateTime(message.messageDate)}
          </time>
          <span aria-hidden="true">•</span>
          <span>{message.authorName}</span>
        </div>
      </div>
      <div
        className="prose-sm text-gray-800 leading-relaxed break-words"
        dangerouslySetInnerHTML={{ __html: sanitizeRichText(message.bodyHtml) }}
      />
    </article>
  );
}

export default function ReviewDetail() {
  const { id } = useParams();
  const { user, hasFullAccess, isHead } = useAuth();
  const [letter, setLetter] = useState<ReviewLetterDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [composerKind, setComposerKind] = useState<'addendum' | 'reply' | null>(null);
  const [draftHtml, setDraftHtml] = useState('');
  const [sending, setSending] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  // الجلب مرتبط بالمعرّف والمستخدم ومفتاح التحديث بعد كل إرسال.
  // عند فتح المحامي صاحب الكتاب ووجود ردّ غير مطّلع عليه: يُعلَّم مقروءًا فورًا
  // فيُطفأ شارة «رد جديد» ويُحدَّث عدّاد بند المطالعات عبر حدث عام.
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    api
      .get<ReviewLetterDto>(`/review-letters/${id}`, { signal: controller.signal })
      .then((r) => {
        setLetter(r.data);
        const data = r.data;
        const ownerId = data.messages.find((m) => m.kind === 'letter')?.authorId;
        if (user?.role === 'lawyer' && user.id === ownerId && data.hasUnseenReply) {
          api
            .post(`/review-letters/${data.id}/mark-replies-seen`)
            .then(() => {
              setLetter((prev) =>
                prev && prev.id === data.id ? { ...prev, hasUnseenReply: false } : prev,
              );
              window.dispatchEvent(new Event(REVIEWS_UNSEEN_EVENT));
            })
            .catch(() => {
              /* إشعار الشارات ليس حرجًا؛ يُعاد الحساب لاحقًا */
            });
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
  }, [id, user, refreshKey]);

  const originalAuthorId = letter?.messages.find((m) => m.kind === 'letter')?.authorId;
  const canAddAddendum = user?.role === 'lawyer' && letter !== null && originalAuthorId === user?.id;
  const canReply = isHead && letter !== null;

  const send = async () => {
    if (!composerKind || !letter) return;
    setSending(true);
    try {
      if (composerKind === 'addendum') {
        await api.post(`/review-letters/${letter.id}/addenda`, { bodyHtml: draftHtml });
      } else {
        await api.post(`/review-letters/${letter.id}/replies`, { bodyHtml: draftHtml });
      }
      setComposerKind(null);
      setDraftHtml('');
      setRefreshKey((k) => k + 1);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  if (loading && !letter) return <div className="text-gray-500 text-sm">جارِ التحميل…</div>;
  if (error && !letter)
    return (
      <div className="space-y-4">
        <p className="text-red-600 text-sm">{error}</p>
        <Link to="/reviews" className="text-emerald-800 hover:underline text-sm min-h-11 inline-flex items-center">
          ↩ عودة إلى المطالعات
        </Link>
      </div>
    );
  if (!letter) return null;

  return (
    <div className="max-w-3xl mx-auto">
      <div className="mb-4">
        <Link to="/reviews" className="text-emerald-800 hover:underline text-sm min-h-11 inline-flex items-center gap-1">
          <span aria-hidden="true">↩</span> عودة إلى المطالعات
        </Link>
      </div>

      {/* رأس الصفحة: صيغة العنوان + رقم الكتاب وتاريخه + الحالة */}
      <header className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 sm:p-5 mb-5">
        <div className="flex items-start justify-between gap-3 flex-wrap">
          <h1 className="font-bold text-gray-900 text-base sm:text-lg leading-relaxed break-words flex-1 min-w-0">
            {reviewLetterTitle(letter.fileContext)}
          </h1>
          <ReviewStatusBadge isAnswered={letter.isAnswered} />
        </div>
        <dl className="flex items-center gap-x-4 gap-y-1 flex-wrap mt-3 text-xs sm:text-sm text-gray-600">
          <div className="flex items-center gap-1.5">
            <dt className="text-gray-400">رقم الكتاب:</dt>
            <dd className="font-mono font-semibold text-emerald-800 tabular-nums" dir="ltr">
              {letter.letterNumber}
            </dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt className="text-gray-400">تاريخه:</dt>
            <dd className="tabular-nums">{formatDateTime(letter.letterDate)}</dd>
          </div>
          {(hasFullAccess || isHead) && (
            <div className="flex items-center gap-1.5">
              <dt className="text-gray-400">سطّره:</dt>
              <dd>{letter.lawyerName}</dd>
            </div>
          )}
        </dl>

        <div className="flex gap-2 flex-wrap mt-4 pt-3 border-t border-gray-100">
          {canAddAddendum && (
            <button
              onClick={() => {
                setComposerKind('addendum');
                setError('');
              }}
              className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-700"
            >
              إضافة لاحق
            </button>
          )}
          {canReply && (
            <button
              onClick={() => {
                setComposerKind('reply');
                setError('');
              }}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-700"
            >
              {letter.isAnswered ? 'رد جديد' : 'الرد على الكتاب'}
            </button>
          )}
        </div>
      </header>

      {error && (
        <p className="text-red-600 text-sm mb-4" role="alert">
          {error}
        </p>
      )}

      {/* سلسلة الرسائل: الأصل ثم اللاحقات والردود بتسلسلها الزمني */}
      <div className="space-y-4">
        {letter.messages.map((message) => (
          <MessageCard key={message.id} message={message} />
        ))}
      </div>

      {composerKind && (
        <section
          className={`mt-5 bg-white rounded-xl border shadow-sm p-4 sm:p-5 ${
            composerKind === 'reply' ? 'border-emerald-300' : 'border-sky-300'
          }`}
          aria-label={composerKind === 'reply' ? 'صياغة الرد' : 'صياغة لاحق'}
        >
          <h2 className="font-bold text-gray-800 mb-3">
            {composerKind === 'reply'
              ? 'نص رد رئيس القسم (سيُولَّد له رقم وتاريخ تلقائياً)'
              : 'نص اللاحق (سيُولَّد له رقم وتاريخ تلقائياً)'}
          </h2>
          <label htmlFor="review-composer-body" className="sr-only">
            {composerKind === 'reply' ? 'نص الرد' : 'نص اللاحق'}
          </label>
          <RichTextEditor value={draftHtml} onChange={setDraftHtml} placeholder="اكتب هنا…" />
          <div className="mt-4 flex gap-2 flex-wrap">
            <button
              onClick={send}
              disabled={sending}
              className={
                composerKind === 'reply'
                  ? 'bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11'
                  : 'bg-sky-800 hover:bg-sky-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11'
              }
            >
              {sending ? 'جارِ الإرسال…' : 'حفظ وإرسال'}
            </button>
            <button
              onClick={() => {
                setComposerKind(null);
                setDraftHtml('');
              }}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
          </div>
        </section>
      )}
    </div>
  );
}
