import type {
  DocumentOccurrenceDto,
  DocumentResponse,
  ExecutedHeirDto,
  ExecutedNaturalPersonDto,
  HeirDto,
} from '../../types';
import { getDocumentStatus } from '../../utils/documentStatus';
import { isExecutedLike, tripleName } from '../../utils/documentDisplay';
import { formatDate } from '../../utils/dates';
import type { DetailsRow, HeirLine, PersonFields } from './viewTypes';

/** تنسيق المبالغ الصحيحة (حتى ثلاث كسور عشرية) عبر Intl.NumberFormat — الأصل الوحيد لعرض الأرقام هنا. */
const amountFormatter = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });

export function formatAmount(numeric: number, currency?: string): string {
  if (numeric <= 0) return '';
  const formatted = amountFormatter.format(numeric);
  return currency ? `${formatted} ${currency}`.trim() : formatted;
}

/** يجمع خانات المبالغ (حتى ثلاثة) بعملاتها في نصٍ واحد عبر الفاصل « — »، متجاهلًا الفارغ منها. */
function combineAmountSlots(
  pairs: ReadonlyArray<Readonly<[number | undefined, string | undefined]>>,
): string {
  return pairs
    .map(([amount, currency]) => formatAmount(amount ?? 0, currency))
    .filter(Boolean)
    .join(' — ');
}

/** المبالغ المطلوب دفعها من الجهة العامة (حتى ثلاثة) مجتمعةً بعملاتها. */
export function formatRequiredAmounts(doc: DocumentResponse): string {
  return combineAmountSlots([
    [doc.executedRequiredAmount, doc.executedRequiredCurrency],
    [doc.executedRequiredAmount2, doc.executedRequiredCurrency2],
    [doc.executedRequiredAmount3, doc.executedRequiredCurrency3],
  ]);
}

/** المبالغ المدفوعة من الجهة العامة / المبالغ المودعة (حتى ثلاثة) مجتمعةً بعملاتها. */
export function formatPaidAmounts(doc: DocumentResponse): string {
  return combineAmountSlots([
    [doc.executedPaidAmount, doc.executedPaidCurrency],
    [doc.executedPaidAmount2, doc.executedPaidCurrency2],
    [doc.executedPaidAmount3, doc.executedPaidCurrency3],
  ]);
}

export function formatFileNumber(doc: DocumentResponse): string {
  const number = doc.displayFileNumber ?? doc.fileNumber ?? '';
  const parts = [number];
  if (doc.fileType) parts.push(doc.fileType);
  if (doc.displayFileYear ?? doc.fileYear) parts.push(`لعام ${doc.displayFileYear ?? doc.fileYear}`);
  return parts.filter(Boolean).join(' ');
}

export function fullName(person: PersonFields): string {
  return tripleName(person.name, person.father, person.family);
}

/** تفصيل عنوان/وكيل الوريث: «عنوان: …» أو «يمثله …» (بدون تكرار السابقة إن كانت في القيمة). */
export function heirLineDetail(addressType: string | undefined, address: string | undefined): string {
  const value = (address ?? '').trim();
  if (!value) return '';
  if (addressType === 'وكيل') {
    const v = value.startsWith('يمثله') ? value.slice('يمثله'.length).trimStart() : value;
    return `يمثله ${v}`;
  }
  return `${addressType ?? 'عنوان'}: ${value}`;
}

export function buildHeirLines(heirs: HeirDto[] | undefined): HeirLine[] {
  return (heirs ?? [])
    .filter((h) => (h.name ?? '').trim() || (h.father ?? '').trim() || (h.family ?? '').trim())
    .map((h) => ({
      name: tripleName(h.name, h.father, h.family),
      detail: heirLineDetail(h.addressType, h.address),
    }));
}

export function buildExecutedHeirLines(heirs: ExecutedHeirDto[] | undefined): HeirLine[] {
  return (heirs ?? [])
    .filter((h) => (h.heirName ?? '').trim())
    .map((h) => ({
      name: tripleName(h.heirName, h.heirFather, h.heirFamily),
      detail: heirLineDetail(h.addressType, h.heirAddress),
    }));
}

