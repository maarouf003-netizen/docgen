import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { assetDisplayName } from '../../utils/assetDisplay';
import { delegationAssetLabel, matchDelegationAssets } from '../../utils/delegationAssets';
import type { AssetDto, BranchDto, DelegationDto, UpsertDelegationRequest } from '../../types';

const DATE_PLACEHOLDER = 'مثال: 1/8/2026';

export default function DelegationFormModal({
  documentId,
  documentTitle,
  assets,
  initial,
  onClose,
  onSaved,
}: {
  documentId: number;
  documentTitle?: string;
  /** أصول الملف المنيب المتاحة للاختيار. */
  assets: AssetDto[];
  /** إنابة قائمة عند التعديل، وnull عند تسطير إنابة جديدة. */
  initial?: DelegationDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = initial !== undefined && initial !== null;
  const [delegatedCourt, setDelegatedCourt] = useState(initial?.delegatedCourt ?? '');
  const [isExternal, setIsExternal] = useState(initial?.isExternal ?? false);
  const [externalBranchId, setExternalBranchId] = useState<number | ''>(
    initial?.externalBranchId ?? '',
  );
  const [delegationDate, setDelegationDate] = useState(initial?.delegationDate ?? '');
  const [delegationText, setDelegationText] = useState(initial?.delegationText ?? '');
  const [depositBookNumber, setDepositBookNumber] = useState(initial?.depositBookNumber ?? '');
  const [depositBookDate, setDepositBookDate] = useState(initial?.depositBookDate ?? '');
  const [sendBookNumber, setSendBookNumber] = useState(initial?.sendBookNumber ?? '');
  const [sendBookDate, setSendBookDate] = useState(initial?.sendBookDate ?? '');
  const [selectedAssetIds, setSelectedAssetIds] = useState<Set<number>>(() => {
    const initialMatch = matchDelegationAssets(initial?.assets ?? [], assets);
    return new Set(initialMatch.matchedIds);
  });
  const [unmatchedAssets] = useState<DelegationDto['assets']>(() =>
    matchDelegationAssets(initial?.assets ?? [], assets).unmatched,
  );
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const courtRef = useRef<HTMLInputElement>(null);
  const branchRef = useRef<HTMLSelectElement>(null);
  const dateRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .get<BranchDto[]>('/branches')
      .then((r) => {
        if (!cancelled) setBranches(r.data ?? []);
      })
      .catch(() => {
        // فشل تحميل الفروع لا يمنع تسطير إنابة داخلية؛ يظهر الفرع فارغًا عند الاختيار الخارجي.
        if (!cancelled) setBranches([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const toggleAsset = (id: number) => {
    setSelectedAssetIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const validate = (): string => {
    if (!delegatedCourt.trim()) return 'الدائرة المنابة مطلوبة';
    if (isExternal && externalBranchId === '') return 'الإنابة الخارجية تتطلب تحديد الفرع المناب';
    if (!delegationDate.trim()) return 'تاريخ الإنابة مطلوب';
    if (selectedAssetIds.size === 0) return 'يجب اختيار الأموال موضوع الإنابة';
    return '';
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const problem = validate();
    if (problem) {
      setFormError(problem);
      const firstInvalid = isExternal && externalBranchId === ''
        ? branchRef
        : !delegatedCourt.trim()
          ? courtRef
          : !delegationDate.trim()
            ? dateRef
            : null;
      firstInvalid?.current?.focus();
      return;
    }
    setFormError('');
    setSaving(true);
    const payload: UpsertDelegationRequest = {
      delegatedCourt: normalizeArabicDigits(delegatedCourt).trim() || null,
      isExternal,
      externalBranchId: isExternal && externalBranchId !== '' ? Number(externalBranchId) : null,
      delegationDate: normalizeArabicDigits(delegationDate).trim() || null,
      delegationText: normalizeArabicDigits(delegationText).trim() || null,
      depositBookNumber: normalizeArabicDigits(depositBookNumber).trim() || null,
      depositBookDate: normalizeArabicDigits(depositBookDate).trim() || null,
      sendBookNumber: normalizeArabicDigits(sendBookNumber).trim() || null,
      sendBookDate: normalizeArabicDigits(sendBookDate).trim() || null,
      assetIds: [...selectedAssetIds],
    };
    try {
      if (isEdit) {
        await api.put(`/delegations/${initial.id}`, payload);
      } else {
        await api.post(`/documents/${documentId}/delegations`, payload);
      }
      onSaved();
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const inputCls =
    'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={isEdit ? 'تعديل إنابة' : 'تسطير إنابة'}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">{isEdit ? 'تعديل إنابة' : 'تسطير إنابة'}</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} className="px-5 py-4 space-y-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2">
            <p className="text-xs text-gray-500 mb-1">الملف المنيب</p>
            <p className="font-medium text-gray-800">{documentTitle || `ملف رقم ${documentId}`}</p>
          </div>

          {formError && <p className="text-red-600 text-sm" role="alert">{formError}</p>}

          <div>
            <label htmlFor="delegatedCourt" className="block text-xs font-bold text-gray-600 mb-1">
              الدائرة المنابة
            </label>
            <input
              id="delegatedCourt"
              ref={courtRef}
              type="text"
              value={delegatedCourt}
              onChange={(e) => setDelegatedCourt(e.target.value)}
              placeholder="مثال: محكمة التنفيذ الأولى بدمشق…"
              className={inputCls}
              autoComplete="off"
            />
          </div>

          <label className="flex items-center gap-2 min-h-11 cursor-pointer">
            <input
              type="checkbox"
              checked={isExternal}
              onChange={(e) => setIsExternal(e.target.checked)}
              className="h-5 w-5 rounded border-gray-300 text-emerald-700"
            />
            <span className="text-sm text-gray-800">إنابة إلى فرع في محافظة أخرى</span>
          </label>

          {isExternal && (
            <div>
              <label htmlFor="externalBranchId" className="block text-xs font-bold text-gray-600 mb-1">
                الفرع المناب
              </label>
              <select
                id="externalBranchId"
                ref={branchRef}
                value={externalBranchId}
                onChange={(e) => setExternalBranchId(e.target.value === '' ? '' : Number(e.target.value))}
                className={inputCls}
              >
                <option value="">اختر الفرع…</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
              {branches.length === 0 && (
                <p className="text-xs text-gray-500 mt-1">تعذّر تحميل قائمة الفروع — حاول لاحقًا</p>
              )}
            </div>
          )}

          <div>
            <label htmlFor="delegationDate" className="block text-xs font-bold text-gray-600 mb-1">
              تاريخ الإنابة
            </label>
            <input
              id="delegationDate"
              ref={dateRef}
              type="text"
              value={delegationDate}
              onChange={(e) => setDelegationDate(e.target.value)}
              placeholder={DATE_PLACEHOLDER}
              className={inputCls}
              autoComplete="off"
            />
          </div>

          <div>
            <label htmlFor="delegationText" className="block text-xs font-bold text-gray-600 mb-1">
              نص الإنابة
            </label>
            <textarea
              id="delegationText"
              value={delegationText}
              onChange={(e) => setDelegationText(e.target.value)}
              rows={3}
              placeholder="يذكر فيها سبب الإنابة والأموال المطلوب بيعها…"
              className={`${inputCls} resize-y`}
            />
          </div>

          <div className="grid sm:grid-cols-2 gap-3">
            <div>
              <label htmlFor="depositBookNumber" className="block text-xs font-bold text-gray-600 mb-1">
                رقم كتاب إيداع رئيس القسم
              </label>
              <input
                id="depositBookNumber"
                type="text"
                value={depositBookNumber}
                onChange={(e) => setDepositBookNumber(e.target.value)}
                placeholder="اختياري…"
                className={inputCls}
                autoComplete="off"
              />
            </div>
            <div>
              <label htmlFor="depositBookDate" className="block text-xs font-bold text-gray-600 mb-1">
                تاريخ كتاب الإيداع
              </label>
              <input
                id="depositBookDate"
                type="text"
                value={depositBookDate}
                onChange={(e) => setDepositBookDate(e.target.value)}
                placeholder={DATE_PLACEHOLDER}
                className={inputCls}
                autoComplete="off"
              />
            </div>
          </div>

          <div className="grid sm:grid-cols-2 gap-3">
            <div>
              <label htmlFor="sendBookNumber" className="block text-xs font-bold text-gray-600 mb-1">
                رقم كتاب إرسال الإنابة
              </label>
              <input
                id="sendBookNumber"
                type="text"
                value={sendBookNumber}
                onChange={(e) => setSendBookNumber(e.target.value)}
                placeholder="اختياري…"
                className={inputCls}
                autoComplete="off"
              />
            </div>
            <div>
              <label htmlFor="sendBookDate" className="block text-xs font-bold text-gray-600 mb-1">
                تاريخ كتاب الإرسال
              </label>
              <input
                id="sendBookDate"
                type="text"
                value={sendBookDate}
                onChange={(e) => setSendBookDate(e.target.value)}
                placeholder={DATE_PLACEHOLDER}
                className={inputCls}
                autoComplete="off"
              />
            </div>
          </div>

          <fieldset>
            <legend className="block text-xs font-bold text-gray-600 mb-1">
              الأموال موضوع الإنابة
            </legend>
            {assets.length === 0 && unmatchedAssets.length === 0 ? (
              <p className="text-sm text-gray-400">لا توجد أموال مسجلة على هذا الملف</p>
            ) : (
              <div className="space-y-2">
                {assets.length > 0 && (
                  <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 max-h-48 overflow-y-auto overscroll-contain">
                    {assets.map((asset) => {
                      const id = asset.id ?? 0;
                      const label = id ? assetDisplayName(asset) : asset.assetKind;
                      return (
                        <li key={id || label}>
                          <label className="flex items-center gap-2 px-3 py-2 min-h-11 cursor-pointer hover:bg-gray-50">
                            <input
                              type="checkbox"
                              checked={selectedAssetIds.has(id)}
                              onChange={() => toggleAsset(id)}
                              className="h-5 w-5 rounded border-gray-300 text-emerald-700"
                            />
                            <span className="text-sm text-gray-800">{label}</span>
                          </label>
                        </li>
                      );
                    })}
                  </ul>
                )}
                {unmatchedAssets.length > 0 && (
                  <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2">
                    <p className="text-xs text-amber-700 mb-1">
                      أموال كانت محددة سابقًا ولم تعد متاحة (عدِّلت أو حُذفت من الملف):
                    </p>
                    <ul className="space-y-1">
                      {unmatchedAssets.map((u) => (
                        <li key={u.id} className="text-sm text-gray-600">
                          {delegationAssetLabel(u)}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </fieldset>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-5 py-2 text-sm min-h-11 disabled:opacity-50"
            >
              {saving ? 'جارِ الحفظ…' : isEdit ? 'حفظ التعديلات' : 'تسطير الإنابة'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
