import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { assetDisplayName } from '../../utils/assetDisplay';
import { delegationAssetLabel, matchDelegationAssets } from '../../utils/delegationAssets';
import CircuitPickerModal from '../circuit/CircuitPickerModal';
import type { AssetDto, BranchDto, DelegationDto, ExecutionCircuitDto, UpsertDelegationRequest } from '../../types';

const DATE_PLACEHOLDER = 'مثال: 1/8/2026';

export default function DelegationFormModal({
  documentId,
  documentTitle,
  assets,
  noAvailableAssets = false,
  initial,
  onClose,
  onSaved,
}: {
  documentId: number;
  documentTitle?: string;
  /** أصول الملف المنيب المتاحة للاختيار (مفلترة مسبقًا: بلا كفالة وبلا محجوب بإنابة سارية). */
  assets: AssetDto[];
  /** للملف أموال لكن لا متاح منها للإنابة — رسالة صادقة بدل «لا توجد أموال مسجلة». */
  noAvailableAssets?: boolean;
  /** إنابة قائمة عند التعديل، وnull عند تسطير إنابة جديدة. */
  initial?: DelegationDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = initial !== undefined && initial !== null;
  const [delegatedCourt, setDelegatedCourt] = useState(initial?.delegatedCourt ?? '');
  const [delegatedCircuitId, setDelegatedCircuitId] = useState<number | null>(
    initial?.delegatedCircuitId ?? null,
  );
  const [circuitPickerOpen, setCircuitPickerOpen] = useState(false);
  const circuitButtonRef = useRef<HTMLButtonElement>(null);
  const [isExternal, setIsExternal] = useState(initial?.isExternal ?? false);
  const [externalBranchId, setExternalBranchId] = useState<number | ''>(
    initial?.externalBranchId ?? '',
  );
  const [delegationDate, setDelegationDate] = useState(initial?.delegationDate ?? '');
  const [delegationText, setDelegationText] = useState(initial?.delegationText ?? '');
  const [depositBookNumber, setDepositBookNumber] = useState(initial?.depositBookNumber ?? '');
  const [depositBookDate, setDepositBookDate] = useState(initial?.depositBookDate ?? '');
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
  // انتقالي: قائمة المنابة من السجل؛ فرع بلا سجل (أو فشل تحميل كبيئة الاختبارات)
  // يُبقي النص الحر حتى يُعبَّأ السجل.
  const [registryCircuits, setRegistryCircuits] = useState<ExecutionCircuitDto[] | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const r = await api.get<ExecutionCircuitDto[]>('/execution-circuits/for-delegation');
        if (!cancelled) setRegistryCircuits(r?.data ?? []);
      } catch {
        if (!cancelled) setRegistryCircuits(null);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

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

  const registryEnforced = !isExternal && (registryCircuits?.length ?? 0) > 0;
  const validate = (): string => {
    if (!isExternal && registryEnforced && delegatedCircuitId == null)
      return 'الدائرة المنابة مطلوبة — اختر من قائمة المحافظة';
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
      const firstInvalid = isExternal
        ? !delegatedCourt.trim()
          ? courtRef
          : externalBranchId === ''
            ? branchRef
            : !delegationDate.trim()
              ? dateRef
              : null
        : registryEnforced && delegatedCircuitId == null
          ? circuitButtonRef
          : !delegatedCourt.trim()
            ? courtRef
            : !delegationDate.trim()
              ? dateRef
              : null;
      if (firstInvalid && 'current' in firstInvalid) firstInvalid.current?.focus();
      return;
    }
    setFormError('');
    setSaving(true);
    const payload: UpsertDelegationRequest = {
      delegatedCourt: isExternal
        ? normalizeArabicDigits(delegatedCourt).trim() || null
        : delegatedCourt.trim() || null,
      delegatedCircuitId: isExternal ? null : delegatedCircuitId,
      isExternal,
      externalBranchId: isExternal && externalBranchId !== '' ? Number(externalBranchId) : null,
      delegationDate: normalizeArabicDigits(delegationDate).trim() || null,
      delegationText: normalizeArabicDigits(delegationText).trim() || null,
      depositBookNumber: normalizeArabicDigits(depositBookNumber).trim() || null,
      depositBookDate: normalizeArabicDigits(depositBookDate).trim() || null,
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
            {isExternal || !registryEnforced ? (
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
            ) : (
              <>
                <input
                  id="delegatedCourt"
                  type="text"
                  value={delegatedCourt}
                  readOnly
                  placeholder="اختر من قائمة المحافظة…"
                  aria-readonly="true"
                  className={`${inputCls} bg-gray-50 cursor-not-allowed`}
                />
                <button
                  ref={circuitButtonRef}
                  type="button"
                  onClick={() => setCircuitPickerOpen(true)}
                  className="mt-1 border border-emerald-200 text-emerald-800 hover:bg-emerald-50 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  {delegatedCircuitId != null ? 'تغيير من القائمة…' : 'اختيار من القائمة…'}
                </button>
                {delegatedCircuitId != null && (
                  <span className="ms-2 inline-block rounded-full bg-emerald-50 border border-emerald-100 text-emerald-700 px-2 py-0.5 text-[11px] whitespace-nowrap">
                    مرتبطة بالسجل ✓
                  </span>
                )}
              </>
            )}
          </div>

          <label className="flex items-center gap-2 min-h-11 cursor-pointer">
            <input
              type="checkbox"
              checked={isExternal}
              onChange={(e) => {
                const external = e.target.checked;
                setIsExternal(external);
                // الخارجية نص حر خارج السجل؛ الداخلية من القائمة حصرًا.
                if (external) setDelegatedCircuitId(null);
                else setDelegatedCourt('');
              }}
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

          <fieldset>
            <legend className="block text-xs font-bold text-gray-600 mb-1">
              الأموال موضوع الإنابة
            </legend>
            {assets.length === 0 && unmatchedAssets.length === 0 ? (
              <p className="text-sm text-gray-400">
                {noAvailableAssets
                  ? 'لا توجد أموال مؤهلة للإنابة — يجب تسجيل تاريخ القاء الحجز على الأموال أولاً، أو أنها محجوبة بإنابة سارية'
                  : 'لا توجد أموال مسجلة على هذا الملف'}
              </p>
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

      {circuitPickerOpen && !isExternal && (
        <CircuitPickerModal
          title="اختيار الدائرة المنابة"
          fetchUrl="/execution-circuits/for-delegation"
          initialSelectedId={delegatedCircuitId}
          onSelect={(c: ExecutionCircuitDto) => {
            setDelegatedCircuitId(c.id);
            setDelegatedCourt(c.name);
            setCircuitPickerOpen(false);
          }}
          onClose={() => setCircuitPickerOpen(false)}
        />
      )}
    </div>
  );
}