/** حقول الهوية الكاملة لمنفذ عليه طبيعي في سيناريو طالبة التنفيذ. */
export function personRows(person: PersonFields): DetailsRow[] {
  const rows: DetailsRow[] = [
    { label: 'الاسم الثلاثي', value: fullName(person) },
    { label: 'اسم الأم', value: person.mother ?? '' },
    { label: 'مكان وتاريخ الولادة', value: person.birth ?? '' },
    { label: 'مكان ورقم القيد', value: person.register ?? '' },
    { label: 'الرقم الوطني', value: person.nationalId ?? '' },
  ];
  if (person.addressType === 'يمثله') {
    rows.push({ label: 'وكيله القانوني', value: person.address ?? '' });
  } else {
    rows.push({ label: 'نوع العنوان', value: person.addressType ?? '' });
    rows.push({ label: legalAddressLabel(person.addressType), value: person.address ?? '' });
  }
  return rows;
}

/** حقول الهوية الكاملة لشخص طبيعي في وضع «منفذ عليه». */
export function executedPersonRows(person: ExecutedNaturalPersonDto): DetailsRow[] {
  const rows: DetailsRow[] = [
    { label: 'الاسم الثلاثي', value: fullName(person) },
    { label: 'نوع التمثيل', value: person.representationType ?? 'أصالة' },
  ];
  const deceased = fullName({
    name: person.deceasedName,
    father: person.deceasedFather,
    family: person.deceasedFamily,
  });
  if (deceased) rows.push({ label: 'المورث المتوفى', value: deceased });
  if (person.addressType === 'وكيل') {
    rows.push({ label: 'الوكيل', value: person.addressOrRepresentative ?? '' });
  } else {
    rows.push({ label: 'نوع العنوان', value: person.addressType ?? 'عنوان' });
    rows.push({ label: 'العنوان', value: person.addressOrRepresentative ?? '' });
  }
  rows.push(...representativeRows(person));
  return rows;
}

/** سابقة التمثيل في السطر الفرعي للطرف: «إضافة لتركة المتوفى فلان» أو «أصالة وإضافة لتركة المتوفى فلان». */
export function representationSuffix(p: {
  representationType?: string;
  deceasedName?: string;
  deceasedFather?: string;
  deceasedFamily?: string;
}): string {
  const type = p.representationType;
  if (type !== 'إضافة لتركة' && type !== 'أصالة وإضافة') return '';
  const deceased = fullName({ name: p.deceasedName, father: p.deceasedFather, family: p.deceasedFamily });
  if (!deceased) return type === 'أصالة وإضافة' ? 'أصالة وإضافة' : 'إضافة لتركة';
  return type === 'أصالة وإضافة'
    ? `أصالة وإضافة لتركة المتوفى ${deceased}`
    : `إضافة لتركة المتوفى ${deceased}`;
}

/** سطر الممثل الشرعي: «ممثله الشرعي الولي فلان الفلاني» (أو الوصي/القيم) — أو فارغ إن غاب. */
export function representativeLine(p: {
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
}): string {
  const name = fullName({ name: p.representativeName, father: p.representativeFather, family: p.representativeFamily });
  if (!name) return '';
  const capacity = (p.representativeCapacity ?? '').trim();
  return capacity ? `ممثله الشرعي ال${capacity} ${name}` : `ممثله الشرعي ${name}`;
}

/** صفوف الممثل الشرعي في نافذة التفاصيل (الاسم والصّفة وعنوانه/وكيله القانوني) — أو فارغة إن غاب. */
export function representativeRows(p: {
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeAddressType?: string;
  representativeAddress?: string;
}): DetailsRow[] {
  const name = fullName({ name: p.representativeName, father: p.representativeFather, family: p.representativeFamily });
  if (!name) return [];
  const rows: DetailsRow[] = [];
  const capacity = (p.representativeCapacity ?? '').trim();
  rows.push({ label: 'الممثل الشرعي', value: capacity ? `${capacity} ${name}` : name });
  const address = (p.representativeAddress ?? '').trim();
  if (address) {
    const type = (p.representativeAddressType ?? '').trim();
    rows.push({
      label: type === 'موطن مختار' ? 'موطنه المختار' : type === 'وكيل قانوني' ? 'وكيله القانوني' : 'عنوانه',
      value: address,
    });
  }
  return rows;
}

