import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useSearchParams } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import { GOVERNORATES } from '../utils/governorate';
import { useFloatingMenu } from '../hooks/useFloatingMenu';
import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
} from '../utils/entityRegistry';
import type {
  AbolishAndReplaceRequest,
  AbolishAndReplaceResponse,
  AbolishReplacePreviewResponse,
  AbolishReplacePreviewRequest,
  PublicEntityGroupDto,
  PublicEntityGroupListResponse,
  PublicEntityType,
  RenameGroupRequest,
  RenameGroupResponse,
  RenameGroupPreviewRequest,
  RenameGroupPreviewResponse,
} from '../types';
import EntityChangeLog from './EntityChangeLog';
import { SimilarGroupsUnifyTab } from '../components/entity/SimilarGroupsUnifyTab';

type TabId = 'edit' | 'add' | 'unify' | 'log';

const f = new Intl.NumberFormat('ar-EG');

/** مناداة المرجع «بموجب قرار/قانون/مرسوم». */
const DECREE_KINDS = ['قرار', 'قانون', 'مرسوم'];

interface GroupPick {
  groupId: number;
  canonicalName: string;
  entryCount: number;
  governorates: string[];
}

const PICK_PAGE_SIZE = 50;

/** تاريخ حر: نص مع placeholder «مثال: 1/8/2026». */
function FreeDateInput({
  id,
  value,
  onChange,
  required,
  disabled,
}: {
  id: string;
  value: string;
  onChange: (v: string) => void;
  required?: boolean;
  disabled?: boolean;
}) {
  return (
    <input
      id={id}
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder="مثال: 1/8/2026"
      autoComplete="off"
      required={required}
      disabled={disabled}
      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
    />
  );
}

