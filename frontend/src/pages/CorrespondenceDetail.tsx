import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { formatDateTime } from '../utils/dates';
import { sanitizeRichText } from '../utils/richText';
import type {
  CorrespondenceDto,
  CorrespondenceMessageDto,
  CorrespondenceMessageKind,
} from '../types';
import RichTextEditor from '../components/RichTextEditor';
import CorrespondenceImportanceBadge from '../components/correspondence/CorrespondenceImportanceBadge';
import {
  CORRESPONDENCE_MESSAGE_KIND_LABELS,
  CORRESPONDENCE_UNSEEN_EVENT,
  correspondenceRoleLabel,
  correspondenceTitle,
} from '../components/correspondence/correspondenceDisplay';

const KIND_STYLES: Record<CorrespondenceMessageKind, { border: string; chip: string }> = {
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

function MessageCard({ message }: { message: CorrespondenceMessageDto }) {
  const styles = KIND_STYLES[message.kind];
  return (
    <article className={`bg-white rounded-xl border border-gray-200 shadow-sm p-4 sm:p-5 ${styles.border}`}>
      <div className="flex items-center justify-between gap-2 flex-wrap mb-3">
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${styles.chip}`}>
          {CORRESPONDENCE_MESSAGE_KIND_LABELS[message.kind]}
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

/**
 * تفاصيل مراسلة: رأسها (الرقم/التاريخ/الأهمية/الطرفان/سياق الملف/المحافظة) ثم
 * سلسلة الرسائل، فزر «تمت المشاهدة» الموثّق، فحقلا اللاحق (المنشئ) والرد (المستلم).
 * تُغذَّى من المسار الرئيسي أو مسار البوابة حسب `portal`.
 */
export default function CorrespondenceDetail({ portal = false }: { portal?: boolean }) {
  const { id } = useParams();
  const { user, hasFullAccess, isHead } = useAuth();
  const base = portal ? '/portal/correspondence' : '/correspondence';
  const backTo = portal ? '/portal/correspondence' : '/correspondence';
  const backLabel = 'عودة إلى المراسلات';
  const [letter, setLetter] = useState<CorrespondenceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [composerKind, setComposerKind] = useState<'addendum' | 'reply' | null>(null);
  const [draftHtml, setDraftHtml] = useState('');
  const [sending, setSending] = useState(false);
  const [markingSeen, setMarkingSeen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    api
      .get<CorrespondenceDto>(`${base}/${id}`, { signal: controller.signal })
      .then((r) => {
        setLetter(r.data);
      })
      .catch((err) => {
        if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') return;
        setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [id, base, refreshKey]);

  const canAddAddendum = letter !== null && user != null && letter.creatorId === user.id;
  const canReply = letter !== null && user != null && letter.targetUserId === user.id;

  const send = async () => {
    if (!composerKind || !letter) return;
    setSending(true);
    setError('');
    try {
      if (composerKind === 'addendum') {
        await api.post(`${base}/${letter.id}/addenda`, { bodyHtml: draftHtml });
      } else {
        await api.post(`${base}/${letter.id}/replies`, { bodyHtml: draftHtml });
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

  const markSeen = async () => {
    if (!letter) return;
    setMarkingSeen(true);
    setError('');
    try {
      await api.post(`${base}/${letter.id}/mark-seen`);
      setRefreshKey((k) => k + 1);
      window.dispatchEvent(new Event(CORRESPONDENCE_UNSEEN_EVENT));
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setMarkingSeen(false);
    }
  };

  if (loading && !letter) return <div className="text-gray-500 text-sm">جارِ التحميل…</div>;
  if (error && !letter)
    return (
      <div className="space-y-4">
        <p className="text-red-600 text-sm">{error}</p>
        <Link to={backTo} className="text-emerald-800 hover:underline text-sm min-h-11 inline-flex items-center">
          ↩ {backLabel}
        </Link>
      </div>
    );
  if (!letter) return null;

  return (
    <div className="max-w-3xl mx-auto">
      <div className="mb-4">
        <Link to={backTo} className="text-emerald-800 hover:underline text-sm min-h-11 inline-flex items-center gap-1">
          <span aria-hidden="true">↩</span> {backLabel}
        </Link>
      </div>

      {/* رأس الصفحة: العنوان + الرقم والتاريخ والأهمية + الطرفان + التوثيق */}
      <header className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 sm:p-5 mb-5">
        <div className="flex items-start justify-between gap-3 flex-wrap">
          <h1 className="font-bold text-gray-900 text-base sm:text-lg leading-relaxed break-words flex-1 min-w-0">
            {correspondenceTitle(letter.fileContext)}
          </h1>
          <CorrespondenceImportanceBadge importance={letter.importance} />
        </div>
        <dl className="flex items-center gap-x-4 gap-y-1 flex-wrap mt-3 text-xs sm:text-sm text-gray-600">
          <div className="flex items-center gap-1.5">
            <dt className="text-gray-400">رقم المراسلة:</dt>
            <dd className="font-mono font-semibold text-emerald-800 tabular-nums" dir="ltr">
              {letter.correspondenceNumber}
            </dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt className="text-gray-400">تاريخها:</dt>
            <dd className="tabular-nums">{formatDateTime(letter.correspondenceDate)}</dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt className="text-gray-400">من:</dt>
            <dd>
              {letter.creatorName} ({correspondenceRoleLabel(letter.creatorRole)})
            </dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt className="text-gray-400">إلى:</dt>
            <dd>
              {letter.targetName} ({correspondenceRoleLabel(letter.targetRole)})
            </dd>
          </div>
          {(hasFullAccess || isHead) && (
            <div className="flex items-center gap-1.5">
              <dt className="text-gray-400">المحافظة:</dt>
              <dd>{letter.governorate}</dd>
            </div>
          )}
          {hasFullAccess && letter.administrativeBranchName && (
            <div className="flex items-center gap-1.5">
              <dt className="text-gray-400">فرع الإدارة:</dt>
              <dd>{letter.administrativeBranchName}</dd>
            </div>
          )}
        </dl>

        {/* توثيق المشاهدات: من شاهد ومتى */}
        {letter.receipts.length > 0 && (
          <div className="mt-3 pt-3 border-t border-gray-100">
            <p className="text-xs font-medium text-gray-500 mb-1.5">مشاهَدات موثقة ({letter.receipts.length})</p>
            <ul className="flex flex-wrap gap-1.5">
              {letter.receipts.map((r) => (
                <li
                  key={r.userId}
                  className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 border border-emerald-200 px-2.5 py-1 text-xs text-emerald-800"
                >
                  <span className="font-medium">{r.userName}</span>
                  <time dateTime={r.seenAt} className="tabular-nums text-emerald-600">
                    {formatDateTime(r.seenAt)}
                  </time>
                </li>
              ))}
            </ul>
          </div>
        )}

        <div className="flex gap-2 flex-wrap mt-4 pt-3 border-t border-gray-100">
          {!letter.seenByMe ? (
            <button
              onClick={markSeen}
              disabled={markingSeen}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-700"
            >
              {markingSeen ? 'جارِ التوثيق…' : 'تمت المشاهدة'}
            </button>
          ) : (
            <span className="inline-flex items-center rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 px-4 py-2 text-sm min-h-11">
              أكّدتَ مشاهدتها ✓
            </span>
          )}
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
              className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
            >
              الرد على المراسلة
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
              ? 'نص الرد (سيُولَّد له رقم وتاريخ تلقائياً)'
              : 'نص اللاحق (سيُولَّد له رقم وتاريخ تلقائياً)'}
          </h2>
          <label htmlFor="correspondence-composer-body" className="sr-only">
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