/** صفوف الهوية الكاملة لطالب تنفيذ طبيعي في وضع «منفذ عليه». */
export function applicantNaturalRows(a: {
  name?: string;
  father?: string;
  family?: string;
  legalRepresentative?: string;
  representationType?: string;
  deceasedName?: string;
  deceasedFather?: string;
  deceasedFamily?: string;
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeLegalRepresentative?: string;
}): DetailsRow[] {
  const rows: DetailsRow[] = [
    { label: 'الاسم الثلاثي', value: fullName(a) },
    { label: 'نوع التمثيل', value: a.representationType ?? 'أصالة' },
  ];
  const deceased = fullName({ name: a.deceasedName, father: a.deceasedFather, family: a.deceasedFamily });
  if (deceased) rows.push({ label: 'المورث المتوفى', value: deceased });
  // الوكيل القانوني صف دائم: يظهر (بعد فلترة الفارغ في النافذة) متى وُجدت قيمته،
  // مهما كان نوع التمثيل أو وجود مورث متوفى — فلا تُفقد هذه المعلومة عن قصد.
  rows.push({ label: 'الوكيل القانوني', value: a.legalRepresentative ?? '' });
  rows.push(...representativeRows({
    representativeName: a.representativeName,
    representativeFather: a.representativeFather,
    representativeFamily: a.representativeFamily,
    representativeCapacity: a.representativeCapacity,
  }));
  return rows;
}

/** عنوان ملف عائلة وضع «منفذ عليه»: أول منفذ عليه (طبيعي/جهة)، ثم طالب التنفيذ/العرض، ثم الصفة. */
export function executedTitle(doc: DocumentResponse): string {
  const person = doc.executedNaturalPersons?.[0];
  const personName = person ? fullName(person) : '';
  const entity = doc.executedPublicEntities[0]?.entityName ?? '';
  const applicantName = doc.executionApplicants[0] ? fullName(doc.executionApplicants[0]) : '';
  const applicant = applicantName || (doc.applicant ?? '');
  return personName || entity || applicant || doc.generalEntitySideLabel || `مستند #${doc.id}`;
}

/** المبالغ المحصَّلة (حتى ثلاثة بعملاتها) مجتمعةً. */
export function formatCollectedAmounts(doc: DocumentResponse): string {
  return combineAmountSlots([
    [doc.collectedAmount, doc.collectedCurrency],
    [doc.collectedAmount2, doc.collectedCurrency2],
    [doc.collectedAmount3, doc.collectedCurrency3],
  ]);
}

/** أحدث وقعة «استرداد» على الملف (تفاصيل F2): سبب الاسترداد + كتاب براءة الذمة أو تحويل البدل + رقم أساس المنيب. */
function latestRecovery(doc: DocumentResponse): {
  recoveryReason?: string;
  baraetNumber?: string;
  baraetDate?: string;
  forcibleTransferDate?: string;
  forcibleTransferNoticeNumber?: string;
  sourceFileNumber?: string;
} | undefined {
  const recovered = (doc.occurrences ?? []).filter((o) => o.occurrenceType === 'recovered');
  const last = recovered[recovered.length - 1];
  return last ? (last.details ?? {}) : undefined;
}

