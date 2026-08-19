import type { CurrencyAmountDto, ManagerContractSplitDto } from '../../types';
import { currencyLabel, formatNumber } from './dashboardFormat';

/** مبالغ مجمّعة بعملاتها الفعلية (كل مبلغ بتسمية عملته لا بوسم ثابت). */
export function CurrencyAmountList({ amounts }: { amounts: CurrencyAmountDto[] }) {
  if (!amounts || amounts.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs font-semibold text-gray-700 mt-1.5">
      {amounts.map((a) => (
        <span key={a.currency} className="whitespace-nowrap tabular-nums" dir="ltr">
          {formatNumber(Number(a.amount))} {currencyLabel(a.currency)}
        </span>
      ))}
    </div>
  );
}

/** تفصيلة حالة مفصولة مصرفي/عادي: عدد كل نوع ومبالغه بعملاته. */
export function ContractSplit({ split }: { split: ManagerContractSplitDto }) {
  if (!split) return null;
  return (
    <div className="mt-2 space-y-1.5 text-xs">
      {split.bankingCount > 0 ? (
        <div>
          <span className="font-bold text-gray-800">مصرفي</span>
          <span className="text-gray-500 tabular-nums" dir="ltr"> ({split.bankingCount})</span>
          <CurrencyAmountList amounts={split.bankingAmounts} />
        </div>
      ) : null}
      {split.ordinaryCount > 0 ? (
        <div>
          <span className="font-bold text-gray-800">عادي</span>
          <span className="text-gray-500 tabular-nums" dir="ltr"> ({split.ordinaryCount})</span>
          <CurrencyAmountList amounts={split.ordinaryAmounts} />
        </div>
      ) : null}
    </div>
  );
}
