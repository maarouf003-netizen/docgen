import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type {
  RenameGroupPreviewRequest,
  RenameGroupPreviewResponse,
  RenameGroupRequest,
  RenameGroupResponse,
} from '../../types';
import { ActionModal, CloseButton } from './ReviewActionModal';
import { DecreeFields } from './DecreeFields';
import { f, type GroupPick } from './reviewShared';

/* ── نافذة «تعديل تسمية» (جهة واحدة) ───────────────────────────────── */

export function RenameGroupModal({
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
