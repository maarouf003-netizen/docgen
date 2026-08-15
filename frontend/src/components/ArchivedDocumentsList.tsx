import { useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useIsMobile } from '../hooks/useMediaQuery';
import { applicantName, displayFileNumber, isExecutedLike } from '../utils/documentDisplay';
import { RenewalFields, type RenewalFieldsValue } from './form/RenewalFields';
import { trimNull } from '../utils/serialization';
import type { DocumentResponse, PagedResult } from '../types';

/** إعدادات القائمة الأرشيفية (محذوفة/مشطوبة)؛ كل اختلاف بين الصفحتين يمر عبر هذه الخيارات. */
export interface ArchivedDocumentsListConfig {
  /** عنوان الصفحة. */
  title: string;
  /** نص حقل البحث. */
  searchPlaceholder: string;
  /** نص قائمة النتائج الفارغة. */
  emptyText: string;
  /** هل تُعرض وصلة العودة إلى «الملفات التنفيذية»؟ */
  showBackLink: boolean;
  /** نقطة جلب القائمة (مثل /documents/deleted). */
  fetchEndpoint: string;
  /** نقطة إعادة الملف للفهرس المحدد (مثل /documents/7/restore). مطلوبة عند تفعيل canRestore فقط. */
  restoreEndpoint?: (id: number) => string;
  /** تسمية زر الإعادة قبل التأكيد. مطلوبة عند تفعيل canRestore فقط. */
  restoreButtonLabel?: string;
  /** تسمية زر تأكيد الإعادة. مطلوبة عند تفعيل canRestore فقط. */
  confirmRestoreLabel?: string;
  /** تسمية الزر أثناء جارٍ الإعادة. مطلوبة عند تفعيل canRestore فقط. */
  restoringLabel?: string;
  /** رسالة النجاح بعد الإعادة؛ تستقبل الاسم المعروض. مطلوبة عند تفعيل canRestore فقط. */
  successMessage?: (name: string) => string;
  /** تسمية عمود التاريخ في الجدول. */
  dateColumnHeader: string;
  /** قيمة خلية التاريخ المنسّقة. */
  dateCell: (d: DocumentResponse) => string;
  /** عنصر أعلى يمين بطاقة الجوال (تاريخ الحذف أو شارة الحالة). */
  cardTopRight: (d: DocumentResponse) => ReactNode;
  /** عنصر إضافي أسفل بطاقة الجوال (مثل سطر تاريخ الشطب). */
  cardBottomExtra?: (d: DocumentResponse) => ReactNode;
  /** دالة اسم «المنفذ عليه» (fullName أو executedFullName). */
  displayName: (d: DocumentResponse) => string;
  /** هل يربط اسم الملف بصفحة الملف؟ */
  linkToDocument: boolean;
  /** هل يستطيع المستخدم الحالي الإعادة (المحامي صاحب الملف فقط)؟ */
  canRestore: boolean;
  /** هل تستلزم الإعادة إدخال بيان تجديد (رقم ملف جديد)؟ (الملفات المشطوبة فقط). */
  requiresRenewal?: boolean;
}

