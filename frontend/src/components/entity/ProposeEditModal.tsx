import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import type { PublicEntityEntryDto } from '../../types';
import { CITATION_FORMULA_OPTIONS, ENTITY_TYPE_OPTIONS } from '../../utils/entityRegistry';
import { GOVERNORATES } from '../../utils/governorate';

interface ProposeEditModalProps {
  entry: PublicEntityEntryDto;
  onClose: () => void;
  onCommitted: (summary: string) => void;
}

/**
 * اقتراح تعديل فردي من المحامي (يبقى بانتظار المراجعة — لا يزامن النصوص).
 * يُرسل إلى رئيس القسم عبر تنبيه.
 */
export function ProposeEditModal({ entry, onClose, onCommitted }: ProposeEditModalProps) {
  const [canonicalName, setCanonicalName] = useState(entry.canonicalName);
  const [entityType, setEntityType] = useState(entry.entityType);
  const [governorate, setGovernorate] = useState(entry.governorate);
  const [branchName, setBranchName] = useState(entry.branchName);
  const [citationFormula, setCitationFormula] = useState(entry.citationFormula);
  const [coverageLabel, setCoverageLabel] = useState(entry.coverageLabel ?? '');
  const [showCoverage, setShowCoverage] = useState(!!entry.coverageLabel);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const submit = async () => {
    if (!canonicalName.trim()) {
      setError('اسم الجهة مطلوب');
      return;
    }
    if (!governorate.trim()) {
      setError('المحافظة مطلوبة');
      return;
    }
    if (!branchName.trim()) {
      setError('الفرع مطلوب');
      return;
    }
    setSaving(true);
    setError('');
    try {
      await api.post(`/entity-registry/${entry.id}/propose-edit`, {
        canonicalName: canonicalName.trim(),
        entityType,
        governorate: governorate.trim(),
        branchName: branchName.trim(),
        citationFormula,
        coverageLabel: showCoverage && coverageLabel.trim() ? coverageLabel.trim() : null,
      });
      onCommitted(`تم إرسال اقتراح تعديل «${entry.canonicalName}» → «${canonicalName.trim()}» بانتظار مراجعة رئيس القسم`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={`اقتراح تعديل: ${entry.canonicalName}`}
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto overscroll-contain p-5">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-lg font-bold text-gray-800">اقتراح تعديل جهة</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="bg-amber-50 border border-amber-100 rounded-lg p-3 text-xs text-amber-800 leading-relaxed mb-4">
          اقتراحك يبقى بانتظار مراجعة رئيس القسم — لا يزامن النصوص حتى يعتمد. سيُبلّغ رئيس محافظة الجهة.
        </div>

        <div className="grid sm:grid-cols-2 gap-4">
          <div className="sm:col-span-2">
            <label htmlFor="pe-name" className="block text-xs font-medium text-gray-600 mb-1">الاسم المعتمد</label>
            <input
              id="pe-name"
              value={canonicalName}
              onChange={(e) => setCanonicalName(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="pe-type" className="block text-xs font-medium text-gray-600 mb-1">نوع الجهة</label>
            <select
              id="pe-type"
              value={entityType}
              onChange={(e) => setEntityType(e.target.value as typeof entityType)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="pe-citation" className="block text-xs font-medium text-gray-600 mb-1">صيغة ممثلها القانوني</label>
            <select
              id="pe-citation"
              value={citationFormula}
              onChange={(e) => setCitationFormula(e.target.value as typeof citationFormula)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {CITATION_FORMULA_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="pe-gov" className="block text-xs font-medium text-gray-600 mb-1">المحافظة</label>
            <select
              id="pe-gov"
              value={governorate}
              onChange={(e) => setGovernorate(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {!GOVERNORATES.includes(governorate) && governorate !== '' && (
                <option value={governorate}>{governorate}</option>
              )}
              {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="pe-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع</label>
            <input
              id="pe-branch"
              value={branchName}
              onChange={(e) => setBranchName(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <label className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11 sm:col-span-2">
            <input
              type="checkbox"
              checked={showCoverage}
              onChange={(e) => { setShowCoverage(e.target.checked); if (!e.target.checked) setCoverageLabel(''); }}
              className="h-4 w-4"
            />
            تغطية أكثر من محافظة
          </label>
          {showCoverage && (
            <div className="sm:col-span-2">
              <label htmlFor="pe-coverage" className="block text-xs font-medium text-gray-600 mb-1">تسمية التغطية (حد أقصى 150 حرفًا)</label>
              <input
                id="pe-coverage"
                value={coverageLabel}
                onChange={(e) => setCoverageLabel(e.target.value)}
                maxLength={150}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
          )}
        </div>

        {error && <p role="alert" className="text-red-600 text-sm mt-3">{error}</p>}

        <div className="mt-5 flex flex-wrap gap-2 justify-end">
          <button
            onClick={submit}
            disabled={saving}
            className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {saving ? 'جارِ الإرسال…' : 'إرسال الاقتراح'}
          </button>
          <button
            onClick={onClose}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            إلغاء
          </button>
        </div>
      </div>
    </div>
  );
}
