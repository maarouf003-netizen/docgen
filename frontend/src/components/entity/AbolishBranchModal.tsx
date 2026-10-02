import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { GOVERNORATES } from '../../utils/governorate';
import { CITATION_FORMULA_OPTIONS, ENTITY_TYPE_OPTIONS } from '../../utils/entityRegistry';
import type {
  AbolishAndReplaceRequest,
  AbolishAndReplaceResponse,
  AbolishReplacePreviewRequest,
  AbolishReplacePreviewResponse,
  PublicEntityType,
} from '../../types';
import { ActionModal, CloseButton } from './ReviewActionModal';
import { DecreeFields } from './DecreeFields';
import { f, type GroupPick } from './reviewShared';

/* ── نافذة «حلول» (متعدد ← جهة جديدة) ──────────────────────────────── */

export function AbolishBranchModal({
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
      subtitle="الغاء جهات عامة واستبدالها بجهة عامة جديدة"
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
            الجهات العامة الملغاة ({f.format(selected.length)})
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
export function AbolishPreviewBox({ preview }: { preview: AbolishReplacePreviewResponse }) {
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