export function buildStatusSummary(doc: DocumentResponse, context?: { sourceLabel?: string | null }): string {
  // بند و6 (L5): ملخص حالة الملف المناب (عند وجود الإنابة) يقدّم على بقية الفروع؛ «تريث» يُقرأ من
  // كتب المناب الموروثة من المنيب، و«مسترد» يُقرأ سببُه من وقعة «استرداد» الأخيرة (تفاصيل F2).
  // أية حالة أخرى (كصفوف التسوية/الجبريا القديمة المسجلة يدويًا قبل الخطة) تسقط على القديم دفاعًا.
  const sourceLabel = context?.sourceLabel?.trim();
  if (doc.sourceDelegationId != null) {
    if (doc.execStatus === 'تريث') {
      return [
        'تريث تبعًا للملف المنيب',
        sourceLabel ? `(${sourceLabel})` : '',
        'بموجب كتاب التريث رقم',
        doc.tarithNumber?.trim(),
        'بتاريخ',
        doc.tarithDate?.trim(),
      ]
        .filter(Boolean)
        .join(' ');
    }
    if (doc.execStatus === 'مسترد') {
      const recovery = latestRecovery(doc);
      if (recovery?.recoveryReason === 'منفذ بالتسوية') {
        return [
          'مسترد لاعتبار الملف المنيب',
          sourceLabel ? `(${sourceLabel})` : '',
          'منفذًا بالتسوية بكتاب براءة الذمة رقم',
          recovery.baraetNumber || recovery.baraetDate,
        ]
          .filter(Boolean)
          .join(' ');
      }
      return [
        'مسترد لاعتبار الملف المنيب',
        sourceLabel ? `(${sourceLabel})` : '',
        'منفذًا جبريًا كاملًا — بتحصيل المبلغ المطالب به جبريًا في الملف المنيب',
      ]
        .filter(Boolean)
        .join(' ');
    }
  }
  if (doc.execStatus === 'منفذ بالتسوية') {
    const parts = ['منفذ بموجب كتاب براءة الذمة'];
    if (doc.baraetNumber) parts.push(`رقم ${doc.baraetNumber}`);
    if (doc.baraetDate) parts.push(`تاريخ ${doc.baraetDate}`);
    const reg: string[] = [];
    if (doc.baraetRegNumber) reg.push(`برقم ${doc.baraetRegNumber}`);
    if (doc.baraetRegDate) reg.push(`تاريخ ${doc.baraetRegDate}`);
    if (reg.length) parts.push(`والمسجل ${reg.join(' ')}`);
    const collected = formatCollectedAmounts(doc);
    if (collected) parts.push(`المبلغ المحصل: ${collected}`);
    return parts.join(' ');
  }
  if (doc.execStatus === 'تريث') {
    const parts = ['تريث بموجب كتاب التريث'];
    if (doc.tarithNumber) parts.push(`رقم ${doc.tarithNumber}`);
    if (doc.tarithDate) parts.push(`تاريخ ${doc.tarithDate}`);
    const reg: string[] = [];
    if (doc.tarithRegNumber) reg.push(`برقم ${doc.tarithRegNumber}`);
    if (doc.tarithRegDate) reg.push(`تاريخ ${doc.tarithRegDate}`);
    if (reg.length) parts.push(`والمسجل ${reg.join(' ')}`);
    return parts.join(' ');
  }
  if (doc.execStatus === 'منفذ جبريا') {
    const parts = ['منفذ جبريا'];
    if (doc.execSubStatus) parts.push(`(${doc.execSubStatus})`);
    const collected = formatCollectedAmounts(doc);
    if (collected) parts.push(`المبلغ المحصل: ${collected}`);
    if (doc.forcibleTransferDate) parts.push(`تحويل البدل بتاريخ ${formatDate(doc.forcibleTransferDate)}`);
    if (doc.forcibleTransferNoticeNumber) parts.push(`بإشعار رقم ${doc.forcibleTransferNoticeNumber}`);
    return parts.join(' ');
  }
  if (doc.execStatus === 'محال الى البداية') {
    // ملخص «محال الى البداية»: كتاب المطالعة بعدم وجود أموال + كتاب الإحالة إن وُجد. من دخل من
    // «منفذ جبريا - منفذ جزئيا» تحمل جزئيته مقطعًا مرآة لفرع الجبريا (المحصل + التحويل).
    const parts = ['محال إلى قسم البداية لعدم وجود أموال للتنفيذ عليها'];
    if (doc.noFundsDemandNumber) parts.push(`بموجب كتاب المطالعة رقم ${doc.noFundsDemandNumber}`);
    if (doc.noFundsDemandDate) parts.push(`بتاريخ ${formatDate(doc.noFundsDemandDate)}`);
    if (doc.startReferralNumber || doc.startReferralDate) {
      parts.push('وبكتاب الإحالة');
      if (doc.startReferralNumber) parts.push(`رقم ${doc.startReferralNumber}`);
      if (doc.startReferralDate) parts.push(`بتاريخ ${formatDate(doc.startReferralDate)}`);
    }
    if (doc.execSubStatus === 'منفذ جزئيا') {
      parts.push('(منفذ جزئيا)');
      const collected = formatCollectedAmounts(doc);
      if (collected) parts.push(`المبلغ المحصل: ${collected}`);
      if (doc.forcibleTransferDate) parts.push(`تحويل البدل بتاريخ ${formatDate(doc.forcibleTransferDate)}`);
      if (doc.forcibleTransferNoticeNumber) parts.push(`بإشعار رقم ${doc.forcibleTransferNoticeNumber}`);
    }
    return parts.join(' ');
  }
  if (doc.execStatus === 'مشطوب') {
    const parts = ['مشطوب'];
    if (doc.struckOffDate) parts.push(`بتاريخ ${formatDate(doc.struckOffDate)}`);
    return parts.join(' ');
  }
  // عائلة وضع «منفذ عليه»/«عرض وايداع»: ملخص وصفي (متداول/منفذ/مشطوب) مع التاريخ عند وجوده،
  // بدل التسمية المجردة التي تُعيدها الدالة المساندة.
  if (isExecutedLike(doc.generalEntitySide)) {
    if (doc.executedStatus === 'مشطوب') {
      const parts = ['مشطوب'];
      if (doc.struckOffDate) parts.push(`بتاريخ ${formatDate(doc.struckOffDate)}`);
      return parts.join(' ');
    }
    if (doc.executedStatus === 'منفذ') {
      const parts = ['منفذ'];
      const executionDate =
        doc.generalEntitySide === 'deposit' ? doc.executedDepositDate : doc.executedExecutionDate;
      if (executionDate) parts.push(`بتاريخ ${formatDate(executionDate)}`);
      return parts.join(' ');
    }
    return 'متداول';
  }
  return getDocumentStatus(doc);
}

