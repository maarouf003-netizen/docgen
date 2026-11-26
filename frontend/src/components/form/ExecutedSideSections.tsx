import type { Dispatch, SetStateAction } from 'react';
import type {
  DocumentUpsertRequest,
  EntityNature,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  PartyNature,
} from '../../types';
import { makeFieldHelpers, type FormSet } from './formFields';
import { ExecutedContractSection } from './executedSide/ExecutedContractSection';
import { ExecutedApplicantSection } from './executedSide/ExecutedApplicantSection';
import { ExecutedEntitySection } from './executedSide/ExecutedEntitySection';
import { ExecutedNaturalPersonSection } from './executedSide/ExecutedNaturalPersonSection';
import { ExecutedStatusSection } from './executedSide/ExecutedStatusSection';

export interface ExecutedSideSectionsProps {
  /** صفة الملف: executed = «الجهة العامة منفذ عليها»، deposit = «عرض وايداع». */
  side: 'executed' | 'deposit';
  form: DocumentUpsertRequest;
  set: FormSet;
  /** الملف المناب (قرار 7): تُقفل السند/طلاب التنفيذ/المنفذ عليهم وتُخفى أزرارها، مع بقاء
   * ورثة/ممثلي الملف المحليين قابلين للتحرير (احترازي — الإنابة تخص «طالبة تنفيذ» حصرًا). */
  isMirror?: boolean;
  /** قفل ممثل الطرف على الملف المناب: المخزَّن فقط، لا الإضافة المحلية الجارية. */
  partyRepLocked: (key: string) => boolean;
  showRequiredAmount: boolean;
  setShowRequiredAmount: Dispatch<SetStateAction<boolean>>;
  requiredAmountSlots: number;
  setRequiredAmountSlots: (n: number) => void;
  executionApplicants: ExecutionApplicantDto[];
  onApplicantSet: (i: number, key: keyof ExecutionApplicantDto, value: string) => void;
  onApplicantAdd: (nature?: PartyNature) => void;
  onApplicantRemove: (i: number) => void;
  onApplicantHeirSet: (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) => void;
  onApplicantHeirAdd: (i: number) => void;
  onApplicantHeirRemove: (i: number, hi: number) => void;
  onApplicantRepActivate: (i: number) => void;
  onApplicantRepRemove: (i: number) => void;
  executedPublicEntities: ExecutedPublicEntityDto[];
  onEntitySet: (i: number, key: keyof ExecutedPublicEntityDto, value: string) => void;
  onEntityAdd: (nature?: EntityNature) => void;
  onEntityRemove: (i: number) => void;
  /** فتح نافذة اختيار الجهة العامة من السجل المرجعي لصف المنفذ رقم i (اختياري). */
  onPickRegistry?: (i: number) => void;
  /** فك ربط جهة عامة منفذ عليها: يُفرّغ هويتها ورابطها (حقولها مقفلة لا تقبل تحريرًا يدويًا). */
  onEntityUnlink?: (i: number) => void;
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  onPersonSet: (i: number, key: keyof ExecutedNaturalPersonDto, value: string) => void;
  onPersonAdd: () => void;
  onPersonRemove: (i: number) => void;
  onPersonHeirSet: (i: number, hi: number, key: keyof ExecutedHeirDto, value: string) => void;
  onPersonHeirAdd: (i: number) => void;
  onPersonHeirRemove: (i: number, hi: number) => void;
  onPersonRepActivate: (i: number) => void;
  onPersonRepRemove: (i: number) => void;
  /** عدد خانات المبلغ المدفوع المعروضة (1 إلى 3) في وضع «منفذ عليها»/«عرض وايداع». */
  paidAmountSlots: number;
  setPaidAmountSlots: (n: number) => void;
  /** هل كان الملف مشطوبًا قبل التعديل؟ (يكشف انتقال مشطوب ← متداول يُظهر حقول التجديد). */
  wasOriginallyStruckOff?: boolean;
  /** حالة وضع الملف قبل التعديل (لترشيح خيارات «الحالة» بمنطق نافذة تغيير الحالة نفسها —
   * تُقفل الحالات الممنوعة ولا تُعرض كخيارات قابلة للاختيار). */
  currentExecutedStatus?: string;
}

