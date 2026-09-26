import type { PortalScopeEntryDto } from '../../types';
import { publicEntityBranchLabel } from '../../utils/publicEntityBranchLabel';

/** تسمية فرع واحد بصيغة «المحافظة/الفرع» (والجهة الأم تُعرض بلا تكرار فرعي). */
function formatEntryShort(entry: PortalScopeEntryDto): string {
  return publicEntityBranchLabel(entry.governorate, entry.branchName);
}

/**
 * منتقي فرع الجهة لمندوب الهوية: خيار «كل الفروع (الإجمالي)» ثم خيارًا لكل فرع.
 * يُخفى تمامًا لمندوب الفرع الواحد (قرار العرض: إحصائيات الفرع فقط بلا منتقي).
 */
export default function PortalBranchSelect({
  entries,
  value,
  onChange,
  id = 'portal-branch',
  label = 'اختيار الفرع',
}: {
  entries: PortalScopeEntryDto[];
  value: string;
  onChange: (next: string) => void;
  id?: string;
  label?: string;
}) {
  return (
    <div className="min-w-[200px]">
      <label htmlFor={id} className="sr-only">{label}</label>
      <select
        id={id}
        name="entryId"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        autoComplete="off"
        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
      >
        <option value="">كل الفروع (الإجمالي)</option>
        {entries.map((e) => (
          <option key={e.id} value={String(e.id)}>{formatEntryShort(e)}</option>
        ))}
      </select>
    </div>
  );
}
