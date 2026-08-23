import { formatDate, formatDateTime } from '../../utils/dates';
import {
  APPEAL_DIRECTION_APPELLANTS,
  APPEAL_STATUS_DECIDED,
  APPEAL_STATUS_STRUCK_OFF,
  appealDirectionLabel,
  appealOutcomeCls,
  appealOutcomeLabel,
} from '../../utils/appealStatus';
import { FieldCell } from '../view/FieldCell';
import type { AppealDto } from '../../types';

/**
 * جسم تفاصيل الاستئناف المشترك بين نافذة البطاقة وصفحة التفاصيل:
 * الأطراف، القرار المستأنف وكتبه، القيد الاستئنافي، قرار الحسم أو الشطب، والملاحظات.
 */
export default function AppealDetailsBody({ appeal }: { appeal: AppealDto }) {
  const isDecided = appeal.status === APPEAL_STATUS_DECIDED;
  const isStruck = appeal.status === APPEAL_STATUS_STRUCK_OFF;

  return (
    <div className="space-y-4 text-sm">
      <dl className="grid sm:grid-cols-2 gap-x-4 gap-y-2.5">
        <FieldCell label="الاتجاه" value={appealDirectionLabel(appeal.direction)} showEmpty />
        <FieldCell label="نوع الاستئناف" value={appeal.appealTypeLabel} showEmpty />
        <FieldCell label="الملف المستأنف" value={appeal.documentLabel} showEmpty />
        <FieldCell
          label="رقم الملف"
          value={[appeal.fileNumber, appeal.fileType, appeal.fileYear].filter(Boolean).join(' / ')}
          showEmpty
        />
        <FieldCell label="دائرة التنفيذ" value={appeal.court} showEmpty />
        <FieldCell label="المحامي المتابع" value={appeal.assignedLawyerName} showEmpty />
      </dl>

      {/* الأطراف */}
      <section aria-label="أطراف الاستئناف" className="space-y-1.5">
        <h4 className="font-semibold text-gray-700">المستأنف</h4>
        <p className="text-gray-800 bg-gray-50 border border-gray-200 rounded-lg px-3 py-2">
          {(appeal.appellants ?? []).map((p) => p.name).join('، ') || '—'}
        </p>
        <h4 className="font-semibold text-gray-700">المستأنف عليهم</h4>
        <p className="text-gray-800 bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 break-words">
          {(appeal.appellees ?? []).map((p) => p.name).join('، ') || '—'}
        </p>
      </section>

      {/* القرار المستأنف */}
      <section aria-label="القرار المستأنف" className="space-y-2.5">
        <h4 className="font-semibold text-gray-700">القرار المستأنف</h4>
        <FieldCell label="نص القرار" value={appeal.appealedDecisionText} showEmpty />
        {appeal.appealedDecisionSummary && (
          <FieldCell label="ملخص القرار" value={appeal.appealedDecisionSummary} showEmpty />
        )}
        <FieldCell label="تاريخ القرار" value={formatDate(appeal.appealedDecisionDate)} showEmpty />
        {appeal.direction === APPEAL_DIRECTION_APPELLANTS ? (
          <>
            <FieldCell label="رقم كتاب المطالعة وإيداع الملف رئيس القسم" value={appeal.inspectionBookNumber} showEmpty />
            <FieldCell label="تاريخ كتاب المطالعة وإيداع الملف رئيس القسم" value={formatDate(appeal.inspectionBookDate)} showEmpty />
            {appeal.groundsSummary && (
              <FieldCell label="ملخص كتاب المطالعة المتضمن موجبات الاستئناف" value={appeal.groundsSummary} showEmpty />
            )}
          </>
        ) : (
          <>
            <FieldCell label="رقم ورود سند تبليغ الاستئناف" value={appeal.noticeNumber} showEmpty />
            <FieldCell label="تاريخ ورود سند تبليغ الاستئناف" value={formatDate(appeal.noticeDate)} showEmpty />
            {appeal.defenseOpinion && (
              <FieldCell label="رأي المحامي المتابع بأسباب الاستئناف" value={appeal.defenseOpinion} showEmpty />
            )}
          </>
        )}
      </section>

      {/* القيد أمام محكمة الاستئناف */}
      <section aria-label="قيد الاستئناف" className="space-y-2.5">
        <h4 className="font-semibold text-gray-700">القيد الاستئنافي</h4>
        <dl className="grid sm:grid-cols-2 gap-x-4 gap-y-2.5">
          <FieldCell label="محكمة الاستئناف التنفيذية المختصة" value={appeal.appellateCourt} showEmpty />
          <FieldCell label="رقم الأساس الاستئنافي" value={appeal.currentBaseNumber ?? appeal.appealBaseNumber} showEmpty />
          <FieldCell label="لعام" value={appeal.appealYear} showEmpty />
          <FieldCell label="تاريخ إقرار الاستئناف" value={formatDate(appeal.registrationDate)} showEmpty />
          <FieldCell label="رقم كتاب إيداع الملف رئيس القسم" value={appeal.depositBookNumber} showEmpty />
          <FieldCell label="تاريخ كتاب إيداع الملف رئيس القسم" value={formatDate(appeal.depositBookDate)} showEmpty />
        </dl>
      </section>

      {/* الحسم أو الشطب */}
      {isDecided && (
        <section aria-label="قرار الحسم" className="space-y-2.5 border-t border-gray-200 pt-3">
          <h4 className="font-semibold text-gray-700">قرار الحسم</h4>
          <dl className="grid sm:grid-cols-2 gap-x-4 gap-y-2.5">
            <FieldCell label="رقم قرار الحسم" value={appeal.decisionNumber} showEmpty />
            <FieldCell label="تاريخ قرار الحسم" value={formatDate(appeal.decisionDate)} showEmpty />
          </dl>
          <FieldCell label="منطوق القرار" value={appeal.decisionRuling} showEmpty />
          <p className="text-sm">
            <span className="text-gray-500">نتيجة الاستئناف: </span>
            <span className={appealOutcomeCls(appeal.outcome)}>
              {appealOutcomeLabel(appeal.outcome)}
            </span>
          </p>
        </section>
      )}

      {isStruck && (
        <section aria-label="شطب الاستئناف" className="space-y-2.5 border-t border-gray-200 pt-3">
          <h4 className="font-semibold text-gray-700">شطب الاستئناف</h4>
          <dl className="grid sm:grid-cols-2 gap-x-4 gap-y-2.5">
            <FieldCell label="رقم قرار الشطب" value={appeal.struckOffDecisionNumber} showEmpty />
            <FieldCell label="تاريخ الشطب" value={formatDate(appeal.struckOffDate)} showEmpty />
          </dl>
        </section>
      )}

      {appeal.notes && <FieldCell label="ملاحظات" value={appeal.notes} showEmpty />}

      <p className="text-xs text-gray-400 pt-1">
        {appeal.createdByName
          ? `سطّره: ${appeal.createdByName}${
              appeal.createdAt ? ` — ${formatDateTime(appeal.createdAt)}` : ''
            }`
          : ''}
      </p>
    </div>
  );
}
