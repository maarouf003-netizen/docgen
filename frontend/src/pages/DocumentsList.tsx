import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import { STATUS_BADGES, STATUS_OPTIONS, getDocumentStatus } from '../utils/documentStatus';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import type { DocumentResponse, PagedResult } from '../types';

function fullName(d: DocumentResponse) {
  return [d.borrowerName, d.borrowerFather, d.borrowerFamily].filter(Boolean).join(' ');
}

function displayFileNumber(d: DocumentResponse) {
  if (d.isDraft) return '';
  const number = d.fileNumber ?? '';
  const type = d.fileType ?? '';
  return type ? `${number} ${type}`.trim() : number;
}

function StatusBadge({ d }: { d: DocumentResponse }) {
  const { text, cls } = STATUS_BADGES[getDocumentStatus(d)];
  return <span className={`text-xs px-2 py-1 rounded-full ${cls}`}>{text}</span>;
}

type FilterSelectProps = {
  value: string;
  onChange: (value: string) => void;
  ariaLabel: string;
  allLabel: string;
  options: readonly string[];
  className?: string;
};

function FilterSelect({ value, onChange, ariaLabel, allLabel, options, className }: FilterSelectProps) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      aria-label={ariaLabel}
      className={`border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11 ${className ?? ''}`}
    >
      <option value="">{allLabel}</option>
      {options.map((o) => (
        <option key={o} value={o}>{o}</option>
      ))}
    </select>
  );
}

type ColumnFilterProps = {
  label: string;
  ariaLabel: string;
  value: string;
  onChange: (value: string) => void;
  allLabel: string;
  options: readonly string[];
};

function ColumnFilter({ label, ariaLabel, value, onChange, allLabel, options }: ColumnFilterProps) {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; right: number } | null>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  const isActive = value !== '';

  useEffect(() => {
    if (!open) return;
    const updatePosition = () => {
      const rect = buttonRef.current?.getBoundingClientRect();
      if (!rect) return;
      setPosition({ top: rect.bottom + 4, right: window.innerWidth - rect.right });
    };
    updatePosition();
    window.addEventListener('scroll', updatePosition, true);
    window.addEventListener('resize', updatePosition);
    return () => {
      window.removeEventListener('scroll', updatePosition, true);
      window.removeEventListener('resize', updatePosition);
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    const onPointerDown = (e: MouseEvent | TouchEvent) => {
      if (buttonRef.current?.contains(e.target as Node)) return;
      if (menuRef.current?.contains(e.target as Node)) return;
      setOpen(false);
    };
    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('touchstart', onPointerDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('touchstart', onPointerDown);
    };
  }, [open]);

  const select = (v: string) => {
    onChange(v);
    setOpen(false);
  };

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={ariaLabel}
        className={`inline-flex items-start whitespace-nowrap min-h-11 text-sm font-semibold hover:text-emerald-800 transition-colors ${
          isActive ? 'text-emerald-700' : 'text-gray-700'
        }`}
      >
        <span className="inline-flex items-center gap-1">
          <span>{label}</span>
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className={`w-4 h-4 shrink-0 ${isActive ? 'text-emerald-600' : 'text-gray-400'}`}
          >
            <path
              fillRule="evenodd"
              d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.06l3.71-3.83a.75.75 0 1 1 1.08 1.04l-4.25 4.39a.75.75 0 0 1-1.08 0L5.21 8.27a.75.75 0 0 1 .02-1.06Z"
              clipRule="evenodd"
            />
          </svg>
        </span>
      </button>
      {open &&
        position &&
        createPortal(
          <div
            ref={menuRef}
            role="menu"
            aria-label={ariaLabel}
            className="fixed z-50 min-w-48 max-h-80 overflow-y-auto bg-white rounded-xl shadow-xl border border-gray-200 py-1"
            style={{ top: position.top, right: position.right }}
          >
            <button
              type="button"
              role="menuitem"
              onClick={() => select('')}
              className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50"
            >
              {allLabel}
            </button>
            {options.map((o) => (
              <button
                key={o}
                type="button"
                role="menuitem"
                onClick={() => select(o)}
                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50"
              >
                {o}
              </button>
            ))}
          </div>,
          document.body,
        )}
    </>
  );
}

function ActionsCell({ d, onClick }: { d: DocumentResponse; onClick: () => void }) {
  const latest = d.executionActions?.[0];
  return (
    <button
      onClick={onClick}
      className="text-right w-full min-h-11 hover:underline"
      title="عرض الإجراءات والملاحظات"
    >
      {latest ? (
        <>
          <div className="text-gray-800 truncate">{latest.text}</div>
          <div className="text-emerald-600 text-xs mt-0.5">{latest.actionDate || '—'}</div>
        </>
      ) : (
        <span className="text-gray-400">لا توجد إجراءات أو ملاحظات</span>
      )}
    </button>
  );
}

