import { useEffect, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { ProposeEditModal } from './ProposeEditModal';
import { GOVERNORATES, governorateFromBranch } from '../../utils/governorate';
import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
  entityTypeLabel,
  formatEntityCoverage,
  isEntryPendingReview,
  publicEntityStatusLabel,
} from '../../utils/entityRegistry';
import type {
  CreatePublicEntityRequest,
  CitationFormula,
  PublicEntityEntryDto,
  PublicEntityListResponse,
  PublicEntityType,
} from '../../types';

/** نص التحذير الحرفي المعتمد (د7) فوق اسم الجهة في نموذج الاقتراح. */
export const PROPOSAL_WARNING_TEXT =
  'يرجى ادخال اسم الجهة العامة بدقة مع ممثلها القانوني بدون عبارة اضافة لوظيفته أو منصبه تمثله ادارة قضايا الدولة';

/** الـplaceholder المعتمد لحقل اسم الجهة (د7). */
export const PROPOSAL_NAME_PLACEHOLDER = 'مثال: المدير العام للمصرف التجاري السوري';

interface PublicEntityPickerModalProps {
  onClose: () => void;
  /** يُستدعى عند اختيار قيد من نتائج البحث لربطه بالملف. */
  onPick: (entry: PublicEntityEntryDto) => void;
}

/**
 * نافذة «اختيار الجهة العامة» (§5 — د4/د7/د8/د9):
 * بحث واحد بنتائج قابلة للتبديل حسب المحافظة مع اقتراحات الفروع المستخلصة،
 * وبلا عدّاد ملفات إطلاقًا (د9)، وتحويل مباشر إلى نموذج إدخال جهة جديدة
 * تُخزَّن نهائيًا لكنها تبقى بانتظار مراجعة رئيس القسم فلا تظهر لبوات المندوبين
 * قبل الاعتماد (§6bis). قيود المراجعة تُعلَّم بصريًا (د4/§5.3).
 */
