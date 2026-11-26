import { useState } from 'react';
import type { GuarantorDto, HeirDto, PartyNature } from '../../../types';
import {
  ADDRESS_TYPE_OPTIONS,
  MAX_GUARANTORS,
  PARTY_NATURE_OPTIONS,
  addressLabelOf,
  guarantorAddState,
  hasHeirName,
  hasRepresentative,
  hasStoredRepresentative,
} from '../documentFormConstants';
import { HeirsEditor } from '../HeirsEditors';
import { RepresentativeEditor } from '../RepresentativeEditor';
import { FormSectionTitle } from '../FormSectionTitle';

interface ApplicantGuarantorsSectionProps {
  isOrdinary: boolean;
  lock: boolean;
  guarantorLabel: string;
  guarantors: GuarantorDto[];
  onGuarantorSet: (i: number, key: keyof GuarantorDto, value: string) => void;
  onGuarantorHeirSet: (gi: number, hi: number, key: keyof HeirDto, value: string) => void;
  onGuarantorHeirAdd: (gi: number) => void;
  onGuarantorHeirRemove: (gi: number, hi: number) => void;
  onGuarantorAdd: (nature?: PartyNature) => void;
  onGuarantorRemove: (i: number) => void;
  onGuarantorRepActivate: (i: number) => void;
  onGuarantorRepRemove: (i: number) => void;
  partyRepLocked: (key: string) => boolean;
}

/**
 * بطاقات الكفلاء/المنفذ عليهم الآخرين (حتى 49) بنمط الأكورديون وقواعد
 * الورثة/الممثل الشرعي — منقولة حرفيًا من `ApplicantSideSections`.
 */