export default function DocumentsList() {
  const { hasFullAccess, isHead, user } = useAuth();
  const isMobile = useIsMobile();
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('');
  const [applicant, setApplicant] = useState('');
  const [court, setCourt] = useState('');
  const [lawyer, setLawyer] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<DocumentResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [actionsDocId, setActionsDocId] = useState<number | null>(null);
  const [applicants, setApplicants] = useState<string[]>([]);
  const [courts, setCourts] = useState<string[]>([]);
  const [lawyers, setLawyers] = useState<string[]>([]);
  const canViewCounters = hasFullAccess || isHead;
  const canSeeAdministrativeBranch = hasFullAccess;
  const canSeeAssignedLawyer = hasFullAccess || isHead;
  const canSearchByLawyer = hasFullAccess || isHead;
  const canCreate = user?.role === 'lawyer';

  const load = () => {
    setLoading(true);
    const params = new URLSearchParams();
    if (query) params.set('q', query);
    if (status) params.set('status', status);
    if (applicant) params.set('applicant', applicant);
    if (court) params.set('court', court);
    if (lawyer) params.set('lawyer', lawyer);
    params.set('page', String(page));
    params.set('perPage', '20');
    api
      .get<PagedResult<DocumentResponse>>(`/documents?${params.toString()}`)
      .then((r) => setData(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, status, applicant, court, lawyer, page]);

  useEffect(() => {
    api
      .get<{ applicants: string[]; courts: string[]; lawyers?: string[] }>('/documents/filter-options')
      .then((r) => {
        setApplicants(Array.isArray(r.data.applicants) ? r.data.applicants : []);
        setCourts(Array.isArray(r.data.courts) ? r.data.courts : []);
        setLawyers(Array.isArray(r.data.lawyers) ? r.data.lawyers : []);
      })
      .catch(() => {});
  }, []);

  const colSpan =
    (canViewCounters ? 8 : 7) +
    (canSeeAssignedLawyer ? 1 : 0) +
    (canSeeAdministrativeBranch ? 1 : 0);

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-gray-800">الملفات التنفيذية</h2>
        {canCreate && (
          <Link
            to="/documents/new"
            className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
          >
            + ادخال ملف جديد
          </Link>
        )}
      </div>

      <div className="bg-white rounded-xl shadow p-4 mb-6 flex flex-col sm:flex-row flex-wrap gap-3">
        <input
          value={query}
          onChange={(e) => { setQuery(e.target.value); setPage(1); }}
          placeholder="بحث بالاسم الثنائي أو الثلاثي لأحد المنفذ عليهم، رقم العقد، دائرة التنفيذ..."
          className="flex-1 min-w-64 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
        />
        {isMobile && (
          <>
            <FilterSelect
              value={status}
              onChange={(v) => { setStatus(v); setPage(1); }}
              ariaLabel="فلترة الحالة"
              allLabel="كل الحالات"
              options={STATUS_OPTIONS}
              className="sm:w-44"
            />
            {applicants.length > 0 && (
              <FilterSelect
                value={applicant}
                onChange={(v) => { setApplicant(v); setPage(1); }}
                ariaLabel="فلترة طالب التنفيذ"
                allLabel="كل طالبي التنفيذ"
                options={applicants}
                className="sm:w-44"
              />
            )}
            {courts.length > 0 && (
              <FilterSelect
                value={court}
                onChange={(v) => { setCourt(v); setPage(1); }}
                ariaLabel="فلترة دائرة التنفيذ"
                allLabel="كل دوائر التنفيذ"
                options={courts}
                className="sm:w-44"
              />
            )}
            {canSearchByLawyer && lawyers.length > 0 && (
              <FilterSelect
                value={lawyer}
                onChange={(v) => { setLawyer(v); setPage(1); }}
                ariaLabel="فلترة المحامي المختص"
                allLabel="كل المحامين"
                options={lawyers}
                className="sm:w-44"
              />
            )}
          </>
        )}
      </div>

      {loading && <div className="text-gray-500">جارِ البحث...</div>}

      {data && (
        <>
          {isMobile ? (
            <div className="flex flex-col gap-4">
              {data.items.map((d) => (
                <article key={d.id} className="bg-white rounded-xl shadow p-4">
                  <div className="flex items-center justify-between mb-2">
                    <StatusBadge d={d} />
                    {canViewCounters && <span className="text-xs text-gray-500">مشاهدات: {d.viewCount}</span>}
                  </div>
                  <Link
                    to={`/documents/${d.id}`}
                    className="text-emerald-800 font-bold text-lg hover:underline flex items-center min-h-11 mb-1"
                  >
                    {fullName(d) || `مستند ${d.id}`}
                  </Link>
                  <div className="text-sm text-gray-600">
                    {d.applicant || '—'} · {d.branchName || '—'} · {d.court || '—'}
                  </div>
                  {(canSeeAssignedLawyer || canSeeAdministrativeBranch) && (
                    <div className="text-xs text-gray-500 mt-1 flex flex-wrap gap-x-3">
                      {canSeeAssignedLawyer && (
                        <span>المحامي المختص: {d.lawyer || '—'}</span>
                      )}
                      {canSeeAdministrativeBranch && (
                        <span>فرع الإدارة: {d.administrativeBranchName || '—'}</span>
                      )}
                    </div>
                  )}
                  <div className="text-sm font-medium text-gray-800 mt-1">
                    رقم الملف: {displayFileNumber(d) || '—'}
                  </div>
                  <div className="mt-3 pt-3 border-t border-gray-100">
                    <ActionsCell d={d} onClick={() => setActionsDocId(d.id)} />
                  </div>
                </article>
              ))}
              {data.items.length === 0 && (
                <div className="bg-white rounded-xl shadow p-8 text-center text-gray-400">
                  لا توجد نتائج
                </div>
              )}
            </div>
          ) : (
            <div className="bg-white rounded-xl shadow overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-600">
                  <tr className="text-right">
                    <th className="px-4 py-3 align-top">
                      <ColumnFilter
                        label="الحالة"
                        ariaLabel="فلترة الحالة"
                        value={status}
                        onChange={(v) => { setStatus(v); setPage(1); }}
                        allLabel="كل الحالات"
                        options={STATUS_OPTIONS}
                      />
                    </th>
                    <th className="px-4 py-3 align-top">
                      <ColumnFilter
                        label="طالب التنفيذ"
                        ariaLabel="فلترة طالب التنفيذ"
                        value={applicant}
                        onChange={(v) => { setApplicant(v); setPage(1); }}
                        allLabel="كل طالبي التنفيذ"
                        options={applicants}
                      />
                    </th>
                    {canSeeAssignedLawyer && (
                      <th className="px-4 py-3 align-top">
                        <ColumnFilter
                          label="المحامي المختص"
                          ariaLabel="فلترة المحامي المختص"
                          value={lawyer}
                          onChange={(v) => { setLawyer(v); setPage(1); }}
                          allLabel="كل المحامين"
                          options={canSearchByLawyer ? lawyers : []}
                        />
                      </th>
                    )}
                    <th className="px-4 py-3 align-top">الفرع</th>
                    {canSeeAdministrativeBranch && (
                      <th className="px-4 py-3 align-top">فرع الإدارة</th>
                    )}
                    <th className="px-4 py-3 align-top">المنفذ عليه</th>
                    <th className="px-4 py-3 align-top">
                      <ColumnFilter
                        label="دائرة التنفيذ"
                        ariaLabel="فلترة دائرة التنفيذ"
                        value={court}
                        onChange={(v) => { setCourt(v); setPage(1); }}
                        allLabel="كل دوائر التنفيذ"
                        options={courts}
                      />
                    </th>
                    <th className="px-4 py-3 align-top">رقم الملف</th>
                    <th className="px-4 py-3 align-top">الإجراءات والملاحظات</th>
                    {canViewCounters && <th className="px-4 py-3 align-top">عدد المشاهدات</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.items.map((d) => (
                    <tr key={d.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <StatusBadge d={d} />
                      </td>
                      <td className="px-4 py-3">{d.applicant || '—'}</td>
                      {canSeeAssignedLawyer && (
                        <td className="px-4 py-3">{d.lawyer || '—'}</td>
                      )}
                      <td className="px-4 py-3">{d.branchName || '—'}</td>
                      {canSeeAdministrativeBranch && (
                        <td className="px-4 py-3">{d.administrativeBranchName || '—'}</td>
                      )}
                      <td className="px-4 py-3">
                        <Link to={`/documents/${d.id}`} className="inline-flex items-center min-h-11 text-emerald-800 font-medium hover:underline">
                          {fullName(d) || `مستند ${d.id}`}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{d.court || '—'}</td>
                      <td className="px-4 py-3">{displayFileNumber(d)}</td>
                      <td className="px-4 py-3">
                        <ActionsCell d={d} onClick={() => setActionsDocId(d.id)} />
                      </td>
                      {canViewCounters && <td className="px-4 py-3">{d.viewCount}</td>}
                    </tr>
                  ))}
                  {data.items.length === 0 && (
                    <tr>
                      <td colSpan={colSpan} className="px-4 py-8 text-center text-gray-400">
                        لا توجد نتائج
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

      {actionsDocId !== null && (
        <ExecutionActionsModal
          documentId={actionsDocId}
          onClose={() => setActionsDocId(null)}
          onChanged={load}
        />
      )}
    </div>
  );
}
