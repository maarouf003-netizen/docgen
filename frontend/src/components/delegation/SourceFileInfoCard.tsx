import type { DelegationDto } from '../../types';
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
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-emerald-800 mb-3">معلومات الملف المنيب</h3>

      <div className="space-y-2 min-w-0">
        <p className="text-sm text-gray-700">
          <span className="text-gray-500 text-xs block">الملف المنيب</span>
          {delegation.sourceDocumentLabel || `ملف رقم ${delegation.sourceDocumentId}`}
        </p>
        {sourceNumber && (
          <p className="text-sm text-gray-700">
            <span className="text-gray-500 text-xs block">رقم أساس الملف المنيب</span>
            {sourceNumber}
          </p>
        )}
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
    </div>
  );
}
