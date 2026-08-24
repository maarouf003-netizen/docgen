/** شارة حالة كتاب المطالعة: أخضر «تم الرد» أو أحمر «بانتظار رد». */
export default function ReviewStatusBadge({ isAnswered }: { isAnswered: boolean }) {
  return isAnswered ? (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 border border-emerald-200 px-2.5 py-0.5 text-xs font-medium text-emerald-700 whitespace-nowrap">
      <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" aria-hidden="true" />
      تم الرد
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 rounded-full bg-red-50 border border-red-200 px-2.5 py-0.5 text-xs font-medium text-red-700 whitespace-nowrap">
      <span className="w-1.5 h-1.5 rounded-full bg-red-500 animate-pulse" aria-hidden="true" />
      بانتظار رد
    </span>
  );
}
