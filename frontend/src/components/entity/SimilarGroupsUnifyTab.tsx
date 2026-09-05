import { useEffect, useRef, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import type {
  PublicEntityGroupDto,
  PublicEntityGroupListResponse,
  SimilarGroupClusterDto,
  SimilarGroupItemDto,
  SimilarGroupsResponse,
  SimilarToItemDto,
  SimilarToResponse,
} from '../../types';
import { UnifyNamesModal } from './UnifyNamesModal';

type SubTab = 'clusters' | 'all';

// تنسيق عربي للأعداد (كما في EntityRegistryReviewManagement.tsx).
const arEgCount = new Intl.NumberFormat('ar-EG');

// اختيار الهدف الافتراضي للمجموعة المتشابهة: الأعلى ارتباطًا بالملفات ثم بالقيود،
// والأول أبجديًا (ترتيب الخادم) يفوز عند التعادل — مقارنات صارمة > تُبقي الأول عند التزامن.
function pickDefaultTarget(groups: SimilarGroupItemDto[]) {
  let best = groups[0];
  for (let i = 1; i < groups.length; i++) {
    const g = groups[i];
    if (
      g.linkedDocumentCount > best.linkedDocumentCount
      || (g.linkedDocumentCount === best.linkedDocumentCount && g.entryCount > best.entryCount)
    ) {
      best = g;
    }
  }
  return best;
}

/**
 * تبويب «توحيد تسميات الجهات العامة» (المدير/المشرف فقط):
 * يحتوي على تبويبين فرعيين:
 * - «المجموعات المتشابهة»: كشف تلقائي (Union-Find) للجهات المتقاربة في الاسم.
 * - «كافة الجهات العامة»: قائمة مسطحة بعدّادات الملفات، وعند اختيار جهة واحدة
 *   تُعرض اقتراحات الجهات المشابهة لها في نافذة جانبية.
 * يُفعَّل توحيد التسمية عبر <UnifyNamesModal> باختيار الهدف والممتصة.
 */
export function SimilarGroupsUnifyTab() {
  const [subTab, setSubTab] = useState<SubTab>('clusters');

  return (
    <div>
      <div
        role="tablist"
        aria-label="أقسام توحيد التسمية"
        className="flex flex-wrap gap-2 mb-4"
      >
        <TabButton
          id="clusters"
          label="المجموعات المتشابهة"
          active={subTab === 'clusters'}
          onSelect={() => setSubTab('clusters')}
        />
        <TabButton
          id="all"
          label="كافة الجهات العامة"
          active={subTab === 'all'}
          onSelect={() => setSubTab('all')}
        />
      </div>

      {subTab === 'clusters' && <SimilarClustersPanel />}
      {subTab === 'all' && <AllEntitiesPanel />}
    </div>
  );
}

function TabButton({
  id,
  label,
  active,
  onSelect,
}: {
  id: SubTab;
  label: string;
  active: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      role="tab"
      id={`tab-${id}`}
      aria-selected={active}
      aria-controls={`panel-${id}`}
      onClick={onSelect}
      className={`rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 ${
        active
          ? 'bg-emerald-700 text-white font-medium'
          : 'bg-white text-gray-700 hover:bg-gray-50 border border-gray-200'
      }`}
    >
      {label}
    </button>
  );
}

/* ── تبويب «المجموعات المتشابهة» ────────────────────────────────────── */

function SimilarClustersPanel() {
  const [clusters, setClusters] = useState<SimilarGroupClusterDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [unifyTarget, setUnifyTarget] = useState<{ targetId: number; absorbedIds: number[] } | null>(null);

  const load = () => {
    setLoading(true);
    setError('');
    api
      .get<SimilarGroupsResponse>('/entity-registry/groups/similar-groups')
      .then((res) => setClusters(res.data.clusters ?? []))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  if (unifyTarget) {
    return (
      <UnifyNamesModal
        initialGroupId={unifyTarget.targetId}
        initialAbsorbedIds={unifyTarget.absorbedIds}
        onClose={() => setUnifyTarget(null)}
        onCommitted={() => {
          setUnifyTarget(null);
          load();
        }}
      />
    );
  }

  return (
    <div className="bg-white rounded-xl shadow">
      <div className="px-5 py-4 border-b border-gray-100 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-base font-bold text-gray-800">المجموعات المتشابهة</h3>
          <p className="text-xs text-gray-500 mt-0.5">
            كشف تلقائي للجهات المتقاربة في الاسم لتسهيل توحيد تسميتها.
          </p>
        </div>
        <button
          onClick={load}
          className="text-sm text-emerald-700 hover:underline min-h-11 px-2 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
        >
          تحديث
        </button>
      </div>

      <div className="p-5">
        {loading && <p className="text-sm text-gray-500">جارِ تحليل التشابه بين الجهات…</p>}
        {!loading && error && <p role="alert" className="text-red-600 text-sm">{error}</p>}

        {!loading && !error && clusters.length === 0 && (
          <div className="text-center py-8">
            <p className="text-gray-700 text-sm">لا توجد مجموعات متشابهة حاليًا.</p>
            <p className="text-xs text-gray-400 mt-1">ستظهر هنا الجهات التي تتقارب أسماؤها فوق عتبة التشابه.</p>
          </div>
        )}

        {clusters.map((cluster) => (
          <div key={cluster.clusterId} className="mb-5 border border-gray-200 rounded-xl overflow-hidden">
            <div className="bg-gray-50 px-4 py-2.5 flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm font-bold text-gray-800">
                مجموعة متشابهة — {cluster.groups.length} جهة
              </span>
              <span className="text-xs text-gray-500 tabular-nums">
                تشابه {Math.round(cluster.avgSimilarity * 100)}%
              </span>
            </div>
            <ul className="divide-y divide-gray-100">
              {cluster.groups.map((g) => (
                <li key={g.groupId} className="px-4 py-3 flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-medium text-gray-800 truncate">{g.canonicalName}</p>
                    <p className="text-xs text-gray-500 tabular-nums">
                      {g.entryCount} قيد{g.linkedDocumentCount > 0 ? ` · ${g.linkedDocumentCount} ملف` : ''}
                    </p>
                  </div>
                  <span className="text-xs text-gray-500 tabular-nums">
                    تشابه {Math.round(g.avgSimilarityToCluster * 100)}%
                  </span>
                </li>
              ))}
            </ul>
            <div className="px-4 py-2.5 bg-gray-50">
              <button
                onClick={() => {
                  const target = pickDefaultTarget(cluster.groups);
                  setUnifyTarget({
                    targetId: target.groupId,
                    absorbedIds: cluster.groups.filter((x) => x.groupId !== target.groupId).map((x) => x.groupId),
                  });
                }}
                className="text-sm text-emerald-700 font-medium hover:underline min-h-11 px-2 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
              >
                توحيد تسمية هذه المجموعة…
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ── تبويب «كافة الجهات العامة» ─────────────────────────────────────── */

function AllEntitiesPanel() {
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
  const panelRef = useRef<HTMLDivElement>(null);
  const suggestSeq = useRef(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    api
      .get<PublicEntityGroupListResponse>('/entity-registry/groups', {
        // البحث يُنفَّذ خادميًا (يطبّع ويطابق الأسماء البديلة)؛ 100 هو حد التقميم الفعلي للخادم.
        params: { perPage: 100, q: debouncedQuery.trim() || undefined },
      })
      .then((res) => {
        if (active) {
          setGroups(res.data.items ?? []);
          setTotalCount(res.data.totalCount ?? 0);
        }
      })
      .catch((err) => {
        if (active) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [debouncedQuery]);

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
        onCommitted={() => {
          setUnifyTarget(null);
          setSelected(null);
          setSuggestions([]);
        }}
      />
    );
  }

  return (
    <div className="grid lg:grid-cols-5 gap-4 items-start" ref={panelRef}>
      <div className="lg:col-span-3 bg-white rounded-xl shadow">
        <div className="px-5 py-4 border-b border-gray-100">
          <h3 className="text-base font-bold text-gray-800">كافة الجهات العامة</h3>
          <p className="text-xs text-gray-500 mt-0.5">
            اختر جهة واحدة لعرض الاقتراحات المشابهة لها وتوحيد تسميتها.
          </p>
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
              تُعرض أول {groups.length} من أصل {arEgCount.format(totalCount)} — جرّب بحثًا أدقّ بعرض النتائج كاملة.
            </p>
          )}
          {groups.map((g) => (
            <button
              key={g.groupId}
              onClick={() => selectGroup(g)}
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
                  توحيد تسمية الجهة المحددة مع هذه المشابهات…
                </button>
              </>
            )}
        </div>
      </div>
    </div>
  );
}
