import {
  ASSET_KINDS,
  hasHeirName,
  hasStoredRepresentative,
  toExecutedHeirPayload,
  toHeirPayload,
  bankingCurrencyKeys,
  ordinaryCurrencyKeys,
  requiredCurrencyKeys,
} from '../../components/form/documentFormConstants';
import { slotDefaultCurrency } from '../../utils/amountCurrencies';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type {
  ApplicantPublicEntityDto,
  AssetDto,
  DocumentUpsertRequest,
  ExecutedNaturalPersonDto,
  ExecutedPublicEntityDto,
  ExecutionApplicantDto,
  GuarantorDto,
  HeirDto,
} from '../../types';

/** النموذج الفارغ (وإعادة الضبط) — الافتراضيات النظامية نفسها في الإنشاء والتعديل. */
export function createEmptyUpsertForm(currentYear: number, branchName: string): DocumentUpsertRequest {
  return {
    guarantors: [],
    assets: [],
    currency: 'ليرة سورية',
    currency2: 'دولار أمريكي',
    inclusionCurrency: 'ليرة سورية',
    contractTypeSelector: 'مصرفي',
    borrowerAddressType: 'موطن مختار',
    borrowerNature: 'natural',
    // سنة الملف الافتراضية للملف الجديد هي السنة الجارية (خادميًا عند توفرها) —
    // الماضي متاح من القائمة والمستقبل مستحيل واجهةً ومرفوض خلفيةً.
    fileYear: String(currentYear),
    // «فرع الملف» قيمة نظامية مشتقة من فرع المحامي المنشئ — لا يُكتب يدويًا.
    branchName,
    generalEntitySide: 'applicant',
    executedStatus: '',
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
    applicantPublicEntities: [],
    borrowerHeirs: [],
    annexes: [],
  };
}

export interface BuildDocumentPayloadInput {
  form: DocumentUpsertRequest;
  isEdit: boolean;
  isOrdinary: boolean;
  currentYear: number;
  guarantors: GuarantorDto[];
  borrowerHeirs: HeirDto[];
  assets: AssetDto[];
  executionApplicants: ExecutionApplicantDto[];
  executedPublicEntities: ExecutedPublicEntityDto[];
  executedNaturalPersons: ExecutedNaturalPersonDto[];
  applicantPublicEntities: ApplicantPublicEntityDto[];
}

/**
 * بناء حمولة `POST/PUT /documents` من حالة النموذج — دالة نقية بلا آثار جانبية
 * (منقولة حرفيًا من `DocumentForm.submit` مع تثبيت الحقول المشتقة والتصفير النظامي).
 */
