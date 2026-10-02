import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useSearchParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { GOVERNORATES } from '../utils/governorate';
import { useFloatingMenu } from '../hooks/useFloatingMenu';
import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
} from '../utils/entityRegistry';
import type {
  PublicEntityGroupDto,
  PublicEntityGroupListResponse,
  PublicEntityType,
} from '../types';
import EntityChangeLog from './EntityChangeLog';
import { UnifyNamesTab } from '../components/entity/UnifyNamesTab';
import ParentSuggestionsTab from '../components/entity/ParentSuggestionsTab';
import { RenameGroupModal } from '../components/entity/RenameGroupModal';
import { MergeBranchesModal } from '../components/entity/MergeBranchesModal';
import { AbolishBranchModal } from '../components/entity/AbolishBranchModal';
import { f, type GroupPick } from '../components/entity/reviewShared';

type TabId = 'edit' | 'add' | 'unify' | 'log' | 'suggestions';

const PICK_PAGE_SIZE = 50;

export default function EntityRegistryReviewManagement() {
  const [searchParams] = useSearchParams();
  const initialTab =
    searchParams.get('tab') === 'add'
      ? 'add'
      : searchParams.get('tab') === 'log'
        ? 'log'
        : searchParams.get('tab') === 'unify'
          ? 'unify'
          : searchParams.get('tab') === 'suggestions'
            ? 'suggestions'
            : 'edit';
  const [tab, setTab] = useState<TabId>(initialTab);

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-5">
        <h2 className="text-2xl font-bold text-gray-800 text-wrap-balance">
          مراجعة سجل الجهات العامة
        </h2>
      </div>

      {/* تبويبات */}
      <div
        className="flex flex-wrap gap-2 mb-6"
        role="tablist"
        aria-label="أقسام مراجعة سجل الجهات العامة"
      >
        <TabButton id="edit" label="تعديل جهة عامة" active={tab === 'edit'} onSelect={setTab} />
        <TabButton id="add" label="إضافة جهة" active={tab === 'add'} onSelect={setTab} />
        <TabButton id="unify" label="توحيد تسميات" active={tab === 'unify'} onSelect={setTab} />
        <TabButton id="log" label="سجل تغييرات الجهة" active={tab === 'log'} onSelect={setTab} />
        <TabButton id="suggestions" label="اقتراحات الأم" active={tab === 'suggestions'} onSelect={setTab} />
      </div>

      {tab === 'edit' && <EditEntityTab key="edit" />}
      {tab === 'add' && <AddEntityTab />}
      {tab === 'unify' && <UnifyNamesTab />}
      {tab === 'log' && <EntityChangeLog />}
      {tab === 'suggestions' && <ParentSuggestionsTab />}
    </div>
  );
}

function TabButton({
  id,
  label,
  active,
  onSelect,
  disabled,
}: {
  id: TabId;
  label: string;
  active: boolean;
  onSelect: (t: TabId) => void;
  disabled?: boolean;
}) {
  const base =
    'rounded-lg px-4 py-2 text-sm font-medium min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500';
  if (disabled) {
    const hintId = `${id}-hint`;
    return (
      <>
        <button
          type="button"
          role="tab"
          aria-disabled="true"
          aria-describedby={hintId}
          disabled
          title="مؤجل للمرحلة القادمة"
          className={`${base} bg-gray-100 text-gray-400 cursor-not-allowed`}
        >
          {label}
          <span className="block text-[11px] font-normal">قريبًا</span>
        </button>
        <span id={hintId} className="sr-only">
          سيُتاح في المرحلة القادمة
        </span>
      </>
    );
  }
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={() => onSelect(id)}
      className={`${base} ${
        active ? 'bg-emerald-700 text-white' : 'bg-white text-gray-700 hover:bg-emerald-50 border'
      }`}
    >
      {label}
    </button>
  );
}

/* ── تبويب «تعديل جهة عامة» ─────────────────────────────────────────── */

type MenuAction = 'rename' | 'merge' | 'abolish';