export function PublicEntityPickerModal({ onClose, onPick }: PublicEntityPickerModalProps) {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  // المحافظة الافتراضية = محافظة فرع المحامي (مثل «دمشق»)؛ إن لم تُطابق كتالوجًا فتُترك فارغة = الكل.
  const defaultGovernorate = governorateFromBranch(user?.branchName);
  const [query, setQuery] = useState('');
  const [governorateFilter, setGovernorateFilter] = useState(defaultGovernorate);
  const [items, setItems] = useState<PublicEntityEntryDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [proposeEditEntry, setProposeEditEntry] = useState<PublicEntityEntryDto | null>(null);
  const [proposeSuccess, setProposeSuccess] = useState('');

  // نموذج الاقتراح (د7/د8) — ترتيب الحقول كما اعتُمد.
  const [showPropose, setShowPropose] = useState(false);
  const [proposeName, setProposeName] = useState('');
  const [proposeType, setProposeType] = useState<PublicEntityType>('ministry');
  const [proposeCitation, setProposeCitation] = useState<CitationFormula>('add-to-job');
  const [proposeGovernorate, setProposeGovernorate] = useState('');
  const [proposeBranch, setProposeBranch] = useState('الفرع الرئيسي');
  const [proposeSaving, setProposeSaving] = useState(false);
  const [proposeError, setProposeError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  useEffect(() => {
    let active = true;
    setLoading(true);
    api
      .get<PublicEntityListResponse>('/entity-registry/search', {
        params: { q: query.trim() || undefined, governorate: governorateFilter || undefined },
      })
      .then((res) => {
        if (!active) return;
        setItems(Array.isArray(res.data?.items) ? res.data.items : []);
        setError('');
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
  }, [query, governorateFilter]);

  // نتيجة مُرتَّبة: الجهة الأساسية بدون فرع («الفرع الرئيسي») ثابتة أعلى النتائج — لاختيارها
  // عند مخاصمة الجهة الأساسية نفسها (د7): قيدٌ عام يغطي البلد كله مع محافظة تُختار للإجراءات.
  const visibleItems = useMemo(() => {
    const list = items ?? [];
    const filtered = governorateFilter ? list.filter((i) => i.governorate === governorateFilter) : list;
    const mainBranch = 'الفرع الرئيسي';
    return [...filtered.filter((i) => i.branchName === mainBranch), ...filtered.filter((i) => i.branchName !== mainBranch)];
  }, [items, governorateFilter]);

  // اقتراحات الفرع مستخلصة من القيود المطابقة نفسها (§5.1).
  const branchSuggestions = useMemo(() => {
    const set = new Set<string>();
    for (const item of visibleItems.slice(0, 12)) {
      if (item.branchName.trim()) set.add(item.branchName.trim());
    }
    return Array.from(set).slice(0, 6);
  }, [visibleItems]);

  const submitProposal = async () => {
    if (!proposeName.trim()) {
      setProposeError('اسم الجهة مطلوب');
      return;
    }
    if (!proposeGovernorate) {
      setProposeError('المحافظة مطلوبة');
      return;
    }

    setProposeSaving(true);
    setProposeError('');
    try {
      const payload: CreatePublicEntityRequest = {
        canonicalName: proposeName.trim(),
        entityType: proposeType,
        governorate: proposeGovernorate,
        branchName: proposeBranch.trim(),
        citationFormula: proposeCitation,
      };
      await api.post('/entity-registry', payload);
      setSuccessMsg(
        'أُضيفت الجهة إلى السجل وسيقوم رئيس قسمك بمراجعتها قبل ظهورها نهائيًا في بوابات المندوبين.',
      );
    } catch (err) {
      setProposeError(getApiErrorMessage(err));
    } finally {
      setProposeSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="اختيار الجهة العامة"
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h3 className="text-lg font-bold text-gray-800">اختيار الجهة العامة</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="overflow-y-auto p-5 grow overscroll-contain">
          {/* حقل البحث الواحد (§5.1) */}
          <label htmlFor="pep-search" className="sr-only">بحث باسم الجهة</label>
          <input
            id="pep-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="ابحث باسم الجهة أو جزء منها…"
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />

          {/* قائمة المحافظات المنسدلة — الافتراضي محافظة فرع المحامي (د7). */}
          <label htmlFor="pep-governorate" className="mt-3 block text-xs font-medium text-gray-600 mb-1">
            محافظة البحث
          </label>
          <select
            id="pep-governorate"
            value={governorateFilter}
            onChange={(e) => setGovernorateFilter(e.target.value)}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            <option value="">كل المحافظات</option>
            {GOVERNORATES.map((gov) => (
              <option key={gov} value={gov}>{gov}</option>
            ))}
            {!GOVERNORATES.includes(governorateFilter) && governorateFilter !== '' && (
              <option value={governorateFilter}>{governorateFilter}</option>
            )}
          </select>

          <ul className="mt-3 divide-y divide-gray-100">
            {loading && <li className="py-6 text-center text-sm text-gray-400">جارِ البحث…</li>}
            {!loading && error && (
              <li role="alert" className="py-4 text-sm text-red-600">{error}</li>
            )}
            {!loading && !error && visibleItems.length === 0 && (
              <li className="py-6 text-center text-sm text-gray-400">
                لا توجد جهات مطابقة في السجل
              </li>
            )}
            {visibleItems.map((entry) => (
              <li key={entry.id} className="flex items-center gap-2 py-1">
                <button
                  type="button"
                  onClick={() => onPick(entry)}
                  className="grow text-right py-3 flex flex-wrap items-start justify-between gap-2 hover:bg-emerald-50/60 rounded-lg px-2 min-h-11"
                >
                  <span className="min-w-0">
                    <span className="block font-medium text-gray-800 break-words">
                      {entry.canonicalName}
                    </span>
                    <span className="block text-xs text-gray-500 mt-0.5">
                      {entityTypeLabel(entry.entityType)} · {formatEntityCoverage(entry)} / {entry.branchName}
                    </span>
                  </span>
                  {isEntryPendingReview(entry) ? (
                    <span className="shrink-0 rounded-full bg-amber-100 text-amber-800 px-2 py-0.5 text-xs whitespace-nowrap">
                      بانتظار المراجعة
                    </span>
                  ) : (
                    <span className="shrink-0 rounded-full bg-emerald-100 text-emerald-800 px-2 py-0.5 text-xs whitespace-nowrap">
                      {publicEntityStatusLabel(entry.status)}
                    </span>
                  )}
                </button>
                {isLawyer && (
                  <button
                    type="button"
                    onClick={() => setProposeEditEntry(entry)}
                    className="shrink-0 border border-amber-200 text-amber-800 hover:bg-amber-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
                    aria-label={`اقتراح تعديل ${entry.canonicalName}`}
                  >
                    اقتراح تعديل
                  </button>
                )}
              </li>
            ))}
          </ul>

          {/* تحويل إلى نموذج الاقتراح (د7/د8) */}
          {!successMsg && (
            <div className="mt-5 pt-4 border-t border-gray-100">
              {showPropose ? (
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void submitProposal();
                  }}
                  className="space-y-4"
                >
                  <p className="text-sm font-medium text-red-800 bg-red-50 border border-red-200 rounded-lg p-3 leading-relaxed">
                    {PROPOSAL_WARNING_TEXT}
                  </p>

                  <div>
                    <label htmlFor="pep-propose-name" className="block text-xs font-bold text-gray-600 mb-1">
                      اسم الجهة
                    </label>
                    <input
                      id="pep-propose-name"
                      value={proposeName}
                      onChange={(e) => setProposeName(e.target.value)}
                      placeholder={PROPOSAL_NAME_PLACEHOLDER}
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>

                  <div>
                    <label htmlFor="pep-propose-citation" className="block text-xs font-bold text-gray-600 mb-1">
                      الصيغة
                    </label>
                    <select
                      id="pep-propose-citation"
                      value={proposeCitation}
                      onChange={(e) => setProposeCitation(e.target.value as CitationFormula)}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    >
                      {CITATION_FORMULA_OPTIONS.map((o) => (
                        <option key={o.value} value={o.value}>{o.label}</option>
                      ))}
                    </select>
                  </div>

                  <div className="grid sm:grid-cols-3 gap-3">
                    <div>
                      <label htmlFor="pep-propose-type" className="block text-xs font-bold text-gray-600 mb-1">نوع الجهة</label>
                      <select
                        id="pep-propose-type"
                        value={proposeType}
                        onChange={(e) => setProposeType(e.target.value as PublicEntityType)}
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      >
                        {ENTITY_TYPE_OPTIONS.map((o) => (
                          <option key={o.value} value={o.value}>{o.label}</option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label htmlFor="pep-propose-gov" className="block text-xs font-bold text-gray-600 mb-1">المحافظة</label>
                      <select
                        id="pep-propose-gov"
                        value={proposeGovernorate}
                        onChange={(e) => setProposeGovernorate(e.target.value)}
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      >
                        <option value="">اختر المحافظة…</option>
                        {GOVERNORATES.map((g) => (
                          <option key={g} value={g}>{g}</option>
                        ))}
                        {!GOVERNORATES.includes(proposeGovernorate) && proposeGovernorate !== '' && (
                          <option value={proposeGovernorate}>{proposeGovernorate}</option>
                        )}
                      </select>
                    </div>
                    <div>
                      <label htmlFor="pep-propose-branch" className="block text-xs font-bold text-gray-600 mb-1">الفرع</label>
                      <input
                        id="pep-propose-branch"
                        value={proposeBranch}
                        onChange={(e) => setProposeBranch(e.target.value)}
                        autoComplete="off"
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      />
                    </div>
                  </div>

                  {proposeError && <p role="alert" className="text-red-600 text-sm">{proposeError}</p>}

                  <div className="flex flex-wrap gap-2">
                    <button
                      type="submit"
                      disabled={proposeSaving}
                      className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                    >
                      {proposeSaving ? 'جارِ الإرسال…' : 'إرسال الاقتراح'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setShowPropose(false)}
                      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                    >
                      رجوع إلى البحث
                    </button>
                  </div>
                </form>
              ) : (
                <button
                  type="button"
                  onClick={() => {
                    setShowPropose(true);
                    if (governorateFilter) setProposeGovernorate(governorateFilter);
                    if (branchSuggestions.length === 1) setProposeBranch(branchSuggestions[0]);
                  }}
                  className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-sm min-h-11"
                >
                  جهة غير موجودة؟ اقترح إضافة…
                </button>
              )}
            </div>
          )}

          {successMsg && (
            <p role="status" className="mt-5 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-lg p-3 text-sm">
              {successMsg}
            </p>
          )}
          {proposeSuccess && (
            <p role="status" className="mt-3 bg-amber-50 border border-amber-100 text-amber-800 rounded-lg p-3 text-sm">
              {proposeSuccess}
            </p>
          )}
        </div>

        <div className="px-5 py-3 border-t border-gray-100 text-xs text-gray-400">
          الاقتراح الجديد يُراجعه رئيس قسمك قبل ظهوره نهائيًا في السجل وبوابات المندوبين.
        </div>
      </div>
      {proposeEditEntry && (
        <ProposeEditModal
          entry={proposeEditEntry}
          onClose={() => setProposeEditEntry(null)}
          onCommitted={(msg) => {
            setProposeEditEntry(null);
            setProposeSuccess(msg);
          }}
        />
      )}
    </div>
  );
}
