import {
  EXECUTED_STATUS_EXECUTED,
  EXEC_STATUS_DEFERRED,
  EXEC_STATUS_REFERRED_TO_START,
  STATE_CIRCULATING,
  STATE_DRAFT,
} from '../../utils/documentStatus';
import type { PortalCurrencyStatDto, PortalStatsDto } from '../../types';

const AR_MONTHS = ['ك2', 'شباط', 'آذار', 'نيسان', 'أيار', 'حزيران', 'تموز', 'آب', 'أيلول', 'ت1', 'ت2', 'كانون الأول'];

function monthLabel(year: number, month: number): string {
  return `${AR_MONTHS[month - 1] ?? month} ${year}`;
}

const amountFmt = new Intl.NumberFormat('ar-SY');

/** عدد القيود المعروضة في نظرة سريعة؛ ما زاد يُعلن بعد الحدّ بدل إخفائه بصمت. */
const VISIBLE_ENTRIES = 6;

function CurrencyTotalsList({ totals }: { totals: PortalCurrencyStatDto[] }) {
  if (totals.length === 0) return <p className="text-xs text-gray-400">لا توجد مبالغ مسجلة</p>;
  return (
    <ul className="divide-y divide-gray-100">
      {totals.map((c) => (
        <li key={c.currency} className="py-1.5 flex items-center justify-between gap-3 text-sm">
          <span className="truncate text-gray-700">{c.currency}</span>
          <span className="shrink-0 tabular-nums text-gray-600">
            {c.files} ملفًا · {amountFmt.format(c.totalAmount)}
          </span>
        </li>
      ))}
    </ul>
  );
}

/**
 * عرض إحصاءات نطاق الجهة: عدّادات الحالة + مجموع المبالغ الإجمالي ولكل نوع
 * (مكسّرًا حسب العملة بلا خلط) + السلسلة الشهرية + توزيع القيود + الاستئنافات.
 * كتلة «أعلى العملات» محذوفة نهائيًا بقرار صاحب المشروع.
 */
