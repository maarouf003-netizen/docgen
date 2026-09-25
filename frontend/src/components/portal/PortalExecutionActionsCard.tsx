import { sanitizeRichText } from '../../utils/richText';
import { SectionCard } from '../view/SectionCard';
import type { PortalExecutionActionDto } from '../../types';

/**
 * بطاقة «الإجراءات التنفيذية» (ق7): قرائية حصرًا في بوابة مندوب الجهة — تُعرض إجراءات
 * الملف من نوع action فقط (الملاحظات note محجوبة خلفيًا)، كل إجراء بـنصه الغني المعقّم
 * (عبر sanitizeRichText نفسها) وتاريخه الحر (ActionDate نصًا كما هو) والمحامي الذي أدخله
 * (CreatedByName)، مرتبة الأحدث أولًا، وبلا أي شارة تذكير أو أزرار.
 */
export function PortalExecutionActionsCard({
  actions,
  loading = false,
}: {
  actions: PortalExecutionActionDto[];
  loading?: boolean;
}) {
  return (
    <SectionCard title="الإجراءات التنفيذية">
      {loading && actions.length === 0 ? (
        <p className="text-gray-500 text-sm motion-safe:animate-pulse">جارِ تحميل الإجراءات…</p>
      ) : actions.length === 0 ? (
        <p className="text-gray-400 text-sm">لا توجد إجراءات تنفيذية</p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {actions.map((a) => (
            <li key={a.id} className="py-3 first:pt-0 last:pb-0">
              <div
                className="text-gray-800 text-sm [&_ul]:list-disc [&_ul]:pr-5 [&_ol]:list-decimal [&_ol]:pr-5 break-words"
                dangerouslySetInnerHTML={{ __html: sanitizeRichText(a.text) }}
              />
              <p className="text-gray-500 text-xs mt-1.5 tabular-nums">
                {a.actionDate || '—'}
                {a.createdByName ? <span className="text-gray-400"> · {a.createdByName}</span> : null}
              </p>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}