/** رأس المرجع المشترك للأفعال الثلاثة. */
function DecreeFields({
  kind,
  number,
  date,
  onKind,
  onNumber,
  onDate,
  disabled,
  baseId,
}: {
  kind: string;
  number: string;
  date: string;
  onKind: (v: string) => void;
  onNumber: (v: string) => void;
  onDate: (v: string) => void;
  disabled?: boolean;
  baseId: string;
}) {
  return (
    <div className="grid sm:grid-cols-3 gap-3">
      <div>
        <label htmlFor={`${baseId}-kind`} className="block text-xs font-medium text-gray-600 mb-1">
          نوع المرجع
        </label>
        <select
          id={`${baseId}-kind`}
          value={kind}
          onChange={(e) => onKind(e.target.value)}
          disabled={disabled}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
        >
          <option value="">اختر النوع…</option>
          {DECREE_KINDS.map((k) => (
            <option key={k} value={k}>
              {k}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label htmlFor={`${baseId}-number`} className="block text-xs font-medium text-gray-600 mb-1">
          رقم المرجع
        </label>
        <input
          id={`${baseId}-number`}
          value={number}
          onChange={(e) => onNumber(e.target.value)}
          placeholder="مثال: 123…"
          autoComplete="off"
          disabled={disabled}
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
        />
      </div>
      <div>
        <label htmlFor={`${baseId}-date`} className="block text-xs font-medium text-gray-600 mb-1">
          تاريخ المرجع
        </label>
        <FreeDateInput id={`${baseId}-date`} value={date} onChange={onDate} disabled={disabled} />
      </div>
    </div>
  );
}

export default function EntityRegistryReviewManagement() {
  const [searchParams] = useSearchParams();
  const initialTab =
    searchParams.get('tab') === 'add'
      ? 'add'
      : searchParams.get('tab') === 'log'
        ? 'log'
        : searchParams.get('tab') === 'unify'
          ? 'unify'
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
      </div>

      {tab === 'edit' && <EditEntityTab key="edit" />}
      {tab === 'add' && <AddEntityTab />}
      {tab === 'unify' && <SimilarGroupsUnifyTab />}
      {tab === 'log' && <EntityChangeLog />}
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
        <RenameModal
          group={selected[0]}
          onClose={closeModal}
          onCommitted={(msg) => {
            setModal(null);
            setSuccess(msg);
          }}
        />
      )}
      {modal === 'merge' && (
        <MergeActionModal
          selected={selected}
          onClose={closeModal}
          onCommitted={(msg) => {
            setModal(null);
            setSuccess(msg);
          }}
        />
      )}
      {modal === 'abolish' && (
        <AbolishModal
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

/* ── نافذة «تعديل تسمية» (جهة واحدة) ───────────────────────────────── */

function RenameModal({
  group,
  onClose,
  onCommitted,
}: {
  group: GroupPick;
  onClose: () => void;
  onCommitted: (msg: string) => void;
}) {
  const [newName, setNewName] = useState('');
  const [confirmText, setConfirmText] = useState('');
  const [kind, setKind] = useState('');
  const [number, setNumber] = useState('');
  const [date, setDate] = useState('');
  const [preview, setPreview] = useState<RenameGroupPreviewResponse | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');

  const canPreview = newName.trim().length > 0;

  const loadPreview = async () => {
    setLoadingPreview(true);
    setError('');
    try {
      const req: RenameGroupPreviewRequest = { groupId: group.groupId, newCanonicalName: newName.trim() };
      const res = await api.post<RenameGroupPreviewResponse>(
        `/entity-registry/groups/${group.groupId}/rename-preview`,
        req,
      );
      setPreview(res.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoadingPreview(false);
    }
  };

  const commit = async () => {
    if (!kind.trim() || !number.trim() || !date.trim()) {
      setError('نوع المرجع ورقمه وتاريخه مطلوبة');
      return;
    }
    if (confirmText.trim() !== newName.trim()) {
      setError('أكّد بكتابة التسمية الجديدة حرفيًا للمتابعة');
      return;
    }
    setCommitting(true);
    setError('');
    try {
      const req: RenameGroupRequest = {
        groupId: group.groupId,
        newCanonicalName: newName.trim(),
        decreeKind: kind.trim(),
        decreeNumber: number.trim(),
        decreeDate: normalizeArabicDigits(date).trim(),
      };
      const res = await api.post<RenameGroupResponse>(
        `/entity-registry/groups/${group.groupId}/rename`,
        req,
      );
      onCommitted(
        `تم تعديل اسم الجهة من «${res.data.oldCanonicalName}» إلى «${res.data.newCanonicalName}» بموجب ${kind.trim()} — ${f.format(res.data.affectedDocuments)} ملفًا`,
      );
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setCommitting(false);
    }
  };

  return (
    <ActionModal
      title="تعديل تسمية جهة"
      subtitle={`${group.canonicalName} — ${f.format(group.entryCount)} قيد`}
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            onClick={commit}
            disabled={!canPreview || !preview || committing}
            className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm font-bold min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
          >
            {committing ? 'جارِ التنفيذ…' : 'تأكيد التنفيذ'}
          </button>
          <CloseButton onClick={onClose} />
        </>
      }
    >
      <div className="grid gap-4">
        <div>
          <label htmlFor="ren-new" className="block text-xs font-medium text-gray-600 mb-1">
            التسمية الجديدة
          </label>
          <input
            id="ren-new"
            value={newName}
            onChange={(e) => {
              setNewName(e.target.value);
              setPreview(null);
            }}
            placeholder="مثال: المديرية العامة للمصرف…"
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>

        {canPreview && !preview && (
          <button
            type="button"
            onClick={loadPreview}
            disabled={loadingPreview}
            className="bg-sky-700 hover:bg-sky-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
          >
            {loadingPreview ? 'جارِ المعاينة…' : 'معاينة التأثير'}
          </button>
        )}

        {error && (
          <p role="alert" className="text-red-600 text-sm">
            {error}
          </p>
        )}

        {preview && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 text-sm">
            <p className="text-gray-700 mb-1">
              سيتم تعديل اسم «{preview.oldCanonicalName}» إلى «{preview.newCanonicalName}».
            </p>
            <p className="text-gray-700 mb-2">
              الملفات المتأثرة: <strong className="tabular-nums">{f.format(preview.affectedDocuments)}</strong>
            </p>
            {preview.branches.length > 0 && (
              <div className="flex flex-wrap gap-1.5 mt-1">
                {preview.branches.map((b) => (
                  <span key={b} className="bg-white border border-gray-200 rounded-full px-2 py-0.5 text-xs text-gray-600">
                    {b}
                  </span>
                ))}
              </div>
            )}
          </div>
        )}

        {preview && (
          <div className="border-t border-gray-100 pt-3">
            <h4 className="text-sm font-bold text-gray-700 mb-2">المرجع (إلزامي)</h4>
            <DecreeFields
              baseId="ren"
              kind={kind}
              number={number}
              date={date}
              onKind={setKind}
              onNumber={setNumber}
              onDate={setDate}
            />
            {(!kind.trim() || !number.trim() || !date.trim()) && (
              <p className="text-xs text-red-600 mt-1">نوع المرجع ورقمه وتاريخه مطلوبة للتنفيذ</p>
            )}
            <div className="border-t border-gray-200 bg-amber-50 border-amber-100 rounded-lg p-3 mt-3">
              <p className="text-sm text-amber-800 mb-2">
                للتنفيذ، اكتب التسمية الجديدة حرفيًا: «{newName.trim()}»
              </p>
              <label htmlFor="ren-confirm" className="block text-xs text-gray-600 mb-1 sr-only">
                تأكيد كتابة التسمية الجديدة
              </label>
              <input
                id="ren-confirm"
                value={confirmText}
                onChange={(e) => setConfirmText(e.target.value)}
                autoComplete="off"
                aria-describedby={confirmText.length > 0 ? 'ren-confirm-hint' : undefined}
                aria-invalid={confirmText.length > 0 && confirmText.trim() !== newName.trim()}
                className={`w-full min-h-11 border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 ${
                  confirmText.length === 0
                    ? 'border-gray-300'
                    : confirmText.trim() === newName.trim()
                      ? 'border-emerald-400 bg-emerald-50/30'
                      : 'border-red-300 bg-red-50/30'
                }`}
              />
              {confirmText.length > 0 && (
                <p
                  id="ren-confirm-hint"
                  aria-live="polite"
                  className={`text-xs mt-1.5 ${confirmText.trim() === newName.trim() ? 'text-emerald-600' : 'text-red-600'}`}
                >
                  {confirmText.trim() === newName.trim() ? '✓ يطابق التسمية الجديدة' : '✗ لا يطابق — اكتبها حرفيًا'}
                </p>
              )}
            </div>
          </div>
        )}
      </div>
    </ActionModal>
  );
}

/* ── نافذة «دمج» (متعدد ← هدف) ─────────────────────────────────────── */

function MergeActionModal({
  selected,
  onClose,
  onCommitted,
}: {
  selected: GroupPick[];
  onClose: () => void;
  onCommitted: (msg: string) => void;
}) {
  const survivorId = selected[0].groupId;
  const [targetId, setTargetId] = useState<number>(survivorId);
  const [finalName, setFinalName] = useState('');
  const [kind, setKind] = useState('');
  const [number, setNumber] = useState('');
  const [date, setDate] = useState('');
  const [confirmText, setConfirmText] = useState('');
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');

  const absorbed = selected.filter((s) => s.groupId !== targetId);
  const target = selected.find((s) => s.groupId === targetId) ?? selected[0];

  const commit = async () => {
    if (!kind.trim() || !number.trim() || !date.trim()) {
      setError('نوع المرجع ورقمه وتاريخه مطلوبة');
      return;
    }
    if (confirmText.trim() !== target.canonicalName) {
      setError('أكّد بكتابة اسم الهوية الناجية للمتابعة');
      return;
    }
    setCommitting(true);
    setError('');
    try {
      const res = await api.post('/entity-registry/merge-commit', {
        survivorGroupId: targetId,
        absorbedGroupIds: absorbed.map((a) => a.groupId),
        unifyTexts: false,
        newCanonicalName: finalName.trim() || null,
        decreeKind: kind.trim(),
        decreeNumber: number.trim(),
        decreeDate: normalizeArabicDigits(date).trim(),
      });
      const r = res.data as { absorbedGroupsCount: number; entriesMigrated: number; totalAffectedDocuments: number };
      onCommitted(
        `تم دمج ${r.absorbedGroupsCount} هويات في «${finalName.trim() || target.canonicalName}» بموجب ${kind.trim()} — ${r.entriesMigrated} قيد، ${r.totalAffectedDocuments} ملفًا`,
      );
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setCommitting(false);
    }
  };

  return (
    <ActionModal
      title="دمج جهات عامة"
      subtitle="دمج الهويات المختارة في هوية أم ناجية"
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            onClick={commit}
            disabled={absorbed.length === 0 || committing}
            className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm font-bold min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
          >
            {committing ? 'جارِ التنفيذ…' : 'تأكيد الدمج'}
          </button>
          <CloseButton onClick={onClose} />
        </>
      }
    >
      <div className="grid gap-4">
        <div>
          <label htmlFor="mg-target" className="block text-xs font-medium text-gray-600 mb-1">
            الهوية الناجية (تبقى)
          </label>
          <select
            id="mg-target"
            value={targetId}
            onChange={(e) => {
              setTargetId(Number(e.target.value));
              setConfirmText('');
            }}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            {selected.map((s) => (
              <option key={s.groupId} value={s.groupId}>
                {s.canonicalName} — {f.format(s.entryCount)} قيد
              </option>
            ))}
          </select>
        </div>

        <div>
          <span className="block text-xs font-medium text-gray-600 mb-1">
            الهويات الممتصة ({f.format(absorbed.length)})
          </span>
          <ul className="border border-gray-200 rounded-lg p-2 text-sm space-y-1">
            {absorbed.map((a) => (
              <li key={a.groupId} className="flex items-center justify-between gap-2 py-1">
                <span className="truncate">{a.canonicalName}</span>
                <span className="text-xs text-gray-400 whitespace-nowrap tabular-nums">
                  {f.format(a.entryCount)} قيد
                </span>
              </li>
            ))}
            {absorbed.length === 0 && <li className="text-xs text-gray-400 py-1">لا توجد هويات ممتصة</li>}
          </ul>
        </div>

        <div>
          <label htmlFor="mg-final" className="block text-xs font-medium text-gray-600 mb-1">
            الاسم النهائي للنتيجة (اختياري)
          </label>
          <input
            id="mg-final"
            value={finalName}
            onChange={(e) => setFinalName(e.target.value)}
            placeholder="مثال: الهيئة العامة الموحدة…"
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>

        <div className="border-t border-gray-100 pt-3">
          <h4 className="text-sm font-bold text-gray-700 mb-2">المرجع (إلزامي)</h4>
          <DecreeFields
            baseId="mg"
            kind={kind}
            number={number}
            date={date}
            onKind={setKind}
            onNumber={setNumber}
            onDate={setDate}
          />
        </div>

        <div className="border-t border-gray-200 bg-amber-50 border-amber-100 rounded-lg p-3">
          <p className="text-sm text-amber-800 mb-2">
            للتنفيذ، اكتب اسم الهوية الناجية: «{target.canonicalName}»
          </p>
          <label htmlFor="mg-confirm" className="block text-xs text-gray-600 mb-1 sr-only">
            تأكيد كتابة اسم الهدف
          </label>
          <input
            id="mg-confirm"
            value={confirmText}
            onChange={(e) => setConfirmText(e.target.value)}
            autoComplete="off"
            aria-describedby={confirmText.length > 0 ? 'mg-confirm-hint' : undefined}
            aria-invalid={confirmText.length > 0 && confirmText.trim() !== target.canonicalName}
            className={`w-full min-h-11 border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 ${
              confirmText.length === 0
                ? 'border-gray-300'
                : confirmText.trim() === target.canonicalName
                  ? 'border-emerald-400 bg-emerald-50/30'
                  : 'border-red-300 bg-red-50/30'
            }`}
          />
          {confirmText.length > 0 && (
            <p
              id="mg-confirm-hint"
              aria-live="polite"
              className={`text-xs mt-1.5 ${confirmText.trim() === target.canonicalName ? 'text-emerald-600' : 'text-red-600'}`}
            >
              {confirmText.trim() === target.canonicalName ? '✓ يطابق اسم الناجية' : '✗ لا يطابق — اكتبه حرفيًا'}
            </p>
          )}
        </div>

        {error && (
          <p role="alert" className="text-red-600 text-sm">
            {error}
          </p>
        )}
      </div>
    </ActionModal>
  );
}

/* ── نافذة «حلول» (متعدد ← جهة جديدة) ──────────────────────────────── */

function AbolishModal({
  selected,
  onClose,
  onCommitted,
}: {
  selected: GroupPick[];
  onClose: () => void;
  onCommitted: (msg: string) => void;
}) {
  const [preview, setPreview] = useState<AbolishReplacePreviewResponse | null>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');

  const [name, setName] = useState('');
  const [confirmText, setConfirmText] = useState('');
  const [type, setType] = useState<PublicEntityType>('ministry');
  const [governorate, setGovernorate] = useState('');
  const [citation, setCitation] = useState<'add-to-job' | 'add-to-position'>('add-to-job');
  const [coverage, setCoverage] = useState('');
  const [showCoverage, setShowCoverage] = useState(false);
  const [kind, setKind] = useState('');
  const [number, setNumber] = useState('');
  const [date, setDate] = useState('');

  const loadPreview = async () => {
    setLoadingPreview(true);
    setError('');
    try {
      const req: AbolishReplacePreviewRequest = {
        abolishedGroupIds: selected.map((s) => s.groupId),
      };
      const res = await api.post<AbolishReplacePreviewResponse>(
        '/entity-registry/groups/abolish-preview',
        req,
      );
      setPreview(res.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoadingPreview(false);
    }
  };

  const commit = async () => {
    if (!name.trim()) {
      setError('اسم الجهة الجديدة مطلوب');
      return;
    }
    if (!governorate) {
      setError('المحافظة مطلوبة');
      return;
    }
    if (!kind.trim() || !number.trim() || !date.trim()) {
      setError('نوع المرجع ورقمه وتاريخه مطلوبة');
      return;
    }
    if (confirmText.trim() !== name.trim()) {
      setError('أكّد بكتابة اسم الجهة الجديدة حرفيًا للمتابعة');
      return;
    }
    setCommitting(true);
    setError('');
    try {
      const req: AbolishAndReplaceRequest = {
        abolishedGroupIds: selected.map((s) => s.groupId),
        newCanonicalName: name.trim(),
        entityType: type,
        governorate,
        citationFormula: citation,
        coverageLabel: showCoverage && coverage.trim() ? coverage.trim() : null,
        decreeKind: kind.trim(),
        decreeNumber: number.trim(),
        decreeDate: normalizeArabicDigits(date).trim(),
      };
      const res = await api.post<AbolishAndReplaceResponse>(
        '/entity-registry/groups/abolish-and-replace',
        req,
      );
      onCommitted(
        `حلّت الجهة «${res.data.newCanonicalName}» محل ${f.format(res.data.abolishedGroups)} هويات بموجب ${kind.trim()} — ${f.format(res.data.affectedDocuments)} ملفًا`,
      );
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setCommitting(false);
    }
  };

  return (
    <ActionModal
      title="حلول جهة عامة"
      subtitle="إلغاء الهويات المختارة واستبدالها بهوية أم جديدة"
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            onClick={commit}
            disabled={committing}
            className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm font-bold min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
          >
            {committing ? 'جارِ التنفيذ…' : 'تأكيد الحلول'}
          </button>
          <CloseButton onClick={onClose} />
        </>
      }
    >
      <div className="grid gap-4">
        <div>
          <span className="block text-xs font-medium text-gray-600 mb-1">
            الهويات المُلغاة ({f.format(selected.length)})
          </span>
          <ul className="border border-gray-200 rounded-lg p-2 text-sm space-y-1">
            {selected.map((s) => (
              <li key={s.groupId} className="flex items-center justify-between gap-2 py-1">
                <span className="truncate">{s.canonicalName}</span>
                <span className="text-xs text-gray-400 whitespace-nowrap tabular-nums">
                  {f.format(s.entryCount)} قيد
                </span>
              </li>
            ))}
          </ul>
        </div>

        {!preview && (
          <button
            type="button"
            onClick={loadPreview}
            disabled={loadingPreview}
            className="bg-sky-700 hover:bg-sky-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
          >
            {loadingPreview ? 'جارِ المعاينة…' : 'معاينة التأثير'}
          </button>
        )}

        {preview && <AbolishPreviewBox preview={preview} />}

        {error && (
          <p role="alert" className="text-red-600 text-sm">
            {error}
          </p>
        )}

        <div className="border-t border-gray-100 pt-4">
          <h4 className="text-sm font-bold text-gray-700 mb-3">الجهة الجديدة التي حلت محلها</h4>
          <div className="grid gap-4">
            <div>
              <label htmlFor="ab-new" className="block text-xs font-medium text-gray-600 mb-1">
                اسم الجهة الجديدة
              </label>
              <input
                id="ab-new"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="مثال: المديرية العامة الموحدة…"
                autoComplete="off"
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
            <div className="grid sm:grid-cols-3 gap-4">
              <div>
                <label htmlFor="ab-type" className="block text-xs font-medium text-gray-600 mb-1">
                  نوع الجهة
                </label>
                <select
                  id="ab-type"
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
                <label htmlFor="ab-citation" className="block text-xs font-medium text-gray-600 mb-1">
                  صيغة ممثلها
                </label>
                <select
                  id="ab-citation"
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
                <label htmlFor="ab-gov" className="block text-xs font-medium text-gray-600 mb-1">
                  المحافظة
                </label>
                <select
                  id="ab-gov"
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
            </div>
            <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
              <input
                type="checkbox"
                checked={showCoverage}
                onChange={(e) => {
                  setShowCoverage(e.target.checked);
                  if (!e.target.checked) setCoverage('');
                }}
                className="h-4 w-4"
              />
              التغطية تشمل أكثر من محافظة
            </label>
            {showCoverage && (
              <div>
                <label htmlFor="ab-coverage" className="block text-xs font-medium text-gray-600 mb-1">
                  تسمية التغطية
                </label>
                <input
                  id="ab-coverage"
                  value={coverage}
                  onChange={(e) => setCoverage(e.target.value)}
                  placeholder="مثال: دمشق وريفها"
                  maxLength={150}
                  autoComplete="off"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
            )}
          </div>
        </div>

        <div className="border-t border-gray-100 pt-4">
          <h4 className="text-sm font-bold text-gray-700 mb-3">المرجع (إلزامي)</h4>
          <DecreeFields
            baseId="ab"
            kind={kind}
            number={number}
            date={date}
            onKind={setKind}
            onNumber={setNumber}
            onDate={setDate}
          />
        </div>

        <div className="border-t border-gray-200 bg-amber-50 border-amber-100 rounded-lg p-3">
          <p className="text-sm text-amber-800 mb-2">
            للتنفيذ، اكتب اسم الجهة الجديدة حرفيًا: «{name.trim()}»
          </p>
          <label htmlFor="ab-confirm" className="block text-xs text-gray-600 mb-1 sr-only">
            تأكيد كتابة اسم الجهة الجديدة
          </label>
          <input
            id="ab-confirm"
            value={confirmText}
            onChange={(e) => setConfirmText(e.target.value)}
            autoComplete="off"
            aria-describedby={confirmText.length > 0 ? 'ab-confirm-hint' : undefined}
            aria-invalid={confirmText.length > 0 && confirmText.trim() !== name.trim()}
            className={`w-full min-h-11 border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 ${
              confirmText.length === 0
                ? 'border-gray-300'
                : confirmText.trim() === name.trim()
                  ? 'border-emerald-400 bg-emerald-50/30'
                  : 'border-red-300 bg-red-50/30'
            }`}
          />
          {confirmText.length > 0 && (
            <p
              id="ab-confirm-hint"
              aria-live="polite"
              className={`text-xs mt-1.5 ${confirmText.trim() === name.trim() ? 'text-emerald-600' : 'text-red-600'}`}
            >
              {confirmText.trim() === name.trim() ? '✓ يطابق اسم الجهة الجديدة' : '✗ لا يطابق — اكتبها حرفيًا'}
            </p>
          )}
        </div>
      </div>
    </ActionModal>
  );
}

/** عرض معاينة الحلول. */
function AbolishPreviewBox({ preview }: { preview: AbolishReplacePreviewResponse }) {
  return (
    <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 text-sm">
      <p className="text-gray-700 mb-1">
        الملفات المتأثرة: <strong className="tabular-nums">{f.format(preview.affectedDocuments)}</strong> ،
        القيود النشطة: <strong className="tabular-nums">{f.format(preview.activeEntries)}</strong>
      </p>
      <p className="text-gray-600 mb-1">
        مندوبون بحاجة لإعادة توجيه: <strong className="tabular-nums">{f.format(preview.delegatesToReassign)}</strong>
      </p>
      {preview.branches.length > 0 && (
        <div className="flex flex-wrap gap-1.5 mt-1">
          {preview.branches.map((b) => (
            <span key={b} className="bg-white border border-gray-200 rounded-full px-2 py-0.5 text-xs text-gray-600">
              {b}
            </span>
          ))}
        </div>
      )}
    </div>
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

function ActionModal({
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

function CloseButton({ onClick }: { onClick: () => void }) {
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