export default function PortalStatsView({
  stats,
}: {
  stats: PortalStatsDto;
  scopeType?: string | null;
  /** محجوز لتوافق المتصل (منتقي الفرع/القيد) — غير مستخدم في العرض حاليًا. */
  singleEntry?: boolean;
}) {
  const amountTotals = stats.amountTotals ?? [];
  const amountByStatus = stats.amountByStatus ?? [];
  const monthly = stats.monthly ?? [];
  const perEntry = stats.perEntry ?? [];

  return (
    <section aria-labelledby="portal-stats-title" className="bg-white rounded-xl shadow p-4 sm:p-5 mb-4">
      <h3 id="portal-stats-title" className="font-bold text-gray-800 mb-3">إحصاءات نطاق جهتك</h3>

      <dl className="grid grid-cols-2 sm:grid-cols-6 gap-2 text-center">
        {([
          ['الإجمالي', stats.totalFiles, 'bg-emerald-800 text-white'],
          [STATE_CIRCULATING, stats.circulatingFiles, 'bg-emerald-50 text-emerald-900'],
          [EXECUTED_STATUS_EXECUTED, stats.executedFiles, 'bg-sky-50 text-sky-900'],
          [EXEC_STATUS_DEFERRED, stats.deferredFiles, 'bg-amber-50 text-amber-900'],
          [EXEC_STATUS_REFERRED_TO_START, stats.referredToStartFiles ?? 0, 'bg-purple-50 text-purple-900'],
          [STATE_DRAFT, stats.draftFiles, 'bg-gray-100 text-gray-700'],
        ] as const).map(([label, value, cls]) => (
          <div key={label} className={`rounded-lg px-2 py-3 ${cls}`}>
            <dt className="text-[11px] opacity-80">{label}</dt>
            <dd className="text-xl font-bold tabular-nums">{value}</dd>
          </div>
        ))}
      </dl>

      <p className="mt-3 text-xs text-gray-500 tabular-nums">
        الاستئنافات: {stats.pendingAppeals} معلّقًا · {stats.closedAppeals} مغلقًا
      </p>

      <div className="grid sm:grid-cols-2 gap-5 mt-5">
        <div>
          <h4 className="text-xs font-bold text-gray-600 mb-1">مجموع المبالغ (الإجمالي حسب العملة)</h4>
          <CurrencyTotalsList totals={amountTotals} />
        </div>
        <div>
          <h4 className="text-xs font-bold text-gray-600 mb-1">المبالغ لكل نوع من الملفات</h4>
          {amountByStatus.length === 0 ? (
            <p className="text-xs text-gray-400">لا توجد مبالغ مسجلة</p>
          ) : (
            <ul className="space-y-3">
              {amountByStatus.map((row) => (
                <li key={row.status} className="rounded-lg border border-gray-100 px-3 py-2">
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <span className="text-xs font-bold text-gray-700">{row.status}</span>
                    <span className="text-[11px] text-gray-500 tabular-nums">{row.files} ملفًا</span>
                  </div>
                  <CurrencyTotalsList totals={row.totals} />
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <h4 className="text-xs font-bold text-gray-600 mt-4 mb-1">الملفات الواردة — آخر 12 شهرًا</h4>
      <div className="flex items-end gap-1 h-20" role="img"
           aria-label={`ملفات آخر 12 شهرًا، الإجمالي ${monthly.reduce((s, m) => s + m.files, 0)}`}>
        {(() => {
          const max = Math.max(1, ...monthly.map((m) => m.files));
          return monthly.map((m) => (
            <div key={`${m.year}-${m.month}`} className="flex-1 flex flex-col items-center justify-end gap-0.5 min-w-0"
                 title={`${monthLabel(m.year, m.month)}: ${m.files}`}>
              <span className="text-[9px] text-gray-400 tabular-nums">{m.files || ''}</span>
              <div
                className="w-full bg-emerald-600/80 rounded-t"
                style={{ height: `${Math.max(2, Math.round((m.files / max) * 56))}px` }}
                aria-hidden="true"
              />
              <span className="text-[8px] text-gray-400 truncate w-full text-center">{AR_MONTHS[m.month - 1] ?? m.month}</span>
            </div>
          ));
        })()}
      </div>

      <div className="mt-5">
        <h4 id="portal-perentry-title" className="text-xs font-bold text-gray-600 mb-1">توزيع الارتباط على القيود</h4>
        {(() => {
          // أعلى ستة قيود للنظرة السريعة، والباقي مُعلنٌ بعد الحدّ لا مقطوعًا بصمت.
          const visible = perEntry.slice(0, VISIBLE_ENTRIES);
          const hiddenCount = perEntry.length - visible.length;
          // المدى على المرئي وحده: نفس النتيجة بلا اعتماد على ترتيب المصفوفة الكاملة.
          const max = Math.max(1, ...visible.map((e) => e.files));
          return (
            <>
              <ul className="space-y-1.5" aria-labelledby="portal-perentry-title">
                {visible.map((e) => (
                  <li key={e.entryId} className="text-xs">
                    <div className="flex justify-between gap-2 mb-0.5">
                      <span className="truncate text-gray-700">{e.governorate}/{e.branchName}</span>
                      <span className="tabular-nums text-gray-500 shrink-0">{e.files}</span>
                    </div>
                    <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
                      <div className="h-full bg-emerald-600/70" style={{ width: `${(e.files / max) * 100}%` }} aria-hidden="true" />
                    </div>
                  </li>
                ))}
              </ul>
              {hiddenCount > 0 && (
                <p className="mt-1.5 text-[11px] text-gray-400 tabular-nums">
                  +{hiddenCount} قيدًا آخر غير معروض… (اطّلع من صفحة الملفات)
                </p>
              )}
            </>
          );
        })()}
      </div>

    </section>
  );
}