export function buildDocumentPayload(input: BuildDocumentPayloadInput): DocumentUpsertRequest {
  const {
    form,
    isEdit,
    isOrdinary,
    currentYear,
    guarantors,
    borrowerHeirs,
    assets,
    executionApplicants,
    executedPublicEntities,
    executedNaturalPersons,
    applicantPublicEntities,
  } = input;
  const executedSubmit = form.generalEntitySide === 'executed' || form.generalEntitySide === 'deposit';

  // «الإجراءات التي تمت بنتيجة التنفيذ الفوري» أُزيلت من النموذج مؤقتًا (ستُنقل إلى
  // مسار التوليد لاحقًا)، فلا تُزرع كإجراء عند الحفظ. الملاحظات تُزرع عند الإنشاء فقط
  // (لا في التعديل) فلا تتكرر في «الإجراءات والملاحظات» عند كل حفظ.
  const initialActions = !isEdit && (form.notes ?? '').trim()
    ? [{ type: 'note' as const, text: (form.notes ?? '').trim() }]
    : [];
  // قرار التصفير يتبع المخزَّن فعليًا فقط: صف وريث فارغ الاسم الثلاثي
  // يُتجاهل في الحفظ (هنا وفي NormalizeHeirs خلفيًا) فلا يُصفَّر له العنوان.
  const borrowerHasHeirs = borrowerHeirs.some(hasHeirName);
  // قرار التصفير في الحمولة يتبع المخزَّن فعليًا (الاسم الثلاثي) لا الظاهر واجهةً:
  // صفة بلا اسم (تفعيل الزر وحده) لا يحفظها الخلفي، فتصفير العنوان لها فقدان صامت.
  const borrowerHasRep = hasStoredRepresentative({
    representativeName: form.borrowerRepresentativeName,
    representativeFather: form.borrowerRepresentativeFather,
    representativeFamily: form.borrowerRepresentativeFamily,
  });

  return {
    ...form,
    // سنة الإعادة لعائلة «منفذ عليها/عرض وايداع» مقررة كسنة الخادم الحالية فقط (حقلها مخفي):
    // تُرسل سنة الخادم صراحةً بدل أي سنةٍ مخزنة مع المستند ليتسق مع رفض الخلفية الدفاعي.
    ...(executedSubmit ? { renewalYear: currentYear } : {}),
    // تطبيع الأرقام العربية/الفارسية إلى ASCII في حقول التواريخ قبل الإرسال،
    // ليتسق المخزَّن مع ما يُعرض ولتقبله الخلفية في تحليلها (تحافظ على الحقول الأخرى كما هي).
    borrowerBirth: normalizeArabicDigits(form.borrowerBirth ?? '').trim(),
    contractDate: normalizeArabicDigits(form.contractDate ?? '').trim(),
    // الملاحق: الجدول الفرعي هو المصدر. الحقول المفردة القديمة تُرسل فارغة
    // (قراءة فقط خادميًا) فلا تُكتب فوق القيم القديمة.
    annexType: '',
    annexNumber: '',
    annexDate: '',
    annexes: (form.annexes ?? []).map((a) => ({
      sequence: a.sequence ?? 0,
      annexType: (a.annexType ?? '').trim(),
      annexNumber: (a.annexNumber ?? '').trim(),
      annexDate: normalizeArabicDigits(a.annexDate ?? '').trim(),
    })),
    fileIncomingDate: normalizeArabicDigits(form.fileIncomingDate ?? '').trim(),
    fileArrivalDate: normalizeArabicDigits(form.fileArrivalDate ?? '').trim(),
    fileRegistrationDate: normalizeArabicDigits(form.fileRegistrationDate ?? '').trim(),
    seizureDate: normalizeArabicDigits(form.seizureDate ?? '').trim(),
    // تاريخان من نوع DateTime? في الخلفية: السلسلة الفارغة تُعطّل فك JSON،
    // فلا تُرسل إلا القيمة غير الفارغة (وإلا يُحذف المفتاح فيؤول null على الخادم).
    ...((form.fileReceiptDate ?? '').trim()
      ? { fileReceiptDate: normalizeArabicDigits(form.fileReceiptDate ?? '').trim() }
      : {}),
    ...((form.struckOffDate ?? '').trim()
      ? { struckOffDate: normalizeArabicDigits(form.struckOffDate ?? '').trim() }
      : {}),
    ...((form.executedDepositDate ?? '').trim()
      ? { executedDepositDate: normalizeArabicDigits(form.executedDepositDate ?? '').trim() }
      : {}),
    ...((form.renewalFileReceiptDate ?? '').trim()
      ? { renewalFileReceiptDate: normalizeArabicDigits(form.renewalFileReceiptDate ?? '').trim() }
      : {}),
    ...((form.renewalDate ?? '').trim()
      ? { renewalDate: normalizeArabicDigits(form.renewalDate ?? '').trim() }
      : {}),
    // عملة المبالغ: «ليرة سورية» افتراضيًا للخانة الأولى، ولكل خانة لاحقة أول العملات
    // غير المستعملة في الخانات السابقة (لا تكرار للعملة) — للمبالغ المطلوب دفعها في وضع
    // «منفذ عليه» وللمصرفي/العادي في وضع «طالبة التنفيذ».
    executedRequiredCurrency: slotDefaultCurrency(form, requiredCurrencyKeys, 0),
    executedRequiredCurrency2: slotDefaultCurrency(form, requiredCurrencyKeys, 1),
    executedRequiredCurrency3: slotDefaultCurrency(form, requiredCurrencyKeys, 2),
    currency: slotDefaultCurrency(form, bankingCurrencyKeys, 0),
    currency2: slotDefaultCurrency(form, bankingCurrencyKeys, 1),
    currency3: slotDefaultCurrency(form, bankingCurrencyKeys, 2),
    inclusionCurrency: slotDefaultCurrency(form, ordinaryCurrencyKeys, 0),
    inclusionCurrency2: slotDefaultCurrency(form, ordinaryCurrencyKeys, 1),
    inclusionCurrency3: slotDefaultCurrency(form, ordinaryCurrencyKeys, 2),
    // وضع «منفذ عليه»: عادي فقط، بلا مقترض/كفلاء/أموال (مطابق لتحقق الخلفية).
    contractTypeSelector: executedSubmit ? 'عادي' : (form.contractTypeSelector ?? 'مصرفي'),
    // عنوان المقترض يُصفَّر عند وجود وريث أو ممثل شرعي (الوريث/الممثل هو الحامل الفعلي للعنوان).
    borrowerAddressType: executedSubmit || borrowerHasHeirs || borrowerHasRep ? '' : form.borrowerAddressType,
    borrowerAddress: executedSubmit || borrowerHasHeirs || borrowerHasRep ? '' : form.borrowerAddress,
    guarantors: executedSubmit
      ? []
      : guarantors
        .filter((g) => g.name?.trim())
        .map((g, i) => {
          // كالسابق: المخزَّن فعليًا فقط يُصفّر العناوين (انظر borrowerHasRep أعلاه).
          const gHasRep = hasStoredRepresentative(g);
          // كالمعامل أعلاه: الفارغ من الاسم الثلاثي ليس وريثًا مخزَّنًا.
          const gHasHeirs = (g.heirs ?? []).some(hasHeirName);
          return {
            ...g,
            // رقم الكفيل من الصف نفسه (القيم المخزنة المستقرة — قرار 10) لا من الموضع؛
            // الوضع العادي «المنفذ عليهم الآخرون» يواصل ترقيما متصلًا بعد المقترض كالسابق.
            guarantorNumber: isOrdinary ? i + 1 : (g.guarantorNumber ?? i + 1),
            addressType: (gHasHeirs || gHasRep) ? '' : g.addressType,
            address: (gHasHeirs || gHasRep) ? '' : g.address,
            heirs: (g.heirs ?? []).filter(hasHeirName).map(toHeirPayload),
          };
        }),
    borrowerHeirs: executedSubmit
      ? []
      : borrowerHeirs.filter(hasHeirName).map(toHeirPayload),
    assets: executedSubmit
      ? []
      : assets
        .filter((a) => {
          const kind = a.assetKind;
          if (kind === ASSET_KINDS.realEstate) {
            return a.propertyNumber?.trim() || a.caseNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
          }
          if (kind === ASSET_KINDS.vehicle) {
            return a.plateNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
          }
          if (kind === ASSET_KINDS.shop) {
            return a.registerNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
          }
          if (kind === ASSET_KINDS.salaryGuarantee) return (a.owners ?? []).some((o) => (o ?? '').trim());
          if (kind === ASSET_KINDS.unregisteredShop) {
            return a.licenseNumber?.trim() || (a.owners ?? []).some((o) => (o ?? '').trim());
          }
          return (a.owners ?? []).some((o) => (o ?? '').trim());
        })
        .map((a) => ({
          ...a,
          property: `${a.propertyNumber ?? ''} ${a.propertyDistrict ?? ''}`.trim(),
          // تطبيع الأرقام العربية/الفارسية في تاريخَي المتجر قبل الإرسال (تتقبلها الخلفية كتواريخ حرة).
          registrationDate: normalizeArabicDigits(a.registrationDate ?? '').trim(),
          licenseDate: normalizeArabicDigits(a.licenseDate ?? '').trim(),
          seizureDate: normalizeArabicDigits(a.seizureDate ?? '').trim(),
        })),
    executionApplicants: executedSubmit
      ? executionApplicants
        .filter((a) => (a.name ?? '').trim())
        .map((a) => ({
          ...a,
          // الوكيل القانوني يُمسح فقط لممثل مخزَّن فعليًا (تعارضهما حصري) —
          // صفة بلا اسم لا تُحفظ فمسح النص لها فقدان صامت.
          legalRepresentative: hasStoredRepresentative(a) ? '' : a.legalRepresentative,
          heirs: (a.heirs ?? [])
            .filter((h) => (h.heirName ?? '').trim())
            .map(toExecutedHeirPayload),
        }))
      : [],
    executedPublicEntities: executedSubmit
      ? executedPublicEntities.filter((e) => (e.entityName ?? '').trim())
      : [],
    executedNaturalPersons: executedSubmit
      ? executedNaturalPersons
        .filter((p) => (p.name ?? '').trim())
        .map((p) => ({
          ...p,
          heirs: (p.heirs ?? [])
            .filter((h) => (h.heirName ?? '').trim())
            .map(toExecutedHeirPayload),
        }))
      : [],
    applicantPublicEntities: executedSubmit
      ? []
      : applicantPublicEntities.filter((a) => (a.name ?? '').trim()),
    ...(initialActions.length > 0 ? { initialActions } : {}),
  };
}
