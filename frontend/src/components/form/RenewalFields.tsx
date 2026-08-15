import type { DocumentUpsertRequest } from '../../types';

/** مفاتيح حقول التجديد المشتركة بين صفحة التعديل ومسودة الاستعادة. */
export type RenewalFieldKey =
  | 'renewalFileReceiptNumber'
  | 'renewalFileReceiptDate'
  | 'renewalFileNumber'
  | 'renewalFileType'
  | 'renewalYear'
  | 'renewalDate';

export type RenewalFieldsValue = Pick<DocumentUpsertRequest, RenewalFieldKey>;

interface RenewalFieldsProps {
  /** قيم حقول التجديد الحالية. */
  value: RenewalFieldsValue;
  /** معالج تحديث حقل تجديد واحد (يكتب في النموذج أو حالة الاستعادة). */
  onSet: (key: RenewalFieldKey, value: string) => void;
  /** عرض الحقول عمودًا واحدًا (للمساحات الضيقة كخلايا الجداول) بدل الشبكة. */
  stacked?: boolean;
  /** بادئة فريدة لمعرّفات الحقول (لتجنّب تعارض id عند تكرّر المكوّن في شاشة واحدة). */
  idPrefix?: string;
}

/**
 * حقول تجديد الملف المشطوب عند إعادته إلى المتداول: رقم الملف الجديد إلزامي والبقية اختيارية.
 * تُستعمل في صفحة التعديل (عند مشطوب ← متداول) وفي تأكيد الاستعادة من قائمة المشطوبة.
 */
export function RenewalFields({ value, onSet, stacked, idPrefix = '' }: RenewalFieldsProps) {
  const id = (key: string) => `${idPrefix}${key}`;
  return (
    <div className="rounded-lg bg-white border border-emerald-200 p-4">
      <p className="text-xs font-bold text-emerald-800 mb-3">📄 بيانات تجديد الملف</p>
      <div className={`grid gap-4 items-end ${stacked ? 'grid-cols-1' : 'grid-cols-1 md:grid-cols-3'}`}>
        <div>
          <label htmlFor={id('renewalFileReceiptNumber')} className="block text-xs font-bold text-gray-600 mb-1">
            رقم ورود اخطار التجديد
          </label>
          <input
            id={id('renewalFileReceiptNumber')}
            value={value.renewalFileReceiptNumber ?? ''}
            onChange={(e) => onSet('renewalFileReceiptNumber', e.target.value)}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor={id('renewalFileReceiptDate')} className="block text-xs font-bold text-gray-600 mb-1">
            تاريخ ورود اخطار التجديد
          </label>
          <input
            id={id('renewalFileReceiptDate')}
            value={value.renewalFileReceiptDate ?? ''}
            onChange={(e) => onSet('renewalFileReceiptDate', e.target.value)}
            placeholder="مثال: 1/8/2026"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor={id('renewalFileNumber')} className="block text-xs font-bold text-gray-600 mb-1">
            رقم الملف الجديد *
          </label>
          <input
            id={id('renewalFileNumber')}
            value={value.renewalFileNumber ?? ''}
            onChange={(e) => onSet('renewalFileNumber', e.target.value)}
            required
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor={id('renewalFileType')} className="block text-xs font-bold text-gray-600 mb-1">
            نوع الملف الجديد
          </label>
          <input
            id={id('renewalFileType')}
            value={value.renewalFileType ?? ''}
            onChange={(e) => onSet('renewalFileType', e.target.value)}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor={id('renewalYear')} className="block text-xs font-bold text-gray-600 mb-1">
            سنة الإعادة
          </label>
          <input
            id={id('renewalYear')}
            value={value.renewalYear != null ? String(value.renewalYear) : ''}
            onChange={(e) => onSet('renewalYear', e.target.value)}
            placeholder="مثال: 2026"
            inputMode="numeric"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
        <div>
          <label htmlFor={id('renewalDate')} className="block text-xs font-bold text-gray-600 mb-1">
            تاريخ التجديد
          </label>
          <input
            id={id('renewalDate')}
            value={value.renewalDate ?? ''}
            onChange={(e) => onSet('renewalDate', e.target.value)}
            placeholder="مثال: 1/8/2026"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
        </div>
      </div>
    </div>
  );
}
