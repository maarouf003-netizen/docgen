import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { formatDate } from '../../utils/dates';
import { isExecutedLike } from '../../utils/documentDisplay';
import type { DocumentOccurrenceDto, OccurrenceType, UpsertOccurrenceRequest } from '../../types';
import { occurrenceLine } from '../view/viewFormat';
import { FormSectionTitle } from './FormSectionTitle';

/** سنة اليوم المثبتة لاستعادة المشطوب في عائلة «منفذ عليها»/«عرض وايداع» (§4.4). */
const pinnedRenewalYear = () => new Date().getFullYear();

/** تحليل سنة مدخلة بأرقام عربية أو لاتينية (تُرفض العربية الخام من Number مباشرة). */
const parseYearInput = (raw: string): number => Number(normalizeArabicDigits(raw).trim());

/** حقول نموذج إضافة/تعديل وقعة (نصوص يُطبَّع الرقم وتُحلَّل التواريخ عند الإرسال). */
interface OccurrenceFormState {
  occurrenceType: OccurrenceType;
  eventDate: string;
  fileNumber: string;
  fileType: string;
  year: string;
  receiptNumber: string;
  receiptDate: string;
  /** حقول إجراءات تغيير الحالة (نظام «طالبة تنفيذ»): tarith*، baraet*، sayer*، execSubStatus، المبالغ، soldAssetIds. */
  details: Record<string, string>;
}

const emptyForm = (): OccurrenceFormState => ({
  occurrenceType: 'struck-off',
  eventDate: '',
  fileNumber: '',
  fileType: '',
  year: '',
  receiptNumber: '',
  receiptDate: '',
  details: {},
});

const fromDto = (o: DocumentOccurrenceDto): OccurrenceFormState => ({
  occurrenceType: o.occurrenceType,
  eventDate: o.eventDate ?? '',
  fileNumber: o.fileNumber ?? '',
  fileType: o.fileType ?? '',
  year: o.year != null ? String(o.year) : '',
  receiptNumber: o.receiptNumber ?? '',
  receiptDate: o.receiptDate ?? '',
  details: o.details ? { ...o.details } : {},
});

/** حقول كل نوع تغيير حالة (مفاتيح الخدمة + التسمية العربية). */
const STATUS_CHANGE_FIELDS: Record<string, Array<[string, string]>> = {
  deferred: [
    ['tarithNumber', 'رقم كتاب التريث'],
    ['tarithDate', 'تاريخ كتاب التريث'],
    ['tarithRegNumber', 'رقم ورود كتاب التريث'],
    ['tarithRegDate', 'تاريخ ورود كتاب التريث'],
  ],
  settled: [
    ['baraetNumber', 'رقم كتاب براءة الذمة'],
    ['baraetDate', 'تاريخ كتاب براءة الذمة'],
    ['baraetRegNumber', 'رقم ورود كتاب براءة الذمة'],
    ['baraetRegDate', 'تاريخ ورود كتاب براءة الذمة'],
  ],
  revert: [
    ['sayerNumber', 'رقم كتاب الجهة العامة بالسير بالملف'],
    ['sayerDate', 'تاريخ كتاب الجهة العامة بالسير بالملف'],
    ['sayerRegNumber', 'رقم ورود كتاب بالسير بالملف'],
    ['sayerRegDate', 'تاريخ ورود كتاب بالسير بالملف'],
  ],
};

const STATUS_CHANGE_TYPES: OccurrenceType[] = ['deferred', 'settled', 'forcible', 'revert'];

function isStatusChange(type: OccurrenceType): boolean {
  return STATUS_CHANGE_TYPES.includes(type);
}

/**
 * محرر «وقوعات الملف» اليدوي في صفحة تعديل ملف «منفذ عليه»/«عرض وايداع»:
 * إضافة وتعديل وحذف الوقوعات (شطب/تجديد) عبر نقاط نهاية مستقلة تُحفظ فورًا،
 * فتبقى الوقوعات سجلًا مستقلًا عن حقول المستند الرئيسية.
 *
 * للملف المشطوب حاليًا، تسجيل وقعة تجديد يعني إعادته إلى المتداول، ويُوجَّه
 * تلقائيًا إلى إعادة «restore-struck-off» (لا نقطة الوقوعات)، ثم يطلَب من
 * الأب إعادة تحميل المستند ليُعكس أثر الإعادة على الحالة والوقوعات معًا.
 */