/**
 * أقسام عائلة وضع «منفذ عليه» في نموذج الملف (الجهة العامة منفذ عليها / عرض وايداع) —
 * قشرة تركيبية بعد `P6-02d`: السند، الطالب، المنفذ عليهم، الأشخاص الطبيعيون، والحالة.
 */
export function ExecutedSideSections({
  side,
  form,
  set,
  isMirror = false,
  partyRepLocked,
  showRequiredAmount,
  setShowRequiredAmount,
  requiredAmountSlots,
  setRequiredAmountSlots,
  executionApplicants,
  onApplicantSet,
  onApplicantAdd,
  onApplicantRemove,
  onApplicantHeirSet,
  onApplicantHeirAdd,
  onApplicantHeirRemove,
  onApplicantRepActivate,
  onApplicantRepRemove,
  executedPublicEntities,
  onEntitySet,
  onEntityAdd,
  onEntityRemove,
  onPickRegistry,
  onEntityUnlink,
  executedNaturalPersons,
  onPersonSet,
  onPersonAdd,
  onPersonRemove,
  onPersonHeirSet,
  onPersonHeirAdd,
  onPersonHeirRemove,
  onPersonRepActivate,
  onPersonRepRemove,
  paidAmountSlots,
  setPaidAmountSlots,
  wasOriginallyStruckOff,
  currentExecutedStatus,
}: ExecutedSideSectionsProps) {
  const { field } = makeFieldHelpers(form, set, isMirror);
  const isDeposit = side === 'deposit';

  return (
    <>
      <ExecutedContractSection
        form={form}
        set={set}
        isDeposit={isDeposit}
        field={field}
        showRequiredAmount={showRequiredAmount}
        setShowRequiredAmount={setShowRequiredAmount}
        requiredAmountSlots={requiredAmountSlots}
        setRequiredAmountSlots={setRequiredAmountSlots}
      />
      <ExecutedApplicantSection
        isDeposit={isDeposit}
        isMirror={isMirror}
        partyRepLocked={partyRepLocked}
        executionApplicants={executionApplicants}
        onApplicantSet={onApplicantSet}
        onApplicantAdd={onApplicantAdd}
        onApplicantRemove={onApplicantRemove}
        onApplicantHeirSet={onApplicantHeirSet}
        onApplicantHeirAdd={onApplicantHeirAdd}
        onApplicantHeirRemove={onApplicantHeirRemove}
        onApplicantRepActivate={onApplicantRepActivate}
        onApplicantRepRemove={onApplicantRepRemove}
      />
      <ExecutedEntitySection
        isMirror={isMirror}
        executedPublicEntities={executedPublicEntities}
        onEntitySet={onEntitySet}
        onEntityAdd={onEntityAdd}
        onEntityRemove={onEntityRemove}
        onPickRegistry={onPickRegistry}
        onEntityUnlink={onEntityUnlink}
        onPersonAdd={onPersonAdd}
      />
      <ExecutedNaturalPersonSection
        isMirror={isMirror}
        partyRepLocked={partyRepLocked}
        executedNaturalPersons={executedNaturalPersons}
        onPersonSet={onPersonSet}
        onPersonRemove={onPersonRemove}
        onPersonHeirSet={onPersonHeirSet}
        onPersonHeirAdd={onPersonHeirAdd}
        onPersonHeirRemove={onPersonHeirRemove}
        onPersonRepActivate={onPersonRepActivate}
        onPersonRepRemove={onPersonRepRemove}
      />
      <ExecutedStatusSection
        form={form}
        set={set}
        isDeposit={isDeposit}
        field={field}
        paidAmountSlots={paidAmountSlots}
        setPaidAmountSlots={setPaidAmountSlots}
        wasOriginallyStruckOff={wasOriginallyStruckOff}
        currentExecutedStatus={currentExecutedStatus}
      />
    </>
  );
}