export default function ArchivedDocumentsList({ config }: { config: ArchivedDocumentsListConfig }) {
  const isMobile = useIsMobile();
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<DocumentResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [confirmId, setConfirmId] = useState<number | null>(null);
  const [restoringId, setRestoringId] = useState<number | null>(null);
  const [renewal, setRenewal] = useState<RenewalFieldsValue>({});
  const [renewalError, setRenewalError] = useState('');

  const onRenewalSet = (key: keyof RenewalFieldsValue, value: string) => {
    setRenewal((r) => ({ ...r, [key]: key === 'renewalYear' ? (value.trim() ? Number(value.trim()) : undefined) : value }));
    if (key === 'renewalFileNumber' || key === 'renewalYear') setRenewalError('');
  };

  const beginRestore = (d: DocumentResponse) => {
    setRenewal({});
    setRenewalError('');
    setConfirmId(d.id);
  };

  const load = () => {
    setLoading(true);
    const params = new URLSearchParams();
    if (query) params.set('q', query);
    params.set('page', String(page));
    params.set('perPage', '20');
    api
      .get<PagedResult<DocumentResponse>>(`${config.fetchEndpoint}?${params.toString()}`)
      .then((r) => setData(r.data))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, page]);

  const handleRestore = async (d: DocumentResponse) => {
    if (!config.canRestore || !config.restoreEndpoint) return;
    if (config.requiresRenewal) {
      if (!(renewal.renewalFileNumber ?? '').trim()) {
        setRenewalError('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب');
        return;
      }
      // نظام «طالبة تنفيذ»: سنة الإعادة إلزامية أيضًا عند إعادة الملف المشطوب.
      if (!isExecutedLike(d.generalEntitySide) && renewal.renewalYear == null) {
        setRenewalError('سنة الإعادة مطلوبة عند إعادة ملف «طالبة تنفيذ» المشطوب');
        return;
      }
    }
    setRestoringId(d.id);
    setError('');
    try {
      if (config.requiresRenewal) {
        await api.post(config.restoreEndpoint(d.id), {
          renewalFileReceiptNumber: trimNull(renewal.renewalFileReceiptNumber),
          renewalFileReceiptDate: trimNull(renewal.renewalFileReceiptDate),
          renewalFileNumber: trimNull(renewal.renewalFileNumber),
          renewalFileType: trimNull(renewal.renewalFileType),
          renewalYear: renewal.renewalYear ?? undefined,
          renewalDate: trimNull(renewal.renewalDate),
        });
      } else {
        await api.post(config.restoreEndpoint(d.id));
      }
      setMessage(config.successMessage?.(config.displayName(d) || String(d.id)) ?? '');
      setConfirmId(null);
      load();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setRestoringId(null);
    }
  };

  const restoreButton = (d: DocumentResponse) =>
    confirmId === d.id ? (
      <div className="flex flex-col gap-3">
        {config.requiresRenewal && (
          <div className="w-full">
            <RenewalFields value={renewal} onSet={onRenewalSet} stacked idPrefix="restore-" />
            {renewalError && <p className="text-sm text-red-600 mt-2">{renewalError}</p>}
          </div>
        )}
        <div className="flex gap-2 flex-wrap">
          <button
            type="button"
            onClick={() => handleRestore(d)}
            disabled={restoringId === d.id}
            className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
          >
            {restoringId === d.id ? config.restoringLabel : config.confirmRestoreLabel}
          </button>
          <button
            type="button"
            onClick={() => setConfirmId(null)}
            disabled={restoringId === d.id}
            className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm text-gray-700 min-h-11"
          >
            إلغاء
          </button>
        </div>
      </div>
    ) : (
      <button
        type="button"
        onClick={() => beginRestore(d)}
        className="border border-emerald-700 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-1.5 text-sm font-medium min-h-11"
      >
        {config.restoreButtonLabel}
      </button>
    );

  const nameOnCard = (d: DocumentResponse) =>
    config.linkToDocument ? (
      <Link
        to={`/documents/${d.id}`}
        className="text-emerald-800 font-bold text-lg hover:underline min-h-11"
      >
        {config.displayName(d) || `مستند ${d.id}`}
      </Link>
    ) : (
      <div className="text-emerald-800 font-bold text-lg">{config.displayName(d) || `مستند ${d.id}`}</div>
    );

  const nameOnTable = (d: DocumentResponse) =>
    config.linkToDocument ? (
      <Link
        to={`/documents/${d.id}`}
        className="hover:text-emerald-700 hover:underline inline-flex items-center min-h-11"
      >
        {config.displayName(d) || `مستند ${d.id}`}
      </Link>
    ) : (
      config.displayName(d) || `مستند ${d.id}`
    );

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
        <h2 className="text-2xl font-bold text-gray-800">{config.title}</h2>
        {config.showBackLink && (
          <Link
            to="/documents"
            className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
          >
            ← الملفات التنفيذية
          </Link>
        )}
      </div>

      <div className="bg-white rounded-xl shadow p-4 mb-6 flex flex-col sm:flex-row gap-3">
        <input
          value={query}
          onChange={(e) => { setQuery(e.target.value); setPage(1); }}
          placeholder={config.searchPlaceholder}
          className="flex-1 min-w-64 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
        />
      </div>

      {error && <div className="text-red-600 mb-4">{error}</div>}
      {message && <div className="text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-2 mb-4">{message}</div>}

      {loading && <div className="text-gray-500">جارِ البحث...</div>}

      {data && (
        <>
          {isMobile ? (
            <div className="flex flex-col gap-4">
              {data.items.map((d) => (
                <article key={d.id} className="bg-white rounded-xl shadow p-4">
                  <div className="flex items-start justify-between gap-2 mb-2">
                    {nameOnCard(d)}
                    {config.cardTopRight(d)}
                  </div>
                  <div className="text-sm text-gray-600">
                    {applicantName(d) || '—'} · {d.branchName || '—'} · {d.court || '—'}
                  </div>
                  <div className="text-sm font-medium text-gray-800 mt-1">
                    رقم الملف: {displayFileNumber(d) || '—'}
                  </div>
                  {config.cardBottomExtra?.(d)}
                  {config.canRestore && (
                    <div className="mt-3 pt-3 border-t border-gray-100">{restoreButton(d)}</div>
                  )}
                </article>
              ))}
              {data.items.length === 0 && (
                <div className="bg-white rounded-xl shadow p-8 text-center text-gray-400">
                  {config.emptyText}
                </div>
              )}
            </div>
          ) : (
            <div className="bg-white rounded-xl shadow overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-600">
                  <tr className="text-right">
                    <th className="px-4 py-3">{config.dateColumnHeader}</th>
                    <th className="px-4 py-3">المنفذ عليه</th>
                    <th className="px-4 py-3">طالب التنفيذ</th>
                    <th className="px-4 py-3">الفرع</th>
                    <th className="px-4 py-3">دائرة التنفيذ</th>
                    <th className="px-4 py-3">رقم الملف</th>
                    <th className="px-4 py-3">إجراء</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.items.map((d) => (
                    <tr key={d.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 whitespace-nowrap text-gray-600">{config.dateCell(d)}</td>
                      <td className="px-4 py-3 font-medium text-gray-800">{nameOnTable(d)}</td>
                      <td className="px-4 py-3">{applicantName(d) || '—'}</td>
                      <td className="px-4 py-3">{d.branchName || '—'}</td>
                      <td className="px-4 py-3">{d.court || '—'}</td>
                      <td className="px-4 py-3">{displayFileNumber(d)}</td>
                      <td className="px-4 py-3">{config.canRestore ? restoreButton(d) : '—'}</td>
                    </tr>
                  ))}
                  {data.items.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-4 py-8 text-center text-gray-400">
                        {config.emptyText}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          <div className="flex items-center justify-between mt-4 text-sm text-gray-600 flex-wrap gap-2">
            <span>
              صفحة {data.page} من {data.totalPages || 1} ({data.totalCount} نتيجة)
            </span>
            <div className="flex gap-2">
              <button
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                السابق
              </button>
              <button
                disabled={page >= data.totalPages}
                onClick={() => setPage(page + 1)}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                التالي
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
