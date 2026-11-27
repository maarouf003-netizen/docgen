import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { api, getApiErrorMessage } from '../../api/client';
import { useDebouncedCallback } from '../../hooks/useDebouncedCallback';
import { useFloatingMenu } from '../../hooks/useFloatingMenu';
import type {
  PublicEntityGroupDto,
  PublicEntityGroupListResponse,
} from '../../types';
import { RenameGroupModal } from '../../components/entity/RenameGroupModal';
import { MergeBranchesModal } from '../../components/entity/MergeBranchesModal';
import { AbolishBranchModal } from '../../components/entity/AbolishBranchModal';
import { f, type GroupPick } from '../../components/entity/reviewShared';

type MenuAction = 'rename' | 'merge' | 'abolish';

const PICK_PAGE_SIZE = 50;

/**
 * تبويب «تعديل جهة عامة»: بحث مؤجّل عن الهويات الأم، قائمة مختارة، وقائمة أفعال
 * (تعديل تسمية/دمج/حلول) تفتح نافذة التأكيد المناظرة بعد التحقق من عدد الاختيارات.
 */
export function EditEntityTab() {
  const [query, setQuery] = useState('');
  const [searchResults, setSearchResults] = useState<PublicEntityGroupDto[]>([]);
  const [searching, setSearching] = useState(false);
  const [selected, setSelected] = useState<GroupPick[]>([]);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const editMenu = useFloatingMenu();
  // تسلسل البحث: استجابة قديمة لا تطمس نتائج استعلام أحدث.
  const searchSeq = useRef(0);

  // حالة الأفعال
  const [modal, setModal] = useState<'rename' | 'merge' | 'abolish' | null>(null);

  const doSearch = async (q: string) => {
    const seq = ++searchSeq.current;
    if (!q.trim()) {
      setSearchResults([]);
      setSearching(false);
      setError('');
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
      if (seq !== searchSeq.current) return;
      setSearchResults(res.data.items ?? []);
    } catch (err) {
      if (seq !== searchSeq.current) return;
      setError(getApiErrorMessage(err));
    } finally {
      if (seq === searchSeq.current) setSearching(false);
    }
  };

  const debouncedSearch = useDebouncedCallback(doSearch, 300);

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
