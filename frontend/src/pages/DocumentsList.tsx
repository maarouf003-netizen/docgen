import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import { richToPlainText } from '../utils/richText';
import { STATUS_BADGES, STATUS_OPTIONS, getDocumentStatus } from '../utils/documentStatus';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import type { DocumentResponse, PagedResult } from '../types';

function executedFullName(d: DocumentResponse): string {
  const person = d.executedNaturalPersons?.[0];
  const personName = person
    ? [person.name, person.father, person.family].filter(Boolean).join(' ')
    : '';
  const entity = d.executedPublicEntities?.[0]?.entityName ?? '';
  const applicant = d.applicant ?? '';
  return personName || entity || applicant || '';
}

function fullName(d: DocumentResponse) {
  if (d.generalEntitySide === 'executed') return executedFullName(d);
  return [d.borrowerName, d.borrowerFather, d.borrowerFamily].filter(Boolean).join(' ');
}

function displayFileNumber(d: DocumentResponse) {
  if (d.isDraft) return '';
  const number = d.displayFileNumber ?? d.fileNumber ?? '';
  const type = d.fileType ?? '';
  return type ? `${number} ${type}`.trim() : number;
}

function FileNumber({ d }: { d: DocumentResponse }) {
  const text = displayFileNumber(d);
  if (!text) return <span className="text-gray-800">{text}</span>;
  return (
    <span className={d.needsRotation ? 'text-red-600 font-bold' : 'text-gray-800'}>{text}</span>
  );
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
  const [search, setSearch] = useState('');
  const [position, setPosition] = useState<{ top: number; right: number } | null>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  const isActive = value !== '';
  const filtered = options.filter((o) => (search ? o.includes(search) : true));

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
    setSearch('');
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
        className="inline-flex items-start whitespace-nowrap min-h-11 text-sm font-bold text-emerald-900 hover:text-emerald-700 transition-colors"
      >
        <span className="inline-flex items-center gap-1">
          <span>{label}</span>
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className={`w-4 h-4 shrink-0 ${isActive ? 'text-red-600' : 'text-gray-400'}`}
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
            className="fixed z-50 w-64 max-h-96 overflow-hidden bg-white rounded-xl shadow-xl border border-gray-200"
            style={{ top: position.top, right: position.right }}
          >
            <div className="p-2 border-b border-gray-100">
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="بحث..."
                aria-label={`بحث في ${label}`}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
              />
            </div>
            <div className="max-h-72 overflow-y-auto py-1">
              <button
                type="button"
                role="menuitem"
                onClick={() => select('')}
                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-emerald-50"
              >
                {allLabel}
              </button>
              {filtered.map((o) => (
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
              {filtered.length === 0 && (
                <div className="px-4 py-3 text-sm text-gray-400">لا توجد نتائج مطابقة</div>
              )}
            </div>
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
          <div className="text-gray-800 truncate">{richToPlainText(latest.text)}</div>
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
  const [branch, setBranch] = useState('');
  const [administrativeBranch, setAdministrativeBranch] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<DocumentResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [exportMsg, setExportMsg] = useState('');
  const [actionsDocId, setActionsDocId] = useState<number | null>(null);
  const [applicants, setApplicants] = useState<string[]>([]);
  const [courts, setCourts] = useState<string[]>([]);
  const [lawyers, setLawyers] = useState<string[]>([]);
  const [branches, setBranches] = useState<string[]>([]);
  const [administrativeBranches, setAdministrativeBranches] = useState<string[]>([]);
  const canViewCounters = hasFullAccess || isHead;
  const canSeeAdministrativeBranch = hasFullAccess;
  const canSeeAssignedLawyer = hasFullAccess || isHead;
  const canSearchByLawyer = hasFullAccess || isHead;
  const canCreate = user?.role === 'lawyer';
  const canViewDeleted = user?.role === 'lawyer' || user?.role === 'head' || user?.role === 'admin';
  const canRotate = user?.role === 'lawyer';

  // يُمنع تصدير كل الملفات: يتطلب التصدير تطبيق فلتر واحد على الأقل (بحث أو أي فلتر منسدل)،
  // وإلا يُعطَّل الزر ويُعترض الطلب دفاعيًا قبل الإرسال.
  const hasActiveFilter = Boolean(
    query || status || applicant || court || lawyer || branch || administrativeBranch,
  );

  const load = () => {
    setLoading(true);
    const params = new URLSearchParams();
    if (query) params.set('q', query);
    if (status) params.set('status', status);
    if (applicant) params.set('applicant', applicant);
    if (court) params.set('court', court);
    if (lawyer) params.set('lawyer', lawyer);
    if (branch) params.set('branch', branch);
    if (administrativeBranch) params.set('administrativeBranch', administrativeBranch);
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
  }, [query, status, applicant, court, lawyer, branch, administrativeBranch, page]);

  useEffect(() => {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (applicant) params.set('applicant', applicant);
    if (court) params.set('court', court);
    if (lawyer) params.set('lawyer', lawyer);
    if (branch) params.set('branch', branch);
    if (administrativeBranch) params.set('administrativeBranch', administrativeBranch);
    const qs = params.toString();
    api
      .get<{ applicants: string[]; courts: string[]; lawyers?: string[]; branches: string[]; administrativeBranches: string[] }>(
        `/documents/filter-options${qs ? `?${qs}` : ''}`,
      )
      .then((r) => {
        setApplicants(Array.isArray(r.data.applicants) ? r.data.applicants : []);
        setCourts(Array.isArray(r.data.courts) ? r.data.courts : []);
        setLawyers(Array.isArray(r.data.lawyers) ? r.data.lawyers : []);
        setBranches(Array.isArray(r.data.branches) ? r.data.branches : []);
        setAdministrativeBranches(Array.isArray(r.data.administrativeBranches) ? r.data.administrativeBranches : []);
      })
      .catch(() => {});
  }, [status, applicant, court, lawyer, branch, administrativeBranch]);

  useEffect(() => {
    if (hasActiveFilter) setExportMsg('');
  }, [hasActiveFilter]);

  const exportExcel = () => {
    if (!hasActiveFilter) {
      setExportMsg('طبّق فلترًا واحدًا على الأقل قبل التصدير');
      return;
    }
    setExportMsg('');
    setExporting(true);
    const params = new URLSearchParams();
    if (query) params.set('q', query);
    if (status) params.set('status', status);
    if (applicant) params.set('applicant', applicant);
    if (court) params.set('court', court);
    if (lawyer) params.set('lawyer', lawyer);
    if (branch) params.set('branch', branch);
    if (administrativeBranch) params.set('administrativeBranch', administrativeBranch);
    const token = localStorage.getItem('docgen_token');
    const url = `/api/documents/export${params.toString() ? `?${params.toString()}` : ''}`;
    fetch(url, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
      .then((res) => {
        if (!res.ok) throw new Error('export failed');
        return res.blob();
      })
      .then((blob) => {
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = `الملفات التنفيذية ${new Date().toISOString().slice(0, 10)}.xlsx`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(link.href);
      })
      .catch(() => {})
      .finally(() => setExporting(false));
  };

  const colSpan =
    (canViewCounters ? 8 : 7) +
    (canSeeAssignedLawyer ? 1 : 0) +
    (canSeeAdministrativeBranch ? 1 : 0);

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
        <h2 className="text-2xl font-bold text-gray-800">الملفات التنفيذية</h2>
        <div className="flex items-center gap-2 flex-wrap">
          {canViewDeleted && (
            <Link
              to="/documents/deleted"
              className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
            >
              الملفات المحذوفة
            </Link>
          )}
          {canViewDeleted && (
            <Link
              to="/documents/struck-off"
              className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
            >
              الملفات المشطوبة
            </Link>
          )}
          {canRotate && (
            <Link
              to="/documents/rotate"
              className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
            >
              تدوير أرقام الأساس
            </Link>
          )}
          <button
            type="button"
            onClick={exportExcel}
            disabled={exporting}
            className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center disabled:opacity-50"
          >
            {exporting ? 'جارِ التصدير...' : 'تصدير إكسل'}
          </button>
          {canCreate && (
            <Link
              to="/documents/new"
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
            >
              + ادخال ملف جديد
            </Link>
          )}
        </div>
      </div>

      {exportMsg && (
        <div
          role="alert"
          className="bg-amber-50 border border-amber-300 text-amber-800 rounded-lg px-4 py-3 mb-6 text-sm"
        >
          {exportMsg}
        </div>
      )}

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
            {branches.length > 0 && (
              <FilterSelect
                value={branch}
                onChange={(v) => { setBranch(v); setPage(1); }}
                ariaLabel="فلترة الفرع"
                allLabel="كل الفروع"
                options={branches}
                className="sm:w-44"
              />
            )}
            {canSeeAdministrativeBranch && administrativeBranches.length > 0 && (
              <FilterSelect
                value={administrativeBranch}
                onChange={(v) => { setAdministrativeBranch(v); setPage(1); }}
                ariaLabel="فلترة فرع الإدارة"
                allLabel="كل فروع الإدارة"
                options={administrativeBranches}
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
                  {canSeeAdministrativeBranch && (
                    <div className="text-xs text-gray-500 mb-2">
                      فرع الإدارة: {d.administrativeBranchName || '—'}
                    </div>
                  )}
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
                  {canSeeAssignedLawyer && (
                    <div className="text-xs text-gray-500 mt-1">
                      المحامي المختص: {d.lawyer || '—'}
                    </div>
                  )}
                  <div className="text-sm font-medium text-gray-800 mt-1">
                    رقم الملف: {displayFileNumber(d) ? <FileNumber d={d} /> : '—'}
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
              <table className="w-full table-fixed text-sm">
                <thead className="bg-gray-50 text-emerald-900 font-bold">
                  <tr className="text-right">
                    {canSeeAdministrativeBranch && (
                      <th className="px-4 py-3 align-top w-[9%]">
                        <ColumnFilter
                          label="فرع الإدارة"
                          ariaLabel="فلترة فرع الإدارة"
                          value={administrativeBranch}
                          onChange={(v) => { setAdministrativeBranch(v); setPage(1); }}
                          allLabel="كل فروع الإدارة"
                          options={administrativeBranches}
                        />
                      </th>
                    )}
                    <th className="px-4 py-3 align-top w-[8%]">
                      <ColumnFilter
                        label="الحالة"
                        ariaLabel="فلترة الحالة"
                        value={status}
                        onChange={(v) => { setStatus(v); setPage(1); }}
                        allLabel="كل الحالات"
                        options={STATUS_OPTIONS}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[12%]">
                      <ColumnFilter
                        label="طالب التنفيذ"
                        ariaLabel="فلترة طالب التنفيذ"
                        value={applicant}
                        onChange={(v) => { setApplicant(v); setPage(1); }}
                        allLabel="كل طالبي التنفيذ"
                        options={applicants}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[8%]">
                      <ColumnFilter
                        label="الفرع"
                        ariaLabel="فلترة الفرع"
                        value={branch}
                        onChange={(v) => { setBranch(v); setPage(1); }}
                        allLabel="كل الفروع"
                        options={branches}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[14%]">المنفذ عليه</th>
                    <th className="px-4 py-3 align-top w-[11%]">
                      <ColumnFilter
                        label="دائرة التنفيذ"
                        ariaLabel="فلترة دائرة التنفيذ"
                        value={court}
                        onChange={(v) => { setCourt(v); setPage(1); }}
                        allLabel="كل دوائر التنفيذ"
                        options={courts}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[9%]">رقم الملف</th>
                    {canSeeAssignedLawyer && (
                      <th className="px-4 py-3 align-top w-[10%]">
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
                    <th className="px-4 py-3 align-top w-[14%]">الإجراءات والملاحظات</th>
                    {canViewCounters && <th className="px-4 py-3 align-top w-[5%]">عدد المشاهدات</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {data.items.map((d) => (
                    <tr key={d.id} className="hover:bg-gray-50">
                      {canSeeAdministrativeBranch && (
                        <td className="px-4 py-3">{d.administrativeBranchName || '—'}</td>
                      )}
                      <td className="px-4 py-3">
                        <StatusBadge d={d} />
                      </td>
                      <td className="px-4 py-3">{d.applicant || '—'}</td>
                      <td className="px-4 py-3">{d.branchName || '—'}</td>
                      <td className="px-4 py-3">
                        <Link to={`/documents/${d.id}`} className="inline-flex items-center min-h-11 text-emerald-800 font-bold hover:underline">
                          {fullName(d) || `مستند ${d.id}`}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{d.court || '—'}</td>
                      <td className="px-4 py-3">
                        <FileNumber d={d} />
                      </td>
                      {canSeeAssignedLawyer && (
                        <td className="px-4 py-3">{d.lawyer || '—'}</td>
                      )}
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
