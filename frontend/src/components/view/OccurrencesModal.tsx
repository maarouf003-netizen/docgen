import type { DocumentOccurrenceDto } from '../../types';
import { formatDate } from '../../utils/dates';
import { EXEC_STATUS_SETTLED } from '../../utils/documentStatus';
import { occurrenceLine } from './viewFormat';

/**
 * نافذة «وقوعات الملف» (عرض فقط): قسم «الشطوبات» للشطب والتجديد، وقسم «تغييرات الحالة»
 * لإجراءات الحالة (تريث/منفذ بالتسوية/منفذ جبريا/تراجع)، وقسم خاص «التغييرات التي وقعت
 * على الجهة العامة» يجمع وقوعات «تغيير جهة» الآلية بسردها النصي الحر (مع مرجع المرسوم)
 * وتاريخ كل تغيير.
 * الإضافة والتعديل اليدويان من صفحة «تعديل» الملف حصرًا.
 */
export function OccurrencesModal({
  documentTitle,
  occurrences,
  onClose,
}: {
  documentTitle?: string;
  occurrences: DocumentOccurrenceDto[];
  onClose: () => void;
}) {
  const nonEntityOccurrences = occurrences.filter((o) => o.occurrenceType !== 'entity-change');
  const struckRenewalOccurrences = nonEntityOccurrences.filter(
    (o) => o.occurrenceType === 'struck-off' || o.occurrenceType === 'renewal',
  );
  const statusChangeOccurrences = nonEntityOccurrences.filter(
    (o) => !struckRenewalOccurrences.includes(o),
  );
  const entityChangeOccurrences = occurrences.filter((o) => o.occurrenceType === 'entity-change');
  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="وقوعات الملف"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">وقوعات الملف</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 mb-4">
            <p className="text-xs text-gray-500 mb-1">الملف</p>
            <p className="font-medium text-gray-800">{documentTitle || 'ملف'}</p>
          </div>

          {occurrences.length === 0 && (
            <p className="text-gray-400 text-sm">لا توجد وقوعات مسجلة لهذا الملف</p>
          )}

          {struckRenewalOccurrences.length > 0 && (
            <section aria-label="الشطوبات">
              <h4 className="text-sm font-bold text-gray-600 mb-2">الشطوبات</h4>
              <div className="space-y-3">
                {struckRenewalOccurrences.map((occurrence) => (
                  <OccurrenceCard key={occurrence.id} occurrence={occurrence} />
                ))}
              </div>
            </section>
          )}

          {statusChangeOccurrences.length > 0 && (
            <section aria-label="تغييرات الحالة" className="mt-5">
              <h4 className="text-sm font-bold text-gray-600 mb-2">تغييرات الحالة</h4>
              <div className="space-y-3">
                {statusChangeOccurrences.map((occurrence) => (
                  <OccurrenceCard key={occurrence.id} occurrence={occurrence} />
                ))}
              </div>
            </section>
          )}

          {entityChangeOccurrences.length > 0 && (
            <section aria-label="التغييرات التي وقعت على الجهة العامة" className="mt-5">
              <h4 className="text-sm font-bold text-gray-600 mb-2">
                التغييرات التي وقعت على الجهة العامة
              </h4>
              <div className="space-y-3">
                {entityChangeOccurrences.map((occurrence) => (
                  <OccurrenceCard key={occurrence.id} occurrence={occurrence} />
                ))}
              </div>
            </section>
          )}

          <div className="mt-5 flex justify-end">
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إغلاق
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/** بطاقة وقعة واحدة: الشارة والنوع والسرد المختصر وشبكة التفاصيل حسب النوع. */
function OccurrenceCard({ occurrence }: { occurrence: DocumentOccurrenceDto }) {
  return (
    <div className="rounded-lg border border-gray-200 p-4">
      <div className="flex items-center justify-between gap-2 flex-wrap mb-2">
        <span className="inline-flex items-center gap-2 flex-wrap">
          <span
            className={`rounded-full px-3 py-1 text-xs font-medium ${
              occurrence.occurrenceType === 'renewal'
                ? 'bg-emerald-100 text-emerald-800'
                : occurrence.occurrenceType === 'struck-off'
                  ? 'bg-red-100 text-red-800'
                  : 'bg-blue-100 text-blue-800'
            }`}
          >
            {occurrence.occurrenceTypeLabel}
          </span>
          {occurrence.source === 'system' && (
            <span className="rounded-full px-3 py-1 text-xs font-medium bg-gray-100 text-gray-700">
              نظامي
            </span>
          )}
        </span>
        {occurrence.createdByName && (
          <span className="text-xs text-gray-400">أدخلها: {occurrence.createdByName}</span>
        )}
      </div>

      <p className="text-gray-800 font-medium">{occurrenceLine(occurrence)}</p>

      <dl className="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-1.5 text-sm">
        {occurrence.occurrenceType === 'renewal' ? (
          <>
            {occurrence.fileNumber && (
              <Detail label="رقم الملف الجديد" value={occurrence.fileNumber} />
            )}
            {occurrence.fileType && (
              <Detail label="نوع الملف الجديد" value={occurrence.fileType} />
            )}
            {occurrence.year && <Detail label="سنة الإعادة" value={String(occurrence.year)} />}
            {occurrence.receiptNumber && (
              <Detail label="رقم ورود اخطار التجديد" value={occurrence.receiptNumber} />
            )}
            {occurrence.receiptDate && (
              <Detail label="تاريخ ورود اخطار التجديد" value={formatDate(occurrence.receiptDate)} />
            )}
          </>
        ) : occurrence.occurrenceType === 'struck-off' ? (
          <>
            {occurrence.fileNumber && (
              <Detail label="رقم الملف المشطوب" value={occurrence.fileNumber} />
            )}
            {occurrence.fileType && (
              <Detail label="نوع الملف المشطوب" value={occurrence.fileType} />
            )}
            {occurrence.eventDate && (
              <Detail label="تاريخ الشطب" value={formatDate(occurrence.eventDate)} />
            )}
            {occurrence.year && <Detail label="سنة الشطب" value={String(occurrence.year)} />}
          </>
        ) : (
          <StatusChangeOccurrenceDetails occurrence={occurrence} />
        )}
      </dl>
    </div>
  );
}

/** تفاصيل وقعة تغيير الحالة (نظام «طالبة تنفيذ») من حقولها المسجّلة في Details. */
function StatusChangeOccurrenceDetails({ occurrence }: { occurrence: DocumentOccurrenceDto }) {
  const d = occurrence.details ?? {};
  const pairs: Array<[string, string]> = [];
  switch (occurrence.occurrenceType) {
    case 'deferred':
      pushIf(pairs, 'رقم كتاب التريث', d.tarithNumber);
      pushIf(pairs, 'تاريخ كتاب التريث', d.tarithDate);
      pushIf(pairs, 'رقم ورود كتاب التريث', d.tarithRegNumber);
      pushIf(pairs, 'تاريخ ورود كتاب التريث', d.tarithRegDate);
      break;
    case 'settled':
      pushIf(pairs, 'رقم كتاب براءة الذمة', d.baraetNumber);
      pushIf(pairs, 'تاريخ كتاب براءة الذمة', d.baraetDate);
      pushIf(pairs, 'رقم ورود كتاب براءة الذمة', d.baraetRegNumber);
      pushIf(pairs, 'تاريخ ورود كتاب براءة الذمة', d.baraetRegDate);
      pushCollected(pairs, d);
      break;
    case 'forcible':
      pushIf(pairs, 'نوع التنفيذ', d.execSubStatus);
      pushCollected(pairs, d);
      pushIf(pairs, 'الأموال المباعة بالمزاد', d.soldAssetNames);
      pushIf(pairs, 'تحويل بدل المبيع للجهة العامة', d.forcedTransferDate);
      pushIf(pairs, 'رقم إشعار التحويل', d.forcedTransferNoticeNumber);
      break;
    case 'referred-to-start':
      pushIf(pairs, 'رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها', d.noFundsDemandNumber);
      pushIf(pairs, 'تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها', d.noFundsDemandDate);
      pushIf(pairs, 'رقم كتاب الإحالة', d.startReferralNumber);
      pushIf(pairs, 'تاريخ كتاب الإحالة', d.startReferralDate);
      break;
    case 'revert':
      // وقعة العودة من «محال الى البداية» (B4): السرد النصي في سطر البطاقة، وحقول الشطب/
      // التجديد هنا — بلا حقول سير؛ «تراجع» الكلاسيكي يبقى بمفاتيح كتاب السير.
      if (d.revertNarration) {
        pushIf(pairs, 'تاريخ شطب الملف السابق', d.struckOffDate);
        pushIf(pairs, 'رقم الملف الجديد', d.renewalFileNumber);
        pushIf(pairs, 'نوع الملف الجديد', d.renewalFileType);
        pushIf(pairs, 'تاريخ التجديد', d.renewalDate);
        pushIf(pairs, 'سنة الإعادة', d.renewalYear);
      } else {
        pushIf(pairs, 'رقم كتاب الجهة العامة بالسير بالملف', d.sayerNumber);
        pushIf(pairs, 'تاريخ كتاب الجهة العامة بالسير بالملف', d.sayerDate);
        pushIf(pairs, 'رقم ورود كتاب بالسير بالملف', d.sayerRegNumber);
        pushIf(pairs, 'تاريخ ورود كتاب بالسير بالملف', d.sayerRegDate);
      }
      break;
    case 'recovered':
      // وقعة استرداد المناب (F2/L4): سبب الاسترداد + كتاب براءة الذمة (تسوية) أو تحويل البدل
      // (اكتمال جبري) + رقم أساس المنيب — بمفاتيح معاودة من وقعةِ الاسترداد.
      if (d.recoveryReason) pushIf(pairs, 'سبب الاسترداد', d.recoveryReason);
      if (d.recoveryReason === EXEC_STATUS_SETTLED) {
        pushIf(pairs, 'رقم كتاب براءة الذمة', d.baraetNumber);
        pushIf(pairs, 'تاريخ كتاب براءة الذمة', d.baraetDate);
      } else {
        pushIf(pairs, 'تاريخ تحويل البدل', d.forcibleTransferDate);
        pushIf(pairs, 'رقم إشعار التحويل', d.forcibleTransferNoticeNumber);
      }
      pushIf(pairs, 'رقم أساس المنيب', d.sourceFileNumber);
      break;
    case 'entity-change':
      // الوقعة الآلية لتغيير الجهة: سردها النصي الحر في سطر البطاقة (occurrenceLine)،
      // وهنا تاريخ التغيير التشغيلي فقط — بمرآة «تاريخ الشطب» لوقعة الشطب.
      pushIf(pairs, 'تاريخ التغيير', formatDate(occurrence.eventDate));
      break;
  }
  if (pairs.length === 0) return <p className="text-gray-400 text-sm">لا توجد تفاصيل مسجلة</p>;
  return (
    <>
      {pairs.map(([label, value]) => (
        <Detail key={label} label={label} value={value} />
      ))}
    </>
  );
}

function pushIf(pairs: Array<[string, string]>, label: string, value: string | undefined): void {
  const v = value?.trim();
  if (v) pairs.push([label, v]);
}

function pushCollected(pairs: Array<[string, string]>, d: Record<string, string>): void {
  const amounts = [1, 2, 3]
    .map((i) => {
      const amount = d[`collectedAmount${i === 1 ? '' : i}`];
      if (!amount) return null;
      return `${amount} ${d[`collectedCurrency${i === 1 ? '' : i}`] ?? ''}`.trim();
    })
    .filter((v): v is string => Boolean(v));
  if (amounts.length) pairs.push(['المبلغ المحصل', amounts.join(' — ')]);
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-gray-500">{label}</dt>
      <dd className="text-gray-800">{value}</dd>
    </div>
  );
}
