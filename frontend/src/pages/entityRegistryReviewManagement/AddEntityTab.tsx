import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { GOVERNORATES } from '../../utils/governorate';
import { CITATION_FORMULA_OPTIONS, ENTITY_TYPE_OPTIONS } from '../../utils/entityRegistry';
import type { PublicEntityType } from '../../types';

/**
 * تبويب «إضافة جهة»: إنشاء قيد جهة أم جديدة (اسم معتمد، نوع، صيغة مناداة،
 * محافظة، فرع، أسماء بديلة، وتغطية اختيارية متعددة المحافظات).
 */
export function AddEntityTab() {
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
