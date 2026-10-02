import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { ActionModal, CloseButton } from './ReviewActionModal';
import { DecreeFields } from './DecreeFields';
import { f, type GroupPick } from './reviewShared';

/* ── نافذة «دمج» (متعدد ← هدف) ─────────────────────────────────────── */

export function MergeBranchesModal({
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
      subtitle="دمج جهة أو عدة جهات عامة في جهة عامة أخرى"
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
            الجهة العامة التي سيتم الدمج معها (الباقية)
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
            الجهات العامة الملغاة ({f.format(absorbed.length)})
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
            الاسم الجديد للجهة العامة (اختياري)
          </label>
          <input
            id="mg-final"
            value={finalName}
            onChange={(e) => setFinalName(e.target.value)}
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
