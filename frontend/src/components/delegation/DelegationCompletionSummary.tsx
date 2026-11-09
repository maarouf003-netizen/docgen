import type { DelegationDto } from '../../types';
import { formatDate } from '../../utils/dates';
import { delegationAssetLabel } from '../../utils/delegationAssets';
import { currencyLabel, formatNumber } from '../dashboard/dashboardFormat';

/**
 * ملخص حقول «إتمام الإنابة» كما أُدخلت في النافذة: تاريخ إعادة الملف للدائرة المنيبة،
 * وتاريخ قرار الإحالة القطعية، وتغطية بدل المبيع للمديونية، وبدل المبيع لكل أصل.
 * يظهر في بطاقة «حالة الإنابة» (المناب) وبطاقة «تشعبات الملف» (المنيب) بعد الإتمام فقط.
 */
export function DelegationCompletionSummary({ delegation }: { delegation: DelegationDto }) {
  const soldAssets = delegation.assets.filter(
    (asset) => asset.salePrice != null && asset.salePrice > 0,
  );
  const hasCompletion =
    Boolean(delegation.returnDate || delegation.forcedExecutionDate) ||
    delegation.saleCoversFullDebt != null;

  if (!hasCompletion) return null;

  return (
    <section
      aria-label="بيانات إتمام الإنابة"
      className="rounded-lg bg-emerald-50/70 border border-emerald-200 px-3 py-2.5 space-y-2"
    >
      <p className="text-xs font-bold text-emerald-900">بيانات إتمام الإنابة</p>

      <dl className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
        {delegation.returnDate && (
          <div className="min-w-0">
            <dt className="text-emerald-800 font-medium">تاريخ إعادة الملف للدائرة المنيبة</dt>
            <dd className="text-gray-800 font-semibold mt-0.5">{formatDate(delegation.returnDate)}</dd>
          </div>
        )}
        {delegation.forcedExecutionDate && (
          <div className="min-w-0">
            <dt className="text-emerald-800 font-medium">تاريخ قرار الإحالة القطعية</dt>
            <dd className="text-gray-800 font-semibold mt-0.5 break-words">
              {delegation.forcedExecutionDate}
            </dd>
          </div>
        )}
        {delegation.saleCoversFullDebt != null && (
          <div className="sm:col-span-2">
            <dt className="text-emerald-800 font-medium">تغطية بدل المبيع للمديونية</dt>
            <dd
              className={`inline-flex mt-1 rounded-full px-2.5 py-0.5 font-semibold ${
                delegation.saleCoversFullDebt
                  ? 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                  : 'bg-amber-100 text-amber-800 border border-amber-200'
              }`}
            >
              {delegation.saleCoversFullDebt ? 'غطى كامل المديونية' : 'لم يغطِ كامل المديونية'}
            </dd>
          </div>
        )}
      </dl>

      {soldAssets.length > 0 && (
        <div>
          <p className="text-xs font-bold text-emerald-900 mb-1">بدل المبيع</p>
          <ul className="space-y-1">
            {soldAssets.map((asset) => (
              <li
                key={asset.id}
                className="flex items-start justify-between gap-2 text-xs text-gray-700"
              >
                <span className="min-w-0 break-words">{delegationAssetLabel(asset)}</span>
                <span className="shrink-0 font-semibold tabular-nums whitespace-nowrap">
                  {formatNumber(asset.salePrice!)} {currencyLabel('ليرة سورية')}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
