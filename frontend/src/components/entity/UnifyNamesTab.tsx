import { useCallback, useEffect, useRef, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import type {
  PublicEntityGroupDto,
  PublicEntityGroupListResponse,
  SimilarToItemDto,
  SimilarToResponse,
} from '../../types';
import { UnifyNamesModal } from './UnifyNamesModal';

// تنسيق عربي للأعداد (كما في EntityRegistryReviewManagement.tsx).
const arEgCount = new Intl.NumberFormat('ar-EG');

/**
 * تبويب «توحيد تسميات الجهات العامة» (المدير/المشرف فقط):
 * يعرض بطاقتي «كافة الجهات العامة» و«مشابهات الجهة المحددة» مباشرة دون تبويبات
 * فرعية — اختيار جهة من القائمة يعرض الجهات المشابهة لها، ويُفعَّل توحيد التسمية
 * عبر <UnifyNamesModal> بجعل الجهة المحددة هدفًا (تسميتها الصحيحة تبقى) والمشابهات
 * ممتصة. أي نجاح توحيد يُعيد جلب قائمة الجهات تلقائيًا (عبر refreshKey) إضافة
 * لزر «تحديث» اليدوي في بطاقة القائمة.
 */
export function UnifyNamesTab() {
  const [refreshKey, setRefreshKey] = useState(0);

  return (
    <AllEntitiesPanel refreshKey={refreshKey} onCommitted={() => setRefreshKey((k) => k + 1)} />
  );
}

/* ── بطاقتا «كافة الجهات العامة» و«مشابهات الجهة المحددة» ─────────────── */

function AllEntitiesPanel({ refreshKey, onCommitted }: { refreshKey: number; onCommitted: () => void }) {
  const [groups, setGroups] = useState<PublicEntityGroupDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState('');
  const debouncedQuery = useDebouncedValue(query, 300);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selected, setSelected] = useState<PublicEntityGroupDto | null>(null);
  const [suggestions, setSuggestions] = useState<SimilarToItemDto[]>([]);
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);
  const [suggestionError, setSuggestionError] = useState('');
  const [unifyTarget, setUnifyTarget] = useState<{ targetId: number; absorbedIds: number[] } | null>(null);
  const [success, setSuccess] = useState('');
  const listSeq = useRef(0);
  const suggestSeq = useRef(0);

  const load = useCallback(() => {
    const seq = ++listSeq.current;
    setLoading(true);
    setError('');
    api
      .get<PublicEntityGroupListResponse>('/entity-registry/groups', {
        // البحث يُنفَّذ خادميًا (يطبّع ويطابق الأسماء البديلة)؛ 100 هو حد التقميم الفعلي للخادم.
        params: { perPage: 100, q: debouncedQuery.trim() || undefined },
      })
      .then((res) => {
        if (seq === listSeq.current) {
          setGroups(res.data.items ?? []);
          setTotalCount(res.data.totalCount ?? 0);
        }
      })
      .catch((err) => {
        if (seq === listSeq.current) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (seq === listSeq.current) setLoading(false);
      });
  }, [debouncedQuery]);

  useEffect(() => {
    load();
  }, [load, refreshKey]);

  const selectGroup = async (g: PublicEntityGroupDto) => {
    const seq = ++suggestSeq.current;
    setSelected(g);
    setLoadingSuggestions(true);
    setSuggestionError('');
    api
      .get<SimilarToResponse>(`/entity-registry/groups/${g.groupId}/similar-to`, {
        params: { threshold: 0.55 },
      })
      .then((res) => {
        if (seq === suggestSeq.current) setSuggestions(res.data.items ?? []);
      })
      .catch((err) => {
        if (seq === suggestSeq.current) setSuggestionError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (seq === suggestSeq.current) setLoadingSuggestions(false);
      });
  };

  if (unifyTarget) {
    return (
      <UnifyNamesModal
        initialGroupId={unifyTarget.targetId}
        initialAbsorbedIds={unifyTarget.absorbedIds}
        onClose={() => setUnifyTarget(null)}
        onCommitted={(summary) => {
          setSuccess(summary);
          setUnifyTarget(null);
          setSelected(null);
          setSuggestions([]);
          onCommitted();
        }}
      />
    );
  }

  return (
    <div>
      {success && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg p-3 text-sm">
          {success}
        </p>
      )}
      <div className="grid lg:grid-cols-5 gap-4 items-start">
      <div className="lg:col-span-3 bg-white rounded-xl shadow">
        <div className="px-5 py-4 border-b border-gray-100">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <h3 className="text-base font-bold text-gray-800">كافة الجهات العامة</h3>
              <p className="text-xs text-gray-500 mt-0.5">
                اختر جهة واحدة لعرض الاقتراحات المشابهة لها وتوحيد تسميتها.
              </p>
            </div>
            <button
              onClick={load}
              className="text-sm text-emerald-700 hover:underline min-h-11 px-2 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
            >
              تحديث
            </button>
          </div>
          <div className="mt-3">
            <label htmlFor="all-entities-search" className="sr-only">بحث باسم الجهة</label>
            <input
              id="all-entities-search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="بحث باسم الجهة…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
        </div>

        <div className="max-h-[70vh] overflow-y-auto overscroll-contain divide-y divide-gray-100">
          {loading && <p className="px-5 py-6 text-sm text-gray-500">جارِ تحميل الجهات…</p>}
          {!loading && error && <p role="alert" className="px-5 py-6 text-red-600 text-sm">{error}</p>}
          {!loading && !error && groups.length === 0 && (
            <p className="px-5 py-6 text-sm text-gray-500 text-center">لا توجد جهات مطابقة.</p>
          )}
          {!loading && !error && totalCount > groups.length && (
            <p role="note" className="px-5 py-2 text-xs text-gray-500 bg-gray-50 tabular-nums">
              تُعرض أول {arEgCount.format(groups.length)} من أصل {arEgCount.format(totalCount)} — جرّب بحثًا أدقّ بعرض النتائج كاملة.
            </p>
          )}
          {groups.map((g) => (
            <button
              key={g.groupId}
              onClick={() => selectGroup(g)}
              aria-current={selected?.groupId === g.groupId ? 'true' : undefined}
              className={`w-full text-start px-5 py-3 min-h-11 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-500 ${
                selected?.groupId === g.groupId ? 'bg-emerald-50' : ''
              }`}
            >
              <span className="block font-medium text-gray-800 truncate">{g.canonicalName}</span>
              <span className="block text-xs text-gray-500 tabular-nums">
                {g.entryCount} قيد{g.linkedDocumentCount ? ` · ${g.linkedDocumentCount} ملف` : ''}
                {g.governorates.length > 0 ? ` · ${g.governorates.join('، ')}` : ''}
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* لوحة اقتراحات الجهة المحددة */}
      <div className="lg:col-span-2 bg-white rounded-xl shadow">
        <div className="px-5 py-4 border-b border-gray-100">
          <h3 className="text-base font-bold text-gray-800">
            {selected ? `مشابهات «${selected.canonicalName}»` : 'أقرب المشابهات'}
          </h3>
          <p className="text-xs text-gray-500 mt-0.5">
            {selected
              ? 'الجهات المتقاربة في الاسم — يمكن توحيد تسميتها مع الجهة المحددة.'
              : 'اختر جهة من القائمة لعرض الاقتراحات.'}
          </p>
        </div>
        <div className="p-4">
          {!selected && (
            <p className="text-sm text-gray-400 text-center py-8">لم تُحدَّد جهة بعد.</p>
          )}
          {selected && loadingSuggestions && (
            <p className="text-sm text-gray-500">جارِ تحليل التشابه…</p>
          )}
          {selected && !loadingSuggestions && suggestionError && (
            <p role="alert" className="text-red-600 text-sm">{suggestionError}</p>
          )}
          {selected && !loadingSuggestions && !suggestionError && suggestions.length === 0 && (
            <p className="text-sm text-gray-500 text-center py-6">لا توجد جهات مشابهة كافية.</p>
          )}
          {selected &&
            !loadingSuggestions &&
            !suggestionError &&
            suggestions.length > 0 && (
              <>
                <ul className="divide-y divide-gray-100">
                  {suggestions.map((s) => (
                    <li key={s.groupId} className="py-3">
                      <p className="font-medium text-gray-800 text-sm truncate">{s.canonicalName}</p>
                      <p className="text-xs text-gray-500 tabular-nums">
                        {s.entryCount} قيد{s.linkedDocumentCount > 0 ? ` · ${s.linkedDocumentCount} ملف` : ''} · تشابه{' '}
                        {Math.round(s.similarity * 100)}%
                      </p>
                    </li>
                  ))}
                </ul>
                <button
                  onClick={() => setUnifyTarget({ targetId: selected.groupId, absorbedIds: suggestions.map((s) => s.groupId) })}
                  className="mt-3 w-full bg-emerald-700 hover:bg-emerald-600 text-white rounded-lg px-4 py-2.5 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  توحيد تسمية المتشابهات مع الجهة المحددة ذات التسمية الصحيحة
                </button>
              </>
            )}
        </div>
      </div>
    </div>
    </div>
  );
}