export function ApplicantGuarantorsSection({
  isOrdinary,
  lock,
  guarantorLabel,
  guarantors,
  onGuarantorSet,
  onGuarantorHeirSet,
  onGuarantorHeirAdd,
  onGuarantorHeirRemove,
  onGuarantorAdd,
  onGuarantorRemove,
  onGuarantorRepActivate,
  onGuarantorRepRemove,
  partyRepLocked,
}: ApplicantGuarantorsSectionProps) {
  // حالة زر الإضافة (عددًا ورقمًا) تُحسب مرة واحدة: تُستخدم في الزرين والنص.
  const guarantorAdd = guarantorAddState(guarantors, isOrdinary);
  // طيّ بطاقات الكفلاء (أكورديون): مع حتى 49 بطاقة تُطوى الأجسام لتخفيف التخطيط
  // على الجوال، وتبقى البيانات في حالة الأب فلا يفقد الطي أي إدخال. المفتاح كهوية البطاقة.
  const cardKeyOf = (g: GuarantorDto, i: number) => (isOrdinary ? `o-${i}` : `b-${g.guarantorNumber ?? i + 1}`);
  const guarantorSummary = (g: GuarantorDto) =>
    [g.name, g.father, g.family].map((s) => (s ?? '').trim()).filter(Boolean).join(' ');
  const [collapsedCards, setCollapsedCards] = useState<ReadonlySet<string>>(() => new Set());
  const toggleCard = (k: string) =>
    setCollapsedCards((prev) => {
      const next = new Set(prev);
      if (next.has(k)) next.delete(k);
      else next.add(k);
      return next;
    });
  const allCollapsed = guarantors.length > 0 && guarantors.every((g, i) => collapsedCards.has(cardKeyOf(g, i)));

  return (
    <>
      <FormSectionTitle title={isOrdinary ? '👥 المنفذ عليهم الآخرون' : '👥 الكفلاء'} />
      {guarantors.length > 1 && (
        <div className="flex justify-end mb-2">
          <button
            type="button"
            onClick={() => setCollapsedCards(allCollapsed ? new Set() : new Set(guarantors.map((g, i) => cardKeyOf(g, i))))}
            className="text-emerald-800 text-xs font-bold hover:underline min-h-11 px-2 rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {allCollapsed ? 'فتح الكل' : 'طي الكل'}
          </button>
        </div>
      )}
      {guarantors.map((g, i) => {
        // كالمقترض أعلاه: الفارغ من الاسم الثلاثي ليس وريثًا مخزَّنًا.
        const gHasHeirs = (g.heirs ?? []).some(hasHeirName);
        const gHasHeirRows = (g.heirs ?? []).length > 0;
        const gHasRep = hasRepresentative(g);
        // إخفاء العنوان يتبع المخزَّن فعلًا (كحمولة toHeirPayload) — لا مجرد التفعيل بصفة.
        const gRepStored = hasStoredRepresentative(g);
        const gIsLegal = g.nature === 'legal';
        const cardKey = cardKeyOf(g, i);
        const isCollapsed = collapsedCards.has(cardKey);
        return (
          <div key={isOrdinary ? i : g.guarantorNumber ?? i + 1} className="border border-gray-200 rounded-xl p-4 mb-4">
            <div className="flex justify-between items-center gap-2 mb-3">
              <button
                type="button"
                onClick={() => toggleCard(cardKey)}
                aria-expanded={!isCollapsed}
                aria-controls={`guarantor-body-${cardKey}`}
                className="flex min-w-0 flex-1 items-center gap-2 text-right font-medium text-gray-700 text-sm min-h-11 rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                <span aria-hidden="true" className="shrink-0 text-gray-400">{isCollapsed ? '◀' : '▼'}</span>
                <span className="shrink-0">
                  {guarantorLabel} {isOrdinary ? i + 2 : g.guarantorNumber ?? i + 1}
                </span>
                {guarantorSummary(g) ? (
                  <span className="min-w-0 flex-1 truncate text-xs font-normal text-gray-400">— {guarantorSummary(g)}</span>
                ) : null}
              </button>
              {!lock && guarantors.length > 1 && (
                <button type="button" onClick={() => onGuarantorRemove(i)} aria-label={`حذف ${guarantorLabel} ${isOrdinary ? i + 2 : g.guarantorNumber ?? i + 1}`} className="shrink-0 text-red-500 text-xs hover:underline min-h-11 px-1 rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500">
                  ✖ حذف
                </button>
              )}
            </div>
            {!isCollapsed && (
            <div id={`guarantor-body-${cardKey}`}>
            <div className="grid md:grid-cols-3 gap-3 mb-3">
              <select
                aria-label="نوع الطرف"
                value={g.nature ?? 'natural'}
                onChange={(e) => onGuarantorSet(i, 'nature', e.target.value)}
                disabled={lock}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed"
              >
                {PARTY_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            </div>
            {gIsLegal ? (
              <div className="grid md:grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">الشخص الاعتباري</label>
                  <input value={g.name ?? ''} readOnly={lock} aria-readonly={lock || undefined} onChange={(e) => onGuarantorSet(i, 'name', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">رقم تسجيله</label>
                  <input value={g.registrationNumber ?? ''} readOnly={lock} aria-readonly={lock || undefined} onChange={(e) => onGuarantorSet(i, 'registrationNumber', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">يمثلها</label>
                  <input value={g.representedBy ?? ''} readOnly={lock} aria-readonly={lock || undefined} onChange={(e) => onGuarantorSet(i, 'representedBy', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                  <select value={g.addressType ?? 'موطن مختار'} onChange={(e) => onGuarantorSet(i, 'addressType', e.target.value)} disabled={lock} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-gray-50 disabled:cursor-not-allowed">
                    {ADDRESS_TYPE_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(g.addressType)}</label>
                  <input value={g.address ?? ''} readOnly={lock} aria-readonly={lock || undefined} onChange={(e) => onGuarantorSet(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                </div>
              </div>
            ) : (
              <>
                <div className="grid md:grid-cols-5 gap-3">
                  {([
                    ['name', 'الاسم'],
                    ['father', 'اسم الأب'],
                    ['family', 'النسبة'],
                    ['mother', 'اسم الأم'],
                    ['birth', 'مكان وتاريخ الولادة'],
                    ['register', 'مكان ورقم القيد'],
                    ['nationalId', 'الرقم الوطني'],
                  ] as const).map(([k, label]) => (
                    <div key={k}>
                      <label className="block text-xs font-bold text-gray-600 mb-1">{label}</label>
                      <input value={g[k] ?? ''} readOnly={lock} aria-readonly={lock || undefined} onChange={(e) => onGuarantorSet(i, k, e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    </div>
                  ))}
                  {!gHasHeirs && !gRepStored && (
                    <>
                      <div>
                        <label className="block text-xs font-bold text-gray-600 mb-1">نوع العنوان</label>
                        <select value={g.addressType ?? 'موطن مختار'} onChange={(e) => onGuarantorSet(i, 'addressType', e.target.value)} disabled={lock} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none disabled:bg-gray-50 disabled:cursor-not-allowed">
                          {ADDRESS_TYPE_OPTIONS.map((o) => (
                            <option key={o.value} value={o.value}>{o.label}</option>
                          ))}
                        </select>
                      </div>
                      <div className="md:col-span-2">
                        <label className="block text-xs font-bold text-gray-600 mb-1">{addressLabelOf(g.addressType)}</label>
                        <input value={g.address ?? ''} readOnly={lock} aria-readonly={lock || undefined} onChange={(e) => onGuarantorSet(i, 'address', e.target.value)} className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                      </div>
                    </>
                  )}
                </div>
                <HeirsEditor
                  idPrefix={`guarantor-${i}`}
                  heirs={g.heirs ?? []}
                  onSet={(hi, k, v) => onGuarantorHeirSet(i, hi, k, v)}
                  onAdd={() => onGuarantorHeirAdd(i)}
                  onRemove={(hi) => onGuarantorHeirRemove(i, hi)}
                  locked={lock}
                  allowAdd={!gHasRep}
                />
                {!gHasRep && !gHasHeirRows && (
                  <div className="mt-3 flex justify-end">
                    <button
                      type="button"
                      onClick={() => onGuarantorRepActivate(i)}
                      className="bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
                    >
                      ＋ إضافة ممثل شرعي
                    </button>
                  </div>
                )}
                {!gHasRep && gHasHeirRows && (
                  <p className="mt-3 text-xs font-medium text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2" role="note">
                    لا يجتمع الممثل الشرعي مع الورثة — احذف الورثة لإضافة ممثل شرعي.
                  </p>
                )}
                {gHasRep && (
                  <RepresentativeEditor
                    idPrefix={`guarantor-${i}`}
                    mode="address"
                    representative={g}
                    onSet={(key, value) => onGuarantorSet(i, key as keyof GuarantorDto, value)}
                    onRemove={() => onGuarantorRepRemove(i)}
                    locked={partyRepLocked(`guarantor-${i}`)}
                  />
                )}
              </>
            )}
            </div>
            )}
          </div>
        );
      })}
      {!lock && (
        <div className="flex gap-4 items-center flex-wrap">
          <button
            type="button"
            onClick={() => onGuarantorAdd('natural')}
            disabled={!guarantorAdd.canAdd}
            className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            {!guarantorAdd.canAdd ? '🛑 الحد الأقصى' : `➕ إضافة ${guarantorLabel} (شخص طبيعي)`}
          </button>
          <button
            type="button"
            onClick={() => onGuarantorAdd('legal')}
            disabled={!guarantorAdd.canAdd}
            className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-bold rounded-md px-3 py-2 min-h-11"
          >
            {!guarantorAdd.canAdd ? '🛑 الحد الأقصى' : `➕ إضافة ${guarantorLabel} (شخص اعتباري)`}
          </button>
          <span className="text-xs text-gray-500">
            {guarantorAdd.canAdd
              ? `متبقي: ${guarantorAdd.remaining} من ${MAX_GUARANTORS}`
              : 'وصلت الحد الأقصى'}
          </span>
        </div>
      )}
    </>
  );
}