function EditEntityTab() {
  const [query, setQuery] = useState('');
  const [searchResults, setSearchResults] = useState<PublicEntityGroupDto[]>([]);
  const [searching, setSearching] = useState(false);
  const [selected, setSelected] = useState<GroupPick[]>([]);
  const [error, setError] = useState('');
  const editMenu = useFloatingMenu();

  // حالة الأفعال
  const [modal, setModal] = useState<'rename' | 'merge' | 'abolish' | null | 'waiting'>(
    null,
  );

  const doSearch = async (q: string) => {
    if (!q.trim()) {
      setSearchResults([]);
      return;
    }
    setSearching(true);
    setError('');
    try {
      const res = await api.get<PublicEntityGroupListResponse>('/entity-registry/groups', {
        params: {
          q: q.trim(),
          perPage: PICK_PAGE_SIZE,
          excludeIds: selected.map((s) => s.groupId).join(',') || undefined,
        },
      });
      setSearchResults(res.data.items ?? []);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSearching(false);
    }
  };

  const debouncedSearch = useDebounced(doSearch, 300);

  useEffect(() => {
    debouncedSearch(query);
  }, [query, debouncedSearch, selected]);

  const addPick = (g: PublicEntityGroupDto) => {
    setSelected((prev) =>
      prev.some((p) => p.groupId === g.groupId)
        ? prev
        : [
            ...prev,
            {
              groupId: g.groupId,
              canonicalName: g.canonicalName,
              entryCount: g.entryCount,
              governorates: g.governorates ?? [],
            },
          ],
    );
    setSearchResults([]);
    setQuery('');
    setError('');
  };

  const removePick = (groupId: number) => {
    setSelected((prev) => prev.filter((p) => p.groupId !== groupId));
    setError('');
  };

  const runAction = (action: MenuAction) => {
    editMenu.setOpen(false);
    setError('');
    if (action === 'rename' && selected.length !== 1) {
      setError('اختر جهة واحدة لتعديل تسميتها');
      return;
    }
    if (action === 'merge' && selected.length < 2) {
      setError('اختر جهتين على الأقل للدمج');
      return;
    }
    if (action === 'abolish' && selected.length === 0) {
      setError('اختر جهة واحدة على الأقل للحلول');
      return;
    }
    setModal(action);
  };

  const [success, setSuccess] = useState('');

  const closeModal = () => {
    setModal(null);
    setError('');
  };

  return (
    <div>
      <p className="text-sm text-gray-600 mb-4">
        ابحث عن الهويات الأم وأضفها إلى قائمة الجهات المختارة، ثم نفّذ أحد الأفعال (تعديل تسمية /
        دمج / حلول) بالزر الأخضر.
      </p>

      {success && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg p-3 text-sm">
          {success}
        </p>
      )}

      {/* البحث */}
      <div className="mb-4">
        <label htmlFor="revm-search" className="sr-only">
          بحث باسم الجهة
        </label>
        <input
          id="revm-search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="بحث باسم الهوية الأم ثم اضغط للاختيار…"
          autoComplete="off"
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
        {searching && <p className="text-xs text-gray-500 mt-1">جارِ البحث…</p>}
      </div>

      {/* نتائج البحث */}
      {searchResults.length > 0 && (
        <div className="mb-4 bg-white border border-gray-200 rounded-lg overflow-hidden shadow-sm">
          <ul className="divide-y divide-gray-100">
            {searchResults.map((g) => (
              <li key={g.groupId}>
                <button
                  type="button"
                  onClick={() => addPick(g)}
                  className="w-full text-right px-4 py-2.5 hover:bg-emerald-50 flex items-center justify-between gap-2 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  <span className="truncate">{g.canonicalName}</span>
                  <span className="text-xs text-gray-500 whitespace-nowrap tabular-nums">
                    {f.format(g.entryCount)} قيد
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {selected.length > 0 && (
        <button
          type="button"
          onClick={() => setSelected([])}
          className="text-sm text-red-700 hover:underline mb-2 min-h-11 focus-visible:ring-2 focus-visible:ring-red-500 rounded-lg px-2"
        >
          مسح الكل
        </button>
      )}

      {/* القائمة المختارة — مميزة بصريًا عن نتائج البحث */}
      <div className="mb-4 bg-white border border-gray-200 rounded-xl p-3 shadow-sm">
        <h3 className="text-sm font-bold text-gray-700 mb-2 flex items-center gap-2">
          الجهات المختارة
          <span className="bg-emerald-700 text-white rounded-full px-2 py-0.5 text-xs tabular-nums min-w-6 text-center">
            {f.format(selected.length)}
          </span>
        </h3>
        {selected.length === 0 ? (
          <p className="text-sm text-gray-400">لم تُختر جهات بعد</p>
        ) : (
          <ul className="space-y-1.5">
            {selected.map((s) => (
              <li
                key={s.groupId}
                className="flex items-center justify-between gap-2 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2.5"
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium text-emerald-900">{s.canonicalName}</span>
                  {s.governorates.length > 0 && (
                    <span className="flex flex-wrap gap-1 mt-1">
                      {s.governorates.slice(0, 3).map((gov) => (
                        <span
                          key={gov}
                          className="bg-white border border-emerald-200 rounded-full px-2 py-0.5 text-[11px] text-emerald-800"
                        >
                          {gov}
                        </span>
                      ))}
                      {s.governorates.length > 3 && (
                        <span className="text-[11px] text-gray-500">+{s.governorates.length - 3}</span>
                      )}
                    </span>
                  )}
                </span>
                <span className="flex items-center gap-2 shrink-0">
                  <span className="hidden sm:inline bg-white border border-gray-200 rounded-full px-2 py-0.5 text-[11px] text-gray-600 tabular-nums">
                    #{f.format(s.groupId)}
                  </span>
                  <span className="text-xs text-gray-600 whitespace-nowrap tabular-nums">
                    {f.format(s.entryCount)} قيد
                  </span>
                  <button
                    type="button"
                    onClick={() => removePick(s.groupId)}
                    aria-label={`إزالة ${s.canonicalName} من المختارة`}
                    className="text-red-600 hover:text-red-800 text-lg leading-none px-2 min-h-11 focus-visible:ring-2 focus-visible:ring-red-500 rounded-lg"
                  >
                    ×
                  </button>
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>

      {error && (
        <p role="alert" className="text-red-600 text-sm mb-4">
          {error}
        </p>
      )}

      {/* زر الإجراءات — أخضر نظام (التأكيدات الخطرة تبقى حمراء داخل النوافذ) */}
      <button
        ref={editMenu.refs.setReference}
        type="button"
        {...editMenu.getReferenceProps()}
        disabled={selected.length === 0}
        aria-haspopup="menu"
        aria-expanded={editMenu.open}
        className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-5 py-2 text-sm font-bold min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 shadow-sm"
      >
        تعديل ▾
      </button>
      {editMenu.open &&
        createPortal(
          <div
            ref={editMenu.refs.setFloating}
            role="menu"
            aria-label="قائمة إجراءات الجهة"
            style={editMenu.floatingStyles}
            {...editMenu.getFloatingProps()}
            className="fixed z-50 w-48 bg-white rounded-lg shadow-lg border border-gray-200 py-1"
          >
            <MenuItem label="تعديل تسمية" onClick={() => runAction('rename')} disabled={selected.length !== 1} />
            <MenuItem label="دمج" onClick={() => runAction('merge')} disabled={selected.length < 2} />
            <MenuItem label="حلول" onClick={() => runAction('abolish')} disabled={selected.length === 0} />
          </div>,
          document.body,
        )}

      {/* النوافذ */}
      {modal === 'rename' && selected.length === 1 && (
        <RenameGroupModal
          group={selected[0]}
          onClose={closeModal}
          onCommitted={(msg) => {
            setModal(null);
            setSuccess(msg);
          }}
        />
      )}
      {modal === 'merge' && (
        <MergeBranchesModal
          selected={selected}
          onClose={closeModal}
          onCommitted={(msg) => {
            setModal(null);
            setSuccess(msg);
          }}
        />
      )}
      {modal === 'abolish' && (
        <AbolishBranchModal
          selected={selected}
          onClose={closeModal}
          onCommitted={(msg) => {
            setModal(null);
            setSuccess(msg);
          }}
        />
      )}
    </div>
  );
}

function MenuItem({
  label,
  onClick,
  disabled,
}: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      role="menuitem"
      onClick={onClick}
      disabled={disabled}
      className="block w-full text-right px-4 py-2 text-sm text-gray-800 hover:bg-red-50 hover:text-red-800 disabled:opacity-40 disabled:hover:bg-transparent disabled:hover:text-gray-800 min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
    >
      {label}
    </button>
  );
}

/* ── تبويب «إضافة جهة» ─────────────────────────────────────────────── */

function AddEntityTab() {
  const [name, setName] = useState('');
  const [type, setType] = useState<PublicEntityType>('ministry');
  const [governorate, setGovernorate] = useState('');
  const [branch, setBranch] = useState('الجهة الأم');
  const [citation, setCitation] = useState<'add-to-job' | 'add-to-position'>('add-to-job');
  const [aliases, setAliases] = useState('');
  const [showCoverage, setShowCoverage] = useState(false);
  const [coverageLabel, setCoverageLabel] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('اسم الجهة مطلوب');
      return;
    }
    if (!governorate) {
      setError('المحافظة مطلوبة');
      return;
    }
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      await api.post('/entity-registry', {
        canonicalName: name.trim(),
        entityType: type,
        governorate,
        branchName: branch.trim(),
        citationFormula: citation,
        aliases: aliases.split('\n').map((a) => a.trim()).filter(Boolean),
        coverageLabel: showCoverage && coverageLabel.trim() ? coverageLabel.trim() : null,
        isParentEntity: true,
      });
      setName('');
      setBranch('الجهة الأم');
      setAliases('');
      setShowCoverage(false);
      setCoverageLabel('');
      setSuccess('تمت إضافة الجهة بنجاح');
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="bg-white rounded-xl shadow p-4 grid sm:grid-cols-2 gap-4">
      <div className="sm:col-span-2">
        <label htmlFor="add-name" className="block text-xs font-medium text-gray-600 mb-1">
          اسم الجهة المعتمد
        </label>
        <input
          id="add-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="مثال: المدير العام للمصرف التجاري السوري…"
          autoComplete="off"
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      </div>
      <div>
        <label htmlFor="add-type" className="block text-xs font-medium text-gray-600 mb-1">
          نوع الجهة
        </label>
        <select
          id="add-type"
          value={type}
          onChange={(e) => setType(e.target.value as PublicEntityType)}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
          {ENTITY_TYPE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="add-citation" className="block text-xs font-medium text-gray-600 mb-1">
          صيغة ممثلها القانوني
        </label>
        <select
          id="add-citation"
          value={citation}
          onChange={(e) => setCitation(e.target.value as typeof citation)}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
          {CITATION_FORMULA_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="add-gov" className="block text-xs font-medium text-gray-600 mb-1">
          المحافظة
        </label>
        <select
          id="add-gov"
          value={governorate}
          onChange={(e) => setGovernorate(e.target.value)}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
          <option value="">اختر المحافظة…</option>
          {GOVERNORATES.map((g) => (
            <option key={g} value={g}>
              {g}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor="add-branch" className="block text-xs font-medium text-gray-600 mb-1">
          الفرع
        </label>
        <input
          id="add-branch"
          value={branch}
          onChange={(e) => setBranch(e.target.value)}
          placeholder="مثال: فرع حمص…"
          autoComplete="off"
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      </div>
      <div className="sm:col-span-2">
        <label htmlFor="add-aliases" className="block text-xs font-medium text-gray-600 mb-1">
          أسماء بديلة (كل اسم في سطر)
        </label>
        <textarea
          id="add-aliases"
          value={aliases}
          onChange={(e) => setAliases(e.target.value)}
          rows={2}
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      </div>
      <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11 sm:col-span-2">
        <input
          type="checkbox"
          checked={showCoverage}
          onChange={(e) => {
            setShowCoverage(e.target.checked);
            if (!e.target.checked) setCoverageLabel('');
          }}
          className="h-4 w-4"
        />
        تغطية الجهة تشمل أكثر من محافظة
      </label>
      {showCoverage && (
        <div className="sm:col-span-2">
          <label htmlFor="add-coverage" className="block text-xs font-medium text-gray-600 mb-1">
            تسمية التغطية (حد أقصى 150 حرفًا)
          </label>
          <input
            id="add-coverage"
            value={coverageLabel}
            onChange={(e) => setCoverageLabel(e.target.value)}
            placeholder="مثال: دمشق وريف دمشق والقنيطرة"
            maxLength={150}
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
      )}
      {error && (
        <p role="alert" className="text-red-600 text-sm sm:col-span-2">
          {error}
        </p>
      )}
      {success && (
        <p role="status" className="text-emerald-700 text-sm sm:col-span-2">
          {success}
        </p>
      )}
      <div className="sm:col-span-2">
        <button
          type="submit"
          disabled={saving}
          className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
        >
          {saving ? 'جارِ الحفظ…' : 'إنشاء القيد'}
        </button>
      </div>
    </form>
  );
}

/* ── إطار النافذة المشترك ──────────────────────────────────────────── */

/* ── مساعد: بحث مؤجّل ──────────────────────────────────────────────── */

function useDebounced<T extends unknown[]>(fn: (...args: T) => void, delay: number) {
  const ref = useRef<ReturnType<typeof setTimeout> | null>(null);
  const fnRef = useRef(fn);
  fnRef.current = fn;
  const result = useMemo(() => {
    return (...args: T) => {
      if (ref.current) clearTimeout(ref.current);
      ref.current = setTimeout(() => fnRef.current(...args), delay);
    };
  }, [delay]);
  useEffect(() => () => {
    if (ref.current) clearTimeout(ref.current);
  }, []);
  return result;
}