/**
 * السرد المختصر للوقعة داخل «بيانات الملف»/«وقوعات الملف»:
 * شطب/تجديد (وضع «منفذ عليه») أو إجراء تغيير حالة (نظام «طالبة تنفيذ»)
 * أو السرد النصي الحر لوقعة «تغيير جهة» الآلية (عبر DetailsText مع مرجع المرسوم).
 */
export function occurrenceLine(occurrence: DocumentOccurrenceDto): string {
  if (occurrence.occurrenceType === 'renewal') {
    const parts = ['وجُدِّد الملف برقم'];
    const number = occurrence.fileNumber?.trim();
    if (number) parts.push(number);
    const type = occurrence.fileType?.trim();
    if (type) parts.push(`نوع ${type}`);
    if (occurrence.year) parts.push(`لعام ${occurrence.year}`);
    if (occurrence.eventDate) parts.push(`بتاريخ ${formatDate(occurrence.eventDate)}`);
    if (occurrence.receiptNumber) parts.push(`(ورود اخطار رقم ${occurrence.receiptNumber})`);
    return parts.join(' ');
  }
  if (occurrence.occurrenceType === 'struck-off') {
    const parts = ['تم شطب الملف'];
    // الرقم المخزون هو هوية الملف الفعّالة وقت الشطب (المحلل المركزي): آخر رقم أساس ≤ سنة
    // الشطب وإلا رقم الملف الأصلي — فيظهر بعد الإصلاح، وفي سنة الشطب لا السنة الأصلية.
    const struckNumber = occurrence.fileNumber?.trim();
    if (struckNumber) parts.push(`رقم ${struckNumber}`);
    if (occurrence.year) parts.push(`لعام ${occurrence.year}`);
    if (occurrence.eventDate) parts.push(`بتاريخ ${formatDate(occurrence.eventDate)}`);
    return parts.join(' ');
  }

  // وقوعات تغيير الحالة (نظام «طالبة تنفيذ»): سرد مختصر بحقولها المسجّلة.
  const d = occurrence.details ?? {};
  switch (occurrence.occurrenceType) {
    case 'deferred':
      return ['تريث بموجب كتاب التريث', d.tarithNumber ? `رقم ${d.tarithNumber}` : '', d.tarithDate ? `بتاريخ ${d.tarithDate}` : ''].filter(Boolean).join(' ');
    case 'settled':
      return ['منفذ بالتسوية بموجب كتاب براءة الذمة', d.baraetNumber ? `رقم ${d.baraetNumber}` : '', d.baraetDate ? `بتاريخ ${d.baraetDate}` : ''].filter(Boolean).join(' ');
    case 'forcible': {
      const parts = ['منفذ جبريا'];
      if (d.execSubStatus) parts.push(`(${d.execSubStatus})`);
      if (d.forcedTransferDate) parts.push(`تحويل البدل بتاريخ ${formatDate(d.forcedTransferDate)}`);
      if (d.forcedTransferNoticeNumber) parts.push(`بإشعار رقم ${d.forcedTransferNoticeNumber}`);
      return parts.join(' ');
    }
    case 'referred-to-start':
      return ['محال الى البداية بموجب كتاب المطالعة بعدم وجود أموال', d.noFundsDemandNumber ? `رقم ${d.noFundsDemandNumber}` : '', d.noFundsDemandDate ? `بتاريخ ${d.noFundsDemandDate}` : ''].filter(Boolean).join(' ');
    case 'revert':
      // وقعة العودة من «محال الى البداية» (B4) تحمل سردًا نصيًا جاهزًا (revertNarration)؛
      // أما «تراجع» الكلاسيكي فيُقرأ من مفاتيح كتاب السير بالملف.
      if (d.revertNarration) return d.revertNarration;
      return ['تراجع عن الحالة بموجب كتاب السير بالملف', d.sayerNumber ? `رقم ${d.sayerNumber}` : '', d.sayerDate ? `بتاريخ ${d.sayerDate}` : ''].filter(Boolean).join(' ');
    case 'recovered':
      // وقعة استرداد المناب (L4): التسوية تُلحق بكتاب براءة الذمة، والاكتمال الجبري بسبب «تحصيل
      // المبلغ المطالب به جبريًا» في المنيب — بمصطلح المعتمَد (T5/L4).
      return d.recoveryReason === 'منفذ بالتسوية'
        ? ['استرداد الملف المناب لاعتبار الملف المنيب منفذًا', d.baraetNumber ? `ببراءة الذمة رقم ${d.baraetNumber}` : '', d.baraetDate ? `تاريخ ${d.baraetDate}` : ''].filter(Boolean).join(' ')
        : 'استرداد الملف المناب لاعتبار الملف المنيب منفذًا — بتحصيل المبلغ المطالب به جبريًا في الملف المنيب';
    case 'entity-change': {
      // الوقعة الآلية لتغيير الجهة: سرد نصي حر جاهز (مع مرجع المرسوم) يصل عبر DetailsText.
      const narrative = occurrence.detailsText?.trim();
      return narrative ? narrative : occurrence.occurrenceTypeLabel;
    }
    default:
      return occurrence.occurrenceTypeLabel;
  }
}

