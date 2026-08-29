import { useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type { CompleteDelegationRequest, DelegationDto, DelegationSaleDto } from '../../types';
import { DelegationDetails } from './DelegationDetails';

const DATE_PLACEHOLDER = 'مثال: 1/8/2026';

/** تحويل نص بدل المبيع (بأرقام عربية أو لاتينية) إلى قيمة عددية صالحة، أو null إن لم تُدخل. */
function parsePrice(value: string): number | null {
  const normalized = normalizeArabicDigits(value).replace(/,/g, '').trim();
  if (!normalized) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

/**
 * نافذة «إتمام الإنابة» لمحامي الملف المناب: بيع الأموال موضوع الإنابة بالمزاد العلني
 * (بدل المبيع لكل أصل بالليرة السورية) وتاريخ إعادة الملف إلى الدائرة المنيبة،
 * وتاريخ «قرار الإحالة القطعية» (إلزامي — يُحفظ على الملف المنيب عند تفعيله «منفذ جبريا»).
 * يُصبح الملف المناب «منفذ إنابة» بعد الإتمام.
 */
export default function CompleteDelegationModal({
  delegation,
  onClose,
  onCompleted,
}: {
  delegation: DelegationDto;
  onClose: () => void;
  onCompleted: () => void;
}) {
  // حد الثقة الوحيد لبيانات الإنابة: تُضمن المصفوفة مرة واحدة وتُقرأ بعد ذلك بأمان.
  const assets = delegation.assets ?? [];
  const [returnDate, setReturnDate] = useState('');
  const [forcedExecutionDate, setForcedExecutionDate] = useState('');
  const [saleCoversFullDebt, setSaleCoversFullDebt] = useState('');
  const [prices, setPrices] = useState<Record<number, string>>(() => {
    const initial: Record<number, string> = {};
    for (const asset of assets) {
      if (asset.salePrice != null && asset.salePrice > 0) initial[asset.id] = String(asset.salePrice);
    }
    return initial;
  });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');
  const dateRef = useRef<HTMLInputElement>(null);

  const setPrice = (assetId: number, value: string) => {
    setPrices((prev) => ({ ...prev, [assetId]: value }));
  };

  const validate = (): string => {
    if (!returnDate.trim()) return 'تاريخ إعادة الملف للدائرة المنيبة مطلوب';
    if (!forcedExecutionDate.trim()) return 'تاريخ قرار الإحالة القطعية مطلوب';
    if (saleCoversFullDebt !== 'true' && saleCoversFullDebt !== 'false')
      return 'يجب تحديد ما إذا كان بدل المبيع غطى كامل المديونية';
    for (const asset of assets) {
      const price = parsePrice(prices[asset.id] ?? '');
      if (price === null) return `بدل المبيع مطلوب للأصل (${asset.assetLabel})`;
      if (price <= 0) return `بدل المبيع غير صالح للأصل (${asset.assetLabel})`;
    }
    return '';
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const problem = validate();
    if (problem) {
      setFormError(problem);
      if (!returnDate.trim()) dateRef.current?.focus();
      return;
    }
    setFormError('');
    setSaving(true);
    const sales: DelegationSaleDto[] = assets.map((asset) => ({
      delegationAssetId: asset.id,
      salePrice: parsePrice(prices[asset.id] ?? '') ?? 0,
    }));
    const payload: CompleteDelegationRequest = {
      returnDate: normalizeArabicDigits(returnDate).trim(),
      sales,
      forcedExecutionDate: normalizeArabicDigits(forcedExecutionDate).trim(),
      saleCoversFullDebt: saleCoversFullDebt === 'true',
    };
    try {
      await api.post(`/delegations/${delegation.id}/complete`, payload);
      onCompleted();
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
      aria-label="إتمام الإنابة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">إتمام الإنابة (بيع الأموال وإعادة الملف)</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} className="px-5 py-4 space-y-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 space-y-3">
            <p className="text-xs text-gray-500">
              الملف المنيب:{' '}
              <span className="font-medium text-gray-800">
                {delegation.sourceDocumentLabel || `ملف رقم ${delegation.sourceDocumentId}`}
              </span>
            </p>
            <DelegationDetails d={{ ...delegation, assets }} />
          </div>

          {formError && <p className="text-red-600 text-sm" role="alert">{formError}</p>}

          <div>
            <label htmlFor="returnDate" className="block text-xs font-bold text-gray-600 mb-1">
              تاريخ إعادة الملف للدائرة المنيبة
            </label>
            <input
              id="returnDate"
              ref={dateRef}
              type="text"
              value={returnDate}
              onChange={(e) => setReturnDate(e.target.value)}
              placeholder={DATE_PLACEHOLDER}
              className={inputCls}
              autoComplete="off"
            />
          </div>

          <div>
            <label htmlFor="forcedExecutionDate" className="block text-xs font-bold text-gray-600 mb-1">
              تاريخ قرار الإحالة القطعية
            </label>
            <input
              id="forcedExecutionDate"
              type="text"
              value={forcedExecutionDate}
              onChange={(e) => setForcedExecutionDate(e.target.value)}
              placeholder={DATE_PLACEHOLDER}
              className={inputCls}
              autoComplete="off"
            />
            <p className="mt-1 text-xs text-gray-400">
              يُحفظ على الملف المنيب عند تفعيله «منفذ جبريا» مع رقم الإشعار لاحقًا.
            </p>
          </div>

          <div>
            <label htmlFor="saleCoversFullDebt" className="block text-xs font-bold text-gray-600 mb-1">
              هل غطى بدل المبيع كامل المديونية؟
            </label>
            <select
              id="saleCoversFullDebt"
              value={saleCoversFullDebt}
              onChange={(e) => setSaleCoversFullDebt(e.target.value)}
              className={inputCls}
              aria-label="هل غطى بدل المبيع كامل المديونية"
            >
              <option value="">اختر…</option>
              <option value="true">غطى كامل المديونية</option>
              <option value="false">لم يغطِ كامل المديونية</option>
            </select>
            <p className="mt-1 text-xs text-gray-400">
              يحدد محامي المناب — يُظهر للمنيب في التنبيه لتسريع تغيير الحالة.
            </p>
          </div>

          <fieldset>
            <legend className="block text-xs font-bold text-gray-600 mb-1">
              بدل المبيع بالليرة السورية
            </legend>
            {assets.length === 0 ? (
              <p className="text-sm text-gray-400">لا توجد أموال مسجلة على هذه الإنابة</p>
            ) : (
              <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
                {assets.map((asset) => (
                  <li key={asset.id} className="px-3 py-3 sm:flex sm:items-center sm:justify-between gap-3">
                    <p className="text-sm text-gray-800 min-w-0 break-words sm:flex-1">
                      {asset.assetLabel}
                      {asset.snapshotAdjusted && (
                        <span className="block text-xs text-amber-700 mt-0.5">
                          عُدِّلت بياناته بعد التسطير — حُدِّثت اللقطة تلقائيًا
                        </span>
                      )}
                    </p>
                    <div className="mt-2 sm:mt-0 sm:w-48">
                      <label
                        htmlFor={`salePrice-${asset.id}`}
                        className="block text-xs text-gray-500 mb-1 sm:hidden"
                      >
                        بدل المبيع بالليرة السورية
                      </label>
                      <input
                        id={`salePrice-${asset.id}`}
                        type="text"
                        inputMode="decimal"
                        value={prices[asset.id] ?? ''}
                        onChange={(e) => setPrice(asset.id, e.target.value)}
                        placeholder="مثال: 750000…"
                        className={inputCls}
                        autoComplete="off"
                      />
                    </div>
                  </li>
                ))}
              </ul>
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
              {saving ? 'جارِ الإتمام...' : 'إتمام الإنابة'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
