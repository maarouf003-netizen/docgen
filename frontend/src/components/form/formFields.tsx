import type { DocumentUpsertRequest } from '../../types';
import type { LabelValueOption } from './documentFormConstants';

export type FormSet = (key: keyof DocumentUpsertRequest, value: unknown) => void;

/**
 * أدوات حقول النموذج الموحّدة (حقل نصي / حقل مصنّف / قائمة اختيار) المرتبطة
 * بنموذج معين ومعالج تحديثه، لتتجنب الأقسام تكرار نفس البنية.
 */
export function makeFieldHelpers(form: DocumentUpsertRequest, set: FormSet) {
  const input = (
    key: keyof DocumentUpsertRequest,
    placeholder = '',
    type = 'text',
    cls = '',
  ) => (
    <input
      id={key}
      type={type}
      value={(form[key] as string | number | undefined) ?? ''}
      onChange={(e) => set(key, type === 'number' ? Number(e.target.value) : e.target.value)}
      {...(placeholder ? { placeholder } : {})}
      className={cls || 'w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500'}
    />
  );

  const field = (label: string, key: keyof DocumentUpsertRequest, placeholder = '', type = 'text') => (
    <div>
      <label htmlFor={key} className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
      {input(key, placeholder, type)}
    </div>
  );

  const selectField = (
    label: string,
    id: string,
    options: string[],
    value: string | undefined,
    onChange: (v: string) => void,
    extraClass = '',
  ) => (
    <div className={extraClass}>
      <label htmlFor={id} className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
      <select
        id={id}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
      >
        {options.map((o) => (
          <option key={o}>{o}</option>
        ))}
      </select>
    </div>
  );

  const optionSelectField = (
    label: string,
    id: string,
    options: LabelValueOption[],
    value: string | undefined,
    onChange: (v: string) => void,
    extraClass = '',
  ) => (
    <div className={extraClass}>
      <label htmlFor={id} className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
      <select
        id={id}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
    </div>
  );

  return { input, field, selectField, optionSelectField };
}