/** سرد كل وقوعات الملف بترتيبها الزمني (سطر لكل وقعة). */
export function buildOccurrenceLines(occurrences: DocumentOccurrenceDto[] | undefined): string[] {
  return (occurrences ?? []).map(occurrenceLine);
}

/** تسمية حقل عنوان الشخص الاعتباري: «وكيله القانوني»/«الموطن المختار»/«العنوان». */
export function legalAddressLabel(addressType: string | undefined): string {
  if (addressType === 'يمثله') return 'وكيله القانوني';
  if (addressType === 'موطن مختار') return 'الموطن المختار';
  return 'العنوان';
}

/** حقول الشخص الاعتباري (شركة/مؤسسة): الاسم ورقم التسجيل ومن يمثلها ومحافظتها وعنوانها. */
export function legalPartyRows(party: {
  name?: string;
  registrationNumber?: string;
  representedBy?: string;
  addressType?: string;
  address?: string;
  governorate?: string;
}): DetailsRow[] {
  const rows: DetailsRow[] = [];
  const name = (party.name ?? '').trim();
  if (name) rows.push({ label: 'الشخص الاعتباري', value: name });
  const reg = (party.registrationNumber ?? '').trim();
  if (reg) rows.push({ label: 'رقم التسجيل', value: reg });
  const rep = (party.representedBy ?? '').trim();
  if (rep) rows.push({ label: 'يمثلها', value: rep });
  const governorate = (party.governorate ?? '').trim();
  if (governorate) rows.push({ label: 'المحافظة', value: governorate });
  const address = (party.address ?? '').trim();
  if (address) rows.push({ label: legalAddressLabel(party.addressType), value: address });
  return rows;
}
