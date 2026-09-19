import type { DelegationDto } from '../../types';
import { formatDate } from '../../utils/dates';
import { delegationAssetsLine } from '../../utils/delegationAssets';
import { FieldCell } from '../view/FieldCell';
import { SectionCard } from '../view/SectionCard';

/**
 * بطاقة «معلومات الملف المنيب» (في الملف المناب): ثمانية حقول (D2/L7) — المنيب ورقمه الأساس
 * ونوعه، ودائرة التنفيذ المنيبة (دائرة المنيب لا فرعه) وداخليتها/خارجيتها، وتاريخ الإنابة
 * ونصها وأموالها. القيمة كما هي مخزنة بلا بادئات.
 * «مسجلة أصولًا» وحالة الإنابة تُعرض كشارة في بطاقة «حالة الإنابة» الجديدة (و3) لا في هذه
 * البطاقة، وزر «إتمام الإنابة» انتقل إلى بطاقة الحالة (و4). يبقى هنا زر «تسجيل أصولًا» وحده.
 */
export function SourceFileInfoCard({
  delegation,
  canRegister,
  onRegister,
}: {
  delegation: DelegationDto;
  /** هل يعرض زر «تسجيل أصولًا»؟ (إنابة محالة لمحامي الملف المناب نفسه). */
  canRegister?: boolean;
  onRegister?: () => void;
}) {
  const sourceNumber = [delegation.sourceFileNumber, delegation.sourceFileYear]
    .filter(Boolean)
    .join('/');
  const assetsLine = delegationAssetsLine(delegation);

  return (
    <SectionCard title="معلومات الملف المنيب">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 items-start">
        <FieldCell
          label="الملف المنيب"
          value={delegation.sourceDocumentLabel || `ملف رقم ${delegation.sourceDocumentId}`}
        />
        {sourceNumber && <FieldCell label="رقم أساس الملف المنيب" value={sourceNumber} />}
        {delegation.sourceFileType && <FieldCell label="نوع الملف المنيب" value={delegation.sourceFileType} />}
        {delegation.sourceCourt && (
          <FieldCell label="الدائرة المنيبة" value={delegation.sourceCourt} />
        )}
        <FieldCell
          label="داخلية أم خارجية"
          value={
            delegation.isExternal
              ? `إنابة خارجية — الفرع المناب: ${delegation.externalBranchName ?? '—'}`
              : 'إنابة داخلية'
          }
        />
        {delegation.delegationDate && (
          <FieldCell label="تاريخ الإنابة" value={formatDate(delegation.delegationDate)} />
        )}
      </div>

      {delegation.delegationText && (
        <div className="mt-2.5">
          <span className="block text-xs text-gray-500 mb-1">نص قرار الإنابة</span>
          <p className="text-sm text-gray-700 whitespace-pre-line break-words">{delegation.delegationText}</p>
        </div>
      )}

      {assetsLine && (
        <div className="mt-2.5">
          <span className="block text-xs text-gray-500 mb-1">الأموال موضوع الإنابة</span>
          <p className="text-sm text-gray-700 break-words">{assetsLine}</p>
        </div>
      )}

      {canRegister && (
        <div className="mt-4 pt-3 border-t border-gray-100">
          <button
            type="button"
            onClick={onRegister}
            className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            تسجيل أصولًا
          </button>
        </div>
      )}
    </SectionCard>
  );
}