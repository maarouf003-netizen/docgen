import type { CorrespondenceImportance } from '../../types';
import { CORRESPONDENCE_IMPORTANCE_LABELS } from './correspondenceDisplay';

/**
 * شارة أهمية المراسلة: العاجل أحمر نابض، والهام كهرماني، والعادي رمادي.
 */
export default function CorrespondenceImportanceBadge({
  importance,
}: {
  importance: CorrespondenceImportance;
}) {
  if (importance === 'urgent') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-red-600 text-white px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
        <span className="w-1.5 h-1.5 rounded-full bg-white motion-safe:animate-pulse" aria-hidden="true" />
        {CORRESPONDENCE_IMPORTANCE_LABELS.urgent}
      </span>
    );
  }
  if (importance === 'important') {
    return (
      <span className="inline-flex items-center rounded-full bg-amber-100 text-amber-800 px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
        {CORRESPONDENCE_IMPORTANCE_LABELS.important}
      </span>
    );
  }
  return (
    <span className="inline-flex items-center rounded-full bg-gray-100 text-gray-600 px-2.5 py-0.5 text-[11px] font-bold whitespace-nowrap">
      {CORRESPONDENCE_IMPORTANCE_LABELS.normal}
    </span>
  );
}
