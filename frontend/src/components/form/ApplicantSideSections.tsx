import type { Dispatch, SetStateAction } from 'react';
import type { AssetDto, DocumentUpsertRequest, GuarantorDto, HeirDto, PartyNature } from '../../types';
import { makeFieldHelpers, type FormSet } from './formFields';
import { ApplicantContractSection } from './applicantSide/ApplicantContractSection';
import { ApplicantBorrowerSection } from './applicantSide/ApplicantBorrowerSection';
import { ApplicantGuarantorsSection } from './applicantSide/ApplicantGuarantorsSection';
import { ApplicantAssetsSection } from './applicantSide/ApplicantAssetsSection';

export interface ApplicantSideSectionsProps {
  form: DocumentUpsertRequest;
  set: FormSet;
  isOrdinary: boolean;
  /** الملف المناب (قرار 7): تُقفل السند/المقترض/الكفلاء/الجهات وتُخفى أزرارها وأقسام الأصول،
   * مع بقاء ورثة/ممثل الملف المحليين (والممثل/الورثة الجدد) قابلين للتحرير. */
  isMirror?: boolean;
  /** قفل ممثل الطرف على الملف المناب: المخزَّن فقط، لا الإضافة المحلية الجارية. */
  partyRepLocked: (key: string) => boolean;
  guarantorLabel: string;
  showInclusionAmount: boolean;
  setShowInclusionAmount: Dispatch<SetStateAction<boolean>>;
  ordinaryAmountSlots: number;
  setOrdinaryAmountSlots: (n: number) => void;
  bankingAmountSlots: number;
  setBankingAmountSlots: (n: number) => void;
  borrowerHeirs: HeirDto[];
  onBorrowerHeirSet: (i: number, key: keyof HeirDto, value: string) => void;
  onBorrowerHeirAdd: () => void;
  onBorrowerHeirRemove: (i: number) => void;
  guarantors: GuarantorDto[];
  onGuarantorSet: (i: number, key: keyof GuarantorDto, value: string) => void;
  onGuarantorHeirSet: (gi: number, hi: number, key: keyof HeirDto, value: string) => void;
  onGuarantorHeirAdd: (gi: number) => void;
  onGuarantorHeirRemove: (gi: number, hi: number) => void;
  onGuarantorAdd: (nature?: PartyNature) => void;
  onGuarantorRemove: (i: number) => void;
  onBorrowerRepActivate: () => void;
  onBorrowerRepRemove: () => void;
  onGuarantorRepActivate: (i: number) => void;
  onGuarantorRepRemove: (i: number) => void;
  assets: AssetDto[];
  onEstateSet: (i: number, key: keyof AssetDto, value: string) => void;
  /** معرفات الأصول المرجعية بإنابة سارية (من نموذج الملف): تاريخ حجزها يُعدَّل ولا
   * يُحذف — يُعرض تلميح توضيحي تحت الحقل، والمنع الفعلي في onEstateSet والخلفية. */
  seizureLockedIds?: Set<number>;
  onEstateRemove: (i: number) => void;
  onOwnerToggle: (i: number, name: string) => void;
  onSingleOwnerSet: (i: number, name: string) => void;
  onEstateAdd: (kind: string) => void;
  ownerOptions: () => string[];
}

/**
 * أقسام وضع «الجهة العامة طالبة التنفيذ» (المصرفي/العادي) في نموذج الملف —
 * قشرة تركيبية بعد `P6-02c`: السند، المقترض، الكفلاء، والأموال.
 */
export function ApplicantSideSections({
  form,
  set,
  isOrdinary,
  isMirror = false,
  partyRepLocked,
  guarantorLabel,
  showInclusionAmount,
  setShowInclusionAmount,
  ordinaryAmountSlots,
  setOrdinaryAmountSlots,
  bankingAmountSlots,
  setBankingAmountSlots,
  borrowerHeirs,
  onBorrowerHeirSet,
  onBorrowerHeirAdd,
  onBorrowerHeirRemove,
  guarantors,
  onGuarantorSet,
  onGuarantorHeirSet,
  onGuarantorHeirAdd,
  onGuarantorHeirRemove,
  onGuarantorAdd,
  onGuarantorRemove,
  onBorrowerRepActivate,
  onBorrowerRepRemove,
  onGuarantorRepActivate,
  onGuarantorRepRemove,
  assets,
  onEstateSet,
  seizureLockedIds,
  onEstateRemove,
  onOwnerToggle,
  onSingleOwnerSet,
  onEstateAdd,
  ownerOptions,
}: ApplicantSideSectionsProps) {
  const isBanking = !isOrdinary;
  const lock = isMirror;
  const { field, selectField, optionSelectField } = makeFieldHelpers(form, set, lock);

  return (
    <>
      <ApplicantContractSection
        form={form}
        set={set}
        isOrdinary={isOrdinary}
        isBanking={isBanking}
        lock={lock}
        field={field}
        selectField={selectField}
        showInclusionAmount={showInclusionAmount}
        setShowInclusionAmount={setShowInclusionAmount}
        ordinaryAmountSlots={ordinaryAmountSlots}
        setOrdinaryAmountSlots={setOrdinaryAmountSlots}
        bankingAmountSlots={bankingAmountSlots}
        setBankingAmountSlots={setBankingAmountSlots}
      />
      <ApplicantBorrowerSection
        form={form}
        set={set}
        lock={lock}
        isOrdinary={isOrdinary}
        field={field}
        optionSelectField={optionSelectField}
        borrowerHeirs={borrowerHeirs}
        onBorrowerHeirSet={onBorrowerHeirSet}
        onBorrowerHeirAdd={onBorrowerHeirAdd}
        onBorrowerHeirRemove={onBorrowerHeirRemove}
        onBorrowerRepActivate={onBorrowerRepActivate}
        onBorrowerRepRemove={onBorrowerRepRemove}
        partyRepLocked={partyRepLocked}
      />
      <ApplicantGuarantorsSection
        isOrdinary={isOrdinary}
        lock={lock}
        guarantorLabel={guarantorLabel}
        guarantors={guarantors}
        onGuarantorSet={onGuarantorSet}
        onGuarantorHeirSet={onGuarantorHeirSet}
        onGuarantorHeirAdd={onGuarantorHeirAdd}
        onGuarantorHeirRemove={onGuarantorHeirRemove}
        onGuarantorAdd={onGuarantorAdd}
        onGuarantorRemove={onGuarantorRemove}
        onGuarantorRepActivate={onGuarantorRepActivate}
        onGuarantorRepRemove={onGuarantorRepRemove}
        partyRepLocked={partyRepLocked}
      />
      <ApplicantAssetsSection
        lock={lock}
        assets={assets}
        onEstateSet={onEstateSet}
        seizureLockedIds={seizureLockedIds}
        onEstateRemove={onEstateRemove}
        onOwnerToggle={onOwnerToggle}
        onSingleOwnerSet={onSingleOwnerSet}
        onEstateAdd={onEstateAdd}
        ownerOptions={ownerOptions}
      />
    </>
  );
}