export function OccurrencesEditor({
  documentId,
  initial,
  isFileStruckOff,
  generalEntitySide,
  onRenewalRestored,
}: {
  documentId: number;
  initial: DocumentOccurrenceDto[];
  /** هل الملف مشطوب حاليًا (حسب الحالة الأصلية المحفوظة قبل التعديل). */
  isFileStruckOff: boolean;
  /** عائلة الملف («applicant»/«executed»/«deposit») — لسنة الإعادة المثبتة (§4.4). */
  generalEntitySide?: string;
  /** يُستدعى بعد نجاح إعادة ملف مشطوب إلى المتداول لإعادة تحميل المستند. */
  onRenewalRestored?: () => void;
}) {
  const [occurrences, setOccurrences] = useState<DocumentOccurrenceDto[]>(initial);
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<OccurrenceFormState>(emptyForm());
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);

  // يتزامن مع الوقوعات القادمة من تحميل الملف (تُحمَّل بعد التركيب الأول للمكوّن)،
  // ويبقى على أي تعديل داخلي لاحق لأن مرجع initial ثابت ما لم يُعاد تحميل الملف.
  useEffect(() => {
    setOccurrences(initial);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initial]);

  const set = (key: keyof OccurrenceFormState, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  const setDetail = (key: string, value: string) =>
    setForm((f) => ({ ...f, details: { ...f.details, [key]: value } }));

  const openAdd = () => {
    setForm(emptyForm());
    setEditingId(null);
    setError('');
    setSuccess('');
    setFormOpen(true);
  };

  const openEdit = (occurrence: DocumentOccurrenceDto) => {
    setForm(fromDto(occurrence));
    setEditingId(occurrence.id);
    setError('');
    setSuccess('');
    setFormOpen(true);
  };

  const closeForm = () => {
    setFormOpen(false);
    setEditingId(null);
    setError('');
  };

  // عائلة «منفذ عليها»/«عرض وايداع»: سنة الإعادة مخفية ومثبتة على سنة اليوم (§4.4) —
  // تُطبَّق فقط على مسار استعادة المشطوب (تجديد جديد)، لا على تحرير الوقوعات القائمة.
  const pinRenewalYear = isExecutedLike(generalEntitySide);
  const restoringStruckOff = editingId === null && isFileStruckOff && form.occurrenceType === 'renewal';

  const toRequest = (): UpsertOccurrenceRequest => {
    const request: UpsertOccurrenceRequest = {
      occurrenceType: form.occurrenceType,
      eventDate: normalizeArabicDigits(form.eventDate).trim() || undefined,
      fileNumber: form.fileNumber.trim() || undefined,
      fileType: form.fileType.trim() || undefined,
      year: form.year.trim() ? parseYearInput(form.year) : undefined,
      receiptNumber: form.receiptNumber.trim() || undefined,
      receiptDate: normalizeArabicDigits(form.receiptDate).trim() || undefined,
    };
    if (isStatusChange(form.occurrenceType)) {
      const details: Record<string, string> = {};
      for (const [key, value] of Object.entries(form.details)) {
        if (value.trim()) details[key] = normalizeArabicDigits(value).trim();
      }
      if (form.occurrenceType === 'forcible' && form.details.execSubStatus) {
        details.execSubStatus = form.details.execSubStatus;
      }
      request.details = Object.keys(details).length > 0 ? details : undefined;
    }
    return request;
  };

  const validate = (): string => {
    if (form.occurrenceType === 'renewal' && !form.fileNumber.trim()) {
      return 'رقم الملف الجديد مطلوب لوقعة التجديد';
    }
    if (isStatusChange(form.occurrenceType)) {
      if (form.occurrenceType === 'deferred' && (!form.details.tarithNumber?.trim() || !form.details.tarithDate?.trim())) {
        return 'يجب إدخال رقم وتاريخ كتاب التريث على الأقل';
      }
      if (form.occurrenceType === 'settled' && (!form.details.baraetNumber?.trim() || !form.details.baraetDate?.trim())) {
        return 'يجب إدخال رقم وتاريخ كتاب براءة الذمة على الأقل';
      }
      if (form.occurrenceType === 'forcible' && !form.details.execSubStatus?.trim()) {
        return 'نوع التنفيذ الفرعي مطلوب';
      }
      if (form.occurrenceType === 'revert' && (
        !form.details.sayerNumber?.trim() || !form.details.sayerDate?.trim()
        || !form.details.sayerRegNumber?.trim() || !form.details.sayerRegDate?.trim())) {
        return 'يجب إدخال حقول كتاب الجهة العامة بالسير بالملف كاملة';
      }
    }
    if (form.year.trim()) {
      const year = parseYearInput(form.year);
      if (!Number.isInteger(year) || year < 1900 || year > 2100) {
        return 'سنة الوقعة غير صالحة';
      }
    }
    return '';
  };

  const save = async () => {
    const message = validate();
    if (message) {
      setError(message);
      return;
    }
    setBusy(true);
    setError('');
    setSuccess('');
    try {
      // ملف مشطوب حاليًا + وقعة تجديد جديدة = إعادة الملف إلى المتداول: تُرسل عبر
      // نقطة الإعادة المخصصة (التي تحفظ وقعة التجديد وتعيد بناء رقم الأساس)،
      // لا عبر نقطة الوقوعات اليدوية التي ترفضها الخلفية لهذه الحالة.
      if (restoringStruckOff) {
        await api.post(`/documents/${documentId}/restore-struck-off`, {
          renewalFileNumber: form.fileNumber.trim() || undefined,
          renewalFileType: form.fileType.trim() || undefined,
          renewalYear: pinRenewalYear ? pinnedRenewalYear() : (form.year.trim() ? parseYearInput(form.year) : undefined),
          renewalFileReceiptNumber: form.receiptNumber.trim() || undefined,
          renewalFileReceiptDate: normalizeArabicDigits(form.receiptDate).trim() || undefined,
          renewalDate: normalizeArabicDigits(form.eventDate).trim() || undefined,
        });
        onRenewalRestored?.();
        setSuccess('أعيد الملف إلى المتداول بنجاح وسُجِّلت وقعة التجديد.');
        closeForm();
        return;
      }
      const payload = toRequest();
      if (editingId === null) {
        const res = await api.post<DocumentOccurrenceDto>(`/documents/${documentId}/occurrences`, payload);
        setOccurrences((xs) => [...xs, res.data]);
      } else {
        const res = await api.put<DocumentOccurrenceDto>(
          `/documents/${documentId}/occurrences/${editingId}`,
          payload,
        );
        setOccurrences((xs) => xs.map((o) => (o.id === editingId ? res.data : o)));
      }
      closeForm();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const remove = async (occurrence: DocumentOccurrenceDto) => {
    if (!window.confirm('هل أنت متأكد من حذف هذه الوقعة؟')) return;
    setError('');
    try {
      await api.delete(`/documents/${documentId}/occurrences/${occurrence.id}`);
      setOccurrences((xs) => xs.filter((o) => o.id !== occurrence.id));
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  };

  const inputCls =
    'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500';

  return (
    <div>
      <FormSectionTitle title="📂 وقوعات الملف" />

      <div className="rounded-lg bg-gray-50 border border-gray-200 p-4">
        <p className="text-xs text-gray-500 mb-3">
          سجل زمني لكل شطب وتجديد في الملف. الوقوعات تُسجَّل تلقائيًا عند الشطب والتجديد،
          ويمكنك إضافتها أو تعديلها هنا يدويًا.
        </p>

        {error && <p className="text-red-600 text-sm mb-3">{error}</p>}
        {success && <p className="text-emerald-700 text-sm mb-3">{success}</p>}

        {isFileStruckOff && (
          <p className="text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 text-xs mb-3">
            هذا الملف مشطوب حاليًا — تسجيل وقعة «تجديد» هنا يُعيده إلى المتداول دفعة واحدة ويسجّل
            وقعة التجديد تلقائيًا.
          </p>
        )}

        {occurrences.length === 0 && !formOpen && (
          <p className="text-gray-400 text-sm mb-3">لا توجد وقوعات مسجلة لهذا الملف</p>
        )}

        {occurrences.length > 0 && (
          <ul className="space-y-2 mb-4">
            {occurrences.map((occurrence) => (
              <li
                key={occurrence.id}
                className="flex items-start justify-between gap-3 flex-wrap bg-white rounded-lg border border-gray-200 px-3 py-2"
              >
                <div className="min-w-0 flex-1">
                  <span
                    className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium mb-1 ${
                      occurrence.occurrenceType === 'renewal'
                        ? 'bg-emerald-100 text-emerald-800'
                        : occurrence.occurrenceType === 'struck-off'
                          ? 'bg-red-100 text-red-800'
                          : 'bg-blue-100 text-blue-800'
                    }`}
                  >
                    {occurrence.occurrenceTypeLabel}
                  </span>
                  <p className="text-gray-800 text-sm">{occurrenceLine(occurrence)}</p>
                  {occurrence.receiptNumber && (
                    <p className="text-xs text-gray-500">
                      ورود اخطار: {occurrence.receiptNumber}
                      {occurrence.receiptDate ? ` — ${formatDate(occurrence.receiptDate)}` : ''}
                    </p>
                  )}
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => openEdit(occurrence)}
                    className="text-emerald-800 text-sm font-medium hover:underline min-h-11 px-2"
                  >
                    تعديل
                  </button>
                  <button
                    type="button"
                    onClick={() => remove(occurrence)}
                    className="text-red-600 text-sm font-medium hover:underline min-h-11 px-2"
                  >
                    حذف
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {formOpen ? (
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <p className="font-bold text-gray-800 text-sm mb-3">
              {editingId === null ? 'إضافة وقعة' : 'تعديل الوقعة'}
            </p>

            <div className="grid md:grid-cols-2 gap-3">
              <label className="block">
                <span className="text-xs text-gray-500 block mb-1">نوع الوقعة</span>
                <select
                  value={form.occurrenceType}
                  onChange={(e) => set('occurrenceType', e.target.value as OccurrenceType)}
                  className={inputCls}
                >
                  <option value="struck-off">شطب</option>
                  <option value="renewal">تجديد</option>
                  <option value="deferred">تريث</option>
                  <option value="settled">منفذ بالتسوية</option>
                  <option value="forcible">منفذ جبريا</option>
                  <option value="revert">تراجع / إلغاء</option>
                </select>
              </label>

              {isStatusChange(form.occurrenceType) ? (
                <label className="block">
                  <span className="text-xs text-gray-500 block mb-1">تاريخ الوقعة</span>
                  <input
                    value={form.eventDate}
                    onChange={(e) => set('eventDate', e.target.value)}
                    placeholder="مثال: 1/8/2026"
                    className={inputCls}
                  />
                </label>
              ) : (
                <label className="block">
                  <span className="text-xs text-gray-500 block mb-1">
                    {form.occurrenceType === 'renewal' ? 'تاريخ التجديد' : 'تاريخ الشطب'}
                  </span>
                  <input
                    value={form.eventDate}
                    onChange={(e) => set('eventDate', e.target.value)}
                    placeholder="مثال: 1/8/2026"
                    className={inputCls}
                  />
                </label>
              )}

              {!isStatusChange(form.occurrenceType) && (
                <label className="block">
                  <span className="text-xs text-gray-500 block mb-1">
                    {form.occurrenceType === 'renewal' ? 'رقم الملف الجديد' : 'الرقم المشطوب'}
                  </span>
                  <input
                    value={form.fileNumber}
                    onChange={(e) => set('fileNumber', e.target.value)}
                    placeholder={form.occurrenceType === 'renewal' ? 'رقم الملف الجديد...' : 'الرقم المشطوب...'}
                    className={inputCls}
                  />
                </label>
              )}

              {form.occurrenceType === 'renewal' && (
                <>
                  <label className="block">
                    <span className="text-xs text-gray-500 block mb-1">نوع الملف الجديد</span>
                    <input
                      value={form.fileType}
                      onChange={(e) => set('fileType', e.target.value)}
                      placeholder="نوع الملف الجديد..."
                      className={inputCls}
                    />
                  </label>
                  {pinRenewalYear && restoringStruckOff ? (
                    <div className="block">
                      <span className="text-xs text-gray-500 block mb-1">سنة الإعادة</span>
                      <div className="text-sm font-medium text-gray-800 min-h-11 flex items-center">
                        {pinnedRenewalYear()} (سنة اليوم — ثابتة لعائلة «منفذ عليها»)
                      </div>
                    </div>
                  ) : (
                    <label className="block">
                      <span className="text-xs text-gray-500 block mb-1">سنة الإعادة</span>
                      <input
                        value={form.year}
                        onChange={(e) => set('year', e.target.value)}
                        placeholder="مثال: 2026"
                        inputMode="numeric"
                        className={inputCls}
                      />
                    </label>
                  )}
                  <label className="block">
                    <span className="text-xs text-gray-500 block mb-1">رقم ورود اخطار التجديد</span>
                    <input
                      value={form.receiptNumber}
                      onChange={(e) => set('receiptNumber', e.target.value)}
                      placeholder="رقم ورود اخطار التجديد..."
                      className={inputCls}
                    />
                  </label>
                  <label className="block">
                    <span className="text-xs text-gray-500 block mb-1">تاريخ ورود اخطار التجديد</span>
                    <input
                      value={form.receiptDate}
                      onChange={(e) => set('receiptDate', e.target.value)}
                      placeholder="مثال: 1/8/2026"
                      className={inputCls}
                    />
                  </label>
                </>
              )}

              {isStatusChange(form.occurrenceType) && (
                <StatusChangeFields
                  occurrenceType={form.occurrenceType}
                  details={form.details}
                  onSet={setDetail}
                  inputCls={inputCls}
                />
              )}
            </div>

            <div className="mt-4 flex gap-2 flex-wrap">
              <button
                type="button"
                onClick={save}
                disabled={busy}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {busy ? 'جارِ الحفظ...' : 'حفظ الوقعة'}
              </button>
              <button
                type="button"
                onClick={closeForm}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          </div>
        ) : (
          <button
            type="button"
            onClick={openAdd}
            className="bg-gray-100 hover:bg-gray-200 text-gray-800 rounded-lg px-4 py-2 text-sm font-medium min-h-11"
          >
            + إضافة وقعة
          </button>
        )}
      </div>
    </div>
  );
}

/** حقول إجراء تغيير الحالة (نظام «طالبة تنفيذ») في محرر الوقوعات اليدوي. */
function StatusChangeFields({
  occurrenceType,
  details,
  onSet,
  inputCls,
}: {
  occurrenceType: OccurrenceType;
  details: Record<string, string>;
  onSet: (key: string, value: string) => void;
  inputCls: string;
}) {
  if (occurrenceType === 'forcible') {
    return (
      <>
        <label className="block">
          <span className="text-xs text-gray-500 block mb-1">نوع التنفيذ</span>
          <select value={details.execSubStatus ?? ''} onChange={(e) => onSet('execSubStatus', e.target.value)} className={inputCls}>
            <option value="منفذ جزئيا">منفذ جزئيا</option>
            <option value="منفذ كاملا">منفذ كاملا</option>
          </select>
        </label>
        {[1, 2, 3].map((i) => (
          <CollectedInput key={i} index={i} details={details} onSet={onSet} inputCls={inputCls} />
        ))}
        <label className="block md:col-span-2">
          <span className="text-xs text-gray-500 block mb-1">أرقام الأموال المباعة بالمزاد (مفصولة بفواصل)</span>
          <input
            value={details.soldAssetIds ?? ''}
            onChange={(e) => onSet('soldAssetIds', e.target.value)}
            placeholder="مثال: 1,2"
            className={inputCls}
          />
        </label>
      </>
    );
  }

  if (occurrenceType === 'settled') {
    return (
      <>
        {STATUS_CHANGE_FIELDS.settled.map(([key, label]) => (
          <StatusField key={key} fieldKey={key} label={label} value={details[key] ?? ''} onSet={onSet} inputCls={inputCls} />
        ))}
        {[1, 2, 3].map((i) => (
          <CollectedInput key={i} index={i} details={details} onSet={onSet} inputCls={inputCls} />
        ))}
      </>
    );
  }

  const fields = STATUS_CHANGE_FIELDS[occurrenceType] ?? [];
  return (
    <>
      {fields.map(([key, label]) => (
        <StatusField key={key} fieldKey={key} label={label} value={details[key] ?? ''} onSet={onSet} inputCls={inputCls} />
      ))}
    </>
  );
}

function StatusField({
  fieldKey,
  label,
  value,
  onSet,
  inputCls,
}: {
  fieldKey: string;
  label: string;
  value: string;
  onSet: (key: string, value: string) => void;
  inputCls: string;
}) {
  return (
    <label className="block">
      <span className="text-xs text-gray-500 block mb-1">{label}</span>
      <input value={value} onChange={(e) => onSet(fieldKey, e.target.value)} className={inputCls} />
    </label>
  );
}

function CollectedInput({
  index,
  details,
  onSet,
  inputCls,
}: {
  index: number;
  details: Record<string, string>;
  onSet: (key: string, value: string) => void;
  inputCls: string;
}) {
  const amountKey = index === 1 ? 'collectedAmount' : `collectedAmount${index}`;
  const currencyKey = index === 1 ? 'collectedCurrency' : `collectedCurrency${index}`;
  return (
    <>
      <label className="block">
        <span className="text-xs text-gray-500 block mb-1">{index === 1 ? 'المبلغ المحصل' : `المبلغ المحصل ${index}`}</span>
        <input
          type="number"
          value={details[amountKey] ?? ''}
          onChange={(e) => onSet(amountKey, e.target.value)}
          className={inputCls}
        />
      </label>
      <label className="block">
        <span className="text-xs text-gray-500 block mb-1">العملة</span>
        <select value={details[currencyKey] ?? ''} onChange={(e) => onSet(currencyKey, e.target.value)} className={inputCls}>
          <option value="ليرة سورية">ليرة سورية</option>
          <option value="دولار أمريكي">دولار أمريكي</option>
          <option value="يورو">يورو</option>
        </select>
      </label>
    </>
  );
}
