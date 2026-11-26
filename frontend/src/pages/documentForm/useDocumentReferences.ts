import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../../api/client';
import { blockedAssetIds } from '../../utils/delegationAssets';
import type { AppealDto, AssetDto, DelegationDto, ExecutionCircuitDto } from '../../types';

interface UseDocumentReferencesInput {
  id: string | undefined;
  isEdit: boolean;
  assets: AssetDto[];
  isMirror: boolean;
}

/**
 * البيانات المرجعية والارتباطات المحيطة بملف قائم: دوائر الفرع (سجل الدوائر)،
 * الإنابات والاستئنافات (لقفل الأصول ومنع الحذف)، ونوافذ الاختيار. كل الجلب
 * منتقل حرفيًا من `DocumentForm` مع حارس سباق `cancelled` نفسه.
 */
export function useDocumentReferences({ id, isEdit, assets, isMirror }: UseDocumentReferencesInput) {
  // نافذة اختيار الجهة العامة من السجل المرجعي (المرحلة 2): الجهة المستهدفة
  // من الطرفين ورقم صفها، وتُملأ حقولها النصية من القيد المختار مع ربطه.
  const [registryPicker, setRegistryPicker] = useState<
    { side: 'applicant' | 'executed'; index: number } | null
  >(null);
  // سجل دوائر التنفيذ (BQ-004): الحقل زر مقفل بداخله «اختيار» — القائمة من
  // دوائر الفرع حصرًا، والإلزامية تمنع الحفظ مع تركيز أول خطأ.
  // انتقالي: فرع بلا سجل (قائمة فارغة/فشل تحميل) يُقبل النص الحر حتى يُعبَّأ السجل.
  const [circuitPickerOpen, setCircuitPickerOpen] = useState(false);
  const [branchCircuits, setBranchCircuits] = useState<ExecutionCircuitDto[] | null>(null);
  const circuitPickerButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const r = await api.get<ExecutionCircuitDto[]>('/execution-circuits/for-lawyer');
        if (!cancelled) setBranchCircuits(r?.data ?? []);
      } catch {
        if (!cancelled) setBranchCircuits(null);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // إنابات الملف في وضع التعديل — لقفل «الحجز بعد التسطير» أماميًا (مرآة مريحة فقط؛
  // الخلفية هي الضامن). الفشل مفتوح: التحرير يستمر بلا قفل والرفض النهائي خلفيًا.
  const [delegations, setDelegations] = useState<DelegationDto[]>([]);
  const [delegationsKnown, setDelegationsKnown] = useState(false);
  useEffect(() => {
    if (!isEdit || id === undefined) {
      setDelegations([]);
      setDelegationsKnown(true);
      return;
    }
    let cancelled = false;
    setDelegationsKnown(false);
    api
      .get<DelegationDto[]>(`/documents/${id}/delegations`)
      .then((r) => {
        if (cancelled) return;
        setDelegations(Array.isArray(r.data) ? r.data : []);
        setDelegationsKnown(true);
      })
      .catch(() => {
        if (cancelled) return;
        setDelegations([]);
        setDelegationsKnown(true);
      });
    return () => {
      cancelled = true;
    };
  }, [id, isEdit]);

  // الأصول المرجعية بإنابة سارية (مطابقة اللقطات نفسها المستعملة في العرض) — لا قفل
  // قبل العلم بها (مفتوح عند الجهل/الفشل)، ولا قفل على صفوف بلا معرف (جديدة).
  const referencedAssetIds = useMemo(
    () => (delegationsKnown ? blockedAssetIds(delegations, assets) : new Set<number>()),
    [delegations, assets, delegationsKnown],
  );

  // استئنافات الملف في وضع التعديل — لمنع الحذف استباقيًا عند العلم المؤكد بارتباط
  // (إنابة بأي حالة عبر قائمة الإنابات أعلاه، أو ملف مناب، أو أي استئناف بأي حالة).
  // مرآة مريحة فقط والخلفية هي الضامن: الفشل مفتوح (زر مفعّل والرفض النهائي خلفيًا).
  const [appeals, setAppeals] = useState<AppealDto[]>([]);
  useEffect(() => {
    if (!isEdit || id === undefined) {
      setAppeals([]);
      return;
    }
    let cancelled = false;
    api
      .get<AppealDto[]>(`/documents/${id}/appeals`)
      .then((r) => {
        if (cancelled) return;
        setAppeals(Array.isArray(r.data) ? r.data : []);
      })
      .catch(() => {
        if (cancelled) return;
        setAppeals([]);
      });
    return () => {
      cancelled = true;
    };
  }, [id, isEdit]);

  // ارتباط مؤكد يمنع الحذف: أي إنابة (القائمة تشمل إنابة المناب نفسه) أو ملف مناب
  // أو أي استئناف — بلا اعتبار للحالة. المصفوفات الفارغة تعني «لا ارتباط معلوم»
  // (جهل/فشل) فيبقى الزر مفعّلًا والخلفي يرفض — فشل مفتوح.
  const hasLinkedMatter = delegations.length > 0 || isMirror || appeals.length > 0;

  return {
    registryPicker,
    setRegistryPicker,
    circuitPickerOpen,
    setCircuitPickerOpen,
    branchCircuits,
    circuitPickerButtonRef,
    referencedAssetIds,
    delegationsKnown,
    hasLinkedMatter,
  };
}
