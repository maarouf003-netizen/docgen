import type { DelegationDto } from '../../types';
import { FieldCell } from '../view/FieldCell';
import { SectionCard } from '../view/SectionCard';
import { DelegationDetails } from './DelegationDetails';

/**
 * بطاقة «معلومات الملف المنيب» (في الملف المناب): تعرض إنابة هذا الملف كما سطّرها
 * محامي الملف المنيب — مصدره وأطرافه وبيانات سنده (لقطة مجمّدة تُحدَّث من المنيب).
 * يجد محامي الملف المناب هنا أزرار متابعة الإنابة: «تسجيل أصولًا» ثم «إتمام الإنابة».
 */
export function SourceFileInfoCard({
  delegation,
  canRegister,
  canComplete,
  onRegister,
  onComplete,
}: {
  delegation: DelegationDto;
  /** هل يعرض زر «تسجيل أصولًا»؟ (إنابة محالة لمحامي الملف المناب نفسه). */
  canRegister?: boolean;
  /** هل يعرض زر «إتمام الإنابة»؟ (إنابة مسجلة أصولًا لمحامي الملف المناب نفسه). */
  canComplete?: boolean;
  onRegister?: () => void;
  onComplete?: () => void;
}) {
  const sourceNumber = [delegation.sourceFileNumber, delegation.sourceFileYear]
    .filter(Boolean)
    .join('/');

  return (
    <SectionCard title="معلومات الملف المنيب">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 items-start">
        <FieldCell
          label="الملف المنيب"
          value={delegation.sourceDocumentLabel || `ملف رقم ${delegation.sourceDocumentId}`}
        />
        {sourceNumber && <FieldCell label="رقم أساس الملف المنيب" value={sourceNumber} />}
      </div>

      <div className="mt-3 pt-3 border-t border-gray-100">
        <DelegationDetails d={delegation} />
      </div>

      {(canRegister || canComplete) && (
        <div className="mt-4 pt-3 border-t border-gray-100 flex gap-2 flex-wrap">
          {canRegister && (
            <button
              type="button"
              onClick={onRegister}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              تسجيل أصولًا
            </button>
          )}
          {canComplete && (
            <button
              type="button"
              onClick={onComplete}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              إتمام الإنابة
            </button>
          )}
        </div>
      )}
    </SectionCard>
  );
}
