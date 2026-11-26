import { useCallback, useMemo, useRef } from 'react';
import { useNavigate, useParams, Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import NumberReuseWarningModal from '../components/NumberReuseWarningModal';
import { useAuth } from '../auth/useAuth';
import { useCurrentYear } from '../hooks/useCurrentYear';
import { ApplicantSideSections } from '../components/form/ApplicantSideSections';
import { ExecutedSideSections } from '../components/form/ExecutedSideSections';
import { makeFieldHelpers } from '../components/form/formFields';
import { FormSectionTitle } from '../components/form/FormSectionTitle';
import { InterventionModal } from '../components/form/InterventionModal';
import { DocumentInterventionsList } from '../components/form/DocumentInterventionsList';
import FloatingFormAlert from '../components/FloatingFormAlert';
import { PublicEntityPickerModal } from '../components/entity/PublicEntityPickerModal';
import CircuitPickerModal from '../components/circuit/CircuitPickerModal';
import type { ExecutionCircuitDto, PublicEntityEntryDto } from '../types';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import { tripleName, isExecutedLike } from '../utils/documentDisplay';
import { governorateFromBranch } from '../utils/governorate';
import { DocumentBasicsSection } from './documentForm/DocumentBasicsSection';
import { useDocumentParties } from './documentForm/useDocumentParties';
import { useDocumentReferences } from './documentForm/useDocumentReferences';
import { useDocumentFormState } from './documentForm/useDocumentFormState';
import { useDocumentSubmit } from './documentForm/useDocumentSubmit';

/**
 * نموذج إدخال/تعديل المستند — قشرة تركيبية فقط بعد `P6-02a`:
 * حالة الأطراف (`useDocumentParties`)، البيانات المرجعية والارتباطات
 * (`useDocumentReferences`)، حالة النموذج والتحميل (`useDocumentFormState`)،
 * الإرسال والتحقق (`useDocumentSubmit` + وحدات نقية)، والقسم الأساسي
 * (`DocumentBasicsSection`). السلوك منقول بلا تغيير.
 */
export default function DocumentForm() {
  const { id } = useParams();
  const isEdit = Boolean(id);
  const navigate = useNavigate();
  const { user } = useAuth();
  // سنة «قرار السنة» المعتمدة من الخادم — لا من عداد المتصفح.
  const { currentYear } = useCurrentYear();
  // «المحافظة» للجهات العامة تُملأ تلقائيًا من فرع المحامي (دمشق/حلب...) وقابلة للتعديل،
  // فقد تكون الجهة تابعة لمحافظة أخرى. تُحفظ كمرجع ثابت لاستخدامها في معالجات الأحداث.
  const defaultGovernorateRef = useRef(governorateFromBranch(user?.branchName));

  const parties = useDocumentParties(defaultGovernorateRef.current);
  const references = useDocumentReferences({
    id,
    isEdit,
    assets: parties.assets,
    isMirror: parties.isMirror,
  });
  const state = useDocumentFormState({
    id,
    isEdit,
    currentYear,
    branchName: user?.branchName ?? '',
    parties,
  });
  const { submit } = useDocumentSubmit({
    id,
    isEdit,
    currentYear,
    navigate,
    state,
    parties,
    references,
  });

  const { form, set } = state;

  // قفل أمامي مريح لقاعدة «التاريخ يُعدَّل ولا يُحذف على مالٍ عليه إنابة سارية»:
  // محاولة التفريغ تُتجاهل وتُبقى القيمة السابقة — وأي تجاوز يرفضه الخلفي.
  const setEstate = (i: number, key: keyof (typeof parties.assets)[number], value: string) => {
    if (key === 'seizureDate' && references.delegationsKnown) {
      const current = parties.assets[i];
      if (
        current?.id != null &&
        references.referencedAssetIds.has(current.id) &&
        !normalizeArabicDigits(value).trim()
      ) {
        return;
      }
    }
    parties.setAssetField(i, key, value);
  };

  const { field, selectField } = makeFieldHelpers(form, set);
  // حقول «المعلومات الأساسية» الذاتية (الدائرة + كُتب ورود الملف/كتاب الجهة/تحت رفع/الحجز)
  // مقفلة في الملف المناب حيث تجيء من المنيب (قرار 7/§5.7 — F3) عبر مساعدٍ للقراءة فقط؛
  // الهوية الثلاثية (رقم/سنة/نوع الملف وتاريخ قيده) تبقى قابلة للتحرير (قرار 5).
  const { field: lockField } = makeFieldHelpers(form, set, parties.isMirror);

  const isOrdinary = form.contractTypeSelector === 'عادي';
  const isExecuted = isExecutedLike(form.generalEntitySide);
  // المتدخلون متاحون لصفتَي «طالبة التنفيذ» و«منفذ عليها» فقط (لا «عرض وايداع»)،
  // للمحامي المالك في وضع التعديل — في أي حالة للملف غير المحذوف.
  const canManageInterventions =
    isEdit
    && user?.role === 'lawyer'
    && (form.generalEntitySide === 'applicant' || form.generalEntitySide === 'executed');
  const executedSide = form.generalEntitySide === 'deposit' ? 'deposit' : 'executed';
  const sideLabel = form.generalEntitySide === 'deposit'
    ? 'عرض وايداع'
    : form.generalEntitySide === 'executed'
      ? 'الجهة العامة منفذ عليها'
      : 'الجهة العامة طالبة التنفيذ';
  const guarantorLabel = isOrdinary ? 'منفذ عليه' : 'كفيل';

  // قائمة الملاك المرشحة (حتى 50 طرفًا + ورثتهم): تُحسب مرة واحدة لكل تغيير أطراف
  // بدل إعادة البناء مع كل أصل عند كل ضغطة — الاسم الثلاثي يطابق المخزَّن والمعروض.
  const ownerOptionsList = useMemo(() => {
    const opts: string[] = [];
    // الاسم الثلاثي الكامل للمقترض والكفيل يطابق ما يحفظه الخلفية ويعرضه العرض،
    // وبه تتطابق مطابقة الورثة (ورثة المتوفى) في توليد 005/006.
    const borrowerFull = tripleName(form.borrowerName, form.borrowerFather, form.borrowerFamily);
    if (borrowerFull) opts.push(borrowerFull);
    parties.borrowerHeirs.forEach((h) => {
      const full = tripleName(h.name, h.father, h.family);
      if (full && !opts.includes(full)) opts.push(full);
    });
    parties.guarantors.forEach((g) => {
      const full = tripleName(g.name, g.father, g.family);
      if (full && !opts.includes(full)) opts.push(full);
      (g.heirs ?? []).forEach((h) => {
        const fullHeir = tripleName(h.name, h.father, h.family);
        if (fullHeir && !opts.includes(fullHeir)) opts.push(fullHeir);
      });
    });
    return opts;
  }, [form.borrowerName, form.borrowerFather, form.borrowerFamily, parties.borrowerHeirs, parties.guarantors]);

  const ownerOptions = useCallback(() => ownerOptionsList, [ownerOptionsList]);

  const debtorFullName = tripleName(form.borrowerName, form.borrowerFather, form.borrowerFamily);

  const applyRegistryPick = (entry: PublicEntityEntryDto) => {
    if (!references.registryPicker) return;
    const { side, index } = references.registryPicker;
    if (side === 'applicant') parties.linkApplicantEntity(index, entry);
    else parties.linkExecutedEntity(index, entry);
    references.setRegistryPicker(null);
  };

  // الحذف متاح في وضع التعديل للمحامي فقط (نفس قيد صفحة العرض)، ويُعزل في نهاية الصفحة
  // بعد «تغيير الحالة» كمنطقة خطر، مع تأكيد صريح ومنع الإرسال المزدوج.
  const canDelete = isEdit && user?.role === 'lawyer';

  const deleteDoc = async () => {
    if (!isEdit || !id) return;
    if (!window.confirm('هل أنت متأكد من حذف هذا المستند؟')) return;
    state.setDeleteBusy(true);
    state.setError('');
    try {
      await api.delete(`/documents/${id}`);
      navigate('/documents');
    } catch (err) {
      // صدق الخطأ: تُعرض رسالة الخلفية (كمنع الحذف لوجود إنابة/استئناف) بدل إخفائها.
      state.setError(getApiErrorMessage(err));
      state.setDeleteBusy(false);
    }
  };

  // بوابة تحميل وضع التعديل: لا يُعرض النموذج ولا يُتاح الحفظ قبل اكتمال
  // جلب الملف الحالي — فلا يرى المستخدم مال ملف سابق ولا يكتبه في الجديد.
  if (isEdit && state.loading) {
    return (
      <div className="max-w-6xl mx-auto">
        <div className="flex items-center justify-center text-gray-500 py-16">جارِ تحميل بيانات الملف...</div>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-gray-800">
          {isEdit
            ? `تعديل ملف «${debtorFullName || `#${id}`}»`
            : 'إدخال ملف جديد'}
        </h2>
        <Link to="/documents" className="inline-flex items-center min-h-11 text-emerald-800 text-sm hover:underline">
          ← عودة للقائمة
        </Link>
      </div>

      {state.error && <FloatingFormAlert message={state.error} onDismiss={() => state.setError('')} />}

      <form onSubmit={submit} noValidate className="bg-white rounded-xl shadow p-6">
        <FormSectionTitle title="⚖️ صفة الجهة العامة" />
        <div className="rounded-lg bg-gray-50 border border-gray-200 p-4 mb-2">
          {isEdit ? (
            <div className="flex flex-wrap items-start justify-between gap-3">
              <p className="text-sm text-gray-700 min-w-0">
                صفة الجهة العامة:{' '}
                <span className="font-bold">{sideLabel}</span>
                <span className="block text-xs text-gray-500 mt-1">
                  لا يمكن تغيير صفة الجهة العامة بعد إنشائها
                </span>
              </p>
              {canManageInterventions && (
                <button
                  type="button"
                  onClick={state.openAddIntervention}
                  className="ms-auto shrink-0 bg-emerald-800 hover:bg-emerald-700 text-white text-xs font-bold rounded-lg px-4 py-2 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  ＋ إضافة متدخل
                </button>
              )}
            </div>
          ) : (
            <div role="radiogroup" aria-label="صفة الجهة العامة" className="flex flex-wrap gap-x-6 gap-y-2">
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={form.generalEntitySide === 'applicant'}
                  onChange={() => state.setSide('applicant')}
                  className="h-4 w-4 text-emerald-600"
                />
                الجهة العامة طالبة التنفيذ
              </label>
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={form.generalEntitySide === 'executed'}
                  onChange={() => state.setSide('executed')}
                  className="h-4 w-4 text-emerald-600"
                />
                الجهة العامة منفذ عليها
              </label>
              <label className="flex items-center gap-2 min-h-11 cursor-pointer text-sm text-gray-700">
                <input
                  type="radio"
                  name="generalEntitySide"
                  checked={form.generalEntitySide === 'deposit'}
                  onChange={() => state.setSide('deposit')}
                  className="h-4 w-4 text-emerald-600"
                />
                عرض وايداع
              </label>
            </div>
          )}
          {canManageInterventions && (
            <DocumentInterventionsList
              documentId={Number(id)}
              interventions={state.interventions}
              onChanged={state.setInterventions}
              onEdit={state.openEditIntervention}
            />
          )}
        </div>

        <DocumentBasicsSection
          form={form}
          set={set}
          isEdit={isEdit}
          isExecuted={isExecuted}
          isMirror={parties.isMirror}
          currentYear={currentYear}
          field={field}
          lockField={lockField}
          selectField={selectField}
          applicantPublicEntities={parties.applicantPublicEntities}
          onApplicantRemove={parties.removeApplicantPublicEntity}
          onApplicantAdd={parties.addApplicantPublicEntity}
          onApplicantPickRegistry={(i) => references.setRegistryPicker({ side: 'applicant', index: i })}
          onApplicantUnlink={parties.unlinkApplicantPublicEntity}
          circuitPickerButtonRef={references.circuitPickerButtonRef}
          onOpenCircuitPicker={() => references.setCircuitPickerOpen(true)}
        />

        {isExecuted ? (
          <ExecutedSideSections
            side={executedSide}
            form={form}
            set={set}
            isMirror={parties.isMirror}
            partyRepLocked={parties.partyRepLocked}
            showRequiredAmount={state.showRequiredAmount}
            setShowRequiredAmount={state.setShowRequiredAmount}
            requiredAmountSlots={state.requiredAmountSlots}
            setRequiredAmountSlots={state.setRequiredAmountSlots}
            executionApplicants={parties.executionApplicants}
            onApplicantSet={parties.setApplicant}
            onApplicantAdd={parties.addApplicant}
            onApplicantRemove={parties.removeApplicant}
            onApplicantHeirSet={parties.setApplicantHeir}
            onApplicantHeirAdd={parties.addApplicantHeir}
            onApplicantHeirRemove={parties.removeApplicantHeir}
            onApplicantRepActivate={parties.activateApplicantRep}
            onApplicantRepRemove={parties.removeApplicantRep}
            executedPublicEntities={parties.executedPublicEntities}
            onEntitySet={parties.setExecutedEntity}
            onEntityAdd={parties.addExecutedEntity}
            onEntityRemove={parties.removeExecutedEntity}
            onPickRegistry={(i) => references.setRegistryPicker({ side: 'executed', index: i })}
            onEntityUnlink={parties.unlinkExecutedEntity}
            executedNaturalPersons={parties.executedNaturalPersons}
            onPersonSet={parties.setExecutedPerson}
            onPersonAdd={parties.addExecutedPerson}
            onPersonRemove={parties.removeExecutedPerson}
            onPersonHeirSet={parties.setExecutedPersonHeir}
            onPersonHeirAdd={parties.addExecutedPersonHeir}
            onPersonHeirRemove={parties.removeExecutedPersonHeir}
            onPersonRepActivate={parties.activatePersonRep}
            onPersonRepRemove={parties.removePersonRep}
            paidAmountSlots={state.paidAmountSlots}
            setPaidAmountSlots={state.setPaidAmountSlots}
            wasOriginallyStruckOff={state.wasOriginallyStruckOff}
            currentExecutedStatus={isEdit ? state.originalExecutedStatus : undefined}
          />
        ) : (
          <ApplicantSideSections
            form={form}
            set={set}
            isOrdinary={isOrdinary}
            isMirror={parties.isMirror}
            partyRepLocked={parties.partyRepLocked}
            guarantorLabel={guarantorLabel}
            showInclusionAmount={state.showInclusionAmount}
            setShowInclusionAmount={state.setShowInclusionAmount}
            ordinaryAmountSlots={state.ordinaryAmountSlots}
            setOrdinaryAmountSlots={state.setOrdinaryAmountSlots}
            bankingAmountSlots={state.bankingAmountSlots}
            setBankingAmountSlots={state.setBankingAmountSlots}
            borrowerHeirs={parties.borrowerHeirs}
            onBorrowerHeirSet={parties.setBorrowerHeir}
            onBorrowerHeirAdd={parties.addBorrowerHeir}
            onBorrowerHeirRemove={parties.removeBorrowerHeir}
            guarantors={parties.guarantors}
            onGuarantorSet={parties.setGuarantor}
            onGuarantorHeirSet={parties.setGHeir}
            onGuarantorHeirAdd={parties.addGHeir}
            onGuarantorHeirRemove={parties.removeGHeir}
            onGuarantorAdd={(nature) => parties.addGuarantor(isOrdinary, nature)}
            onGuarantorRemove={parties.removeGuarantor}
            onBorrowerRepActivate={state.activateBorrowerRep}
            onBorrowerRepRemove={state.removeBorrowerRep}
            onGuarantorRepActivate={parties.activateGuarantorRep}
            onGuarantorRepRemove={parties.removeGuarantorRep}
            assets={parties.assets}
            onEstateSet={setEstate}
            seizureLockedIds={references.referencedAssetIds}
            onEstateRemove={parties.removeEstate}
            onOwnerToggle={parties.toggleOwner}
            onSingleOwnerSet={parties.setSingleOwner}
            onEstateAdd={parties.addEstate}
            ownerOptions={ownerOptions}
          />
        )}

        {/* حقل «الإجراءات التي تمت بنتيجة التنفيذ الفوري» أُزيل من النموذج مؤقتًا؛
            ستُعاد إضافته في مسار توليد المستندات لاحقًا. تبقى قيمته محفوظة في الحالة
            وتُرسل كما هي عند التعديل، فلا تُفقد بيانات الملفات القائمة. */}

        {!isEdit && (
          <>
            <FormSectionTitle title="📝 الملاحظات" />
            <textarea
              value={form.notes ?? ''}
              onChange={(e) => set('notes', e.target.value)}
              rows={3}
              aria-label="الملاحظات"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </>
        )}

        <div className="mt-8 flex gap-3">
          <button type="submit" disabled={state.busy || state.loading} className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white font-bold rounded-lg px-6 py-2.5 transition-colors min-h-11">
            {state.busy ? 'جارِ الحفظ...' : isEdit ? 'حفظ التعديلات' : '💾 حفظ'}
          </button>
          {!isEdit && (
            <button type="button" onClick={state.resetForm} className="bg-red-700 hover:bg-red-600 text-white font-bold rounded-lg px-6 py-2.5 transition-colors min-h-11">
              🗑️ إعادة تعيين
            </button>
          )}
          <Link to="/documents" className="border border-gray-300 rounded-lg px-6 py-2.5 text-gray-700 hover:bg-gray-50 min-h-11 inline-flex items-center">
            إلغاء
          </Link>
        </div>

        {canDelete && (
          <div className="mt-8 border-t border-gray-200 pt-6">
            <button
              type="button"
              onClick={deleteDoc}
              disabled={state.deleteBusy || references.hasLinkedMatter}
              aria-describedby={references.hasLinkedMatter ? 'delete-blocked-note' : undefined}
              className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {state.deleteBusy ? 'جارِ الحذف...' : '🗑️ حذف الملف'}
            </button>
            {references.hasLinkedMatter && (
              <p id="delete-blocked-note" role="note" className="mt-2 text-sm text-gray-600">
                الملف مرتبط بإنابة/استئناف — الحذف ممنوع
              </p>
            )}
          </div>
        )}
      </form>

      {references.registryPicker && (
        <PublicEntityPickerModal
          onClose={() => references.setRegistryPicker(null)}
          onPick={applyRegistryPick}
        />
      )}

      {references.circuitPickerOpen && (
        <CircuitPickerModal
          fetchUrl="/execution-circuits/for-lawyer"
          initialSelectedId={form.executionCircuitId ?? null}
          onSelect={(c: ExecutionCircuitDto) => {
            set('executionCircuitId', c.id);
            set('court', c.name);
            references.setCircuitPickerOpen(false);
          }}
          onClose={() => references.setCircuitPickerOpen(false)}
        />
      )}

      {state.interventionModalOpen && id !== undefined && (
        <InterventionModal
          documentId={Number(id)}
          initial={state.editingIntervention}
          onSaved={state.applyInterventionSaved}
          onClose={() => {
            state.setInterventionModalOpen(false);
            state.setEditingIntervention(null);
          }}
        />
      )}

      {state.numberWarning && (
        <NumberReuseWarningModal
          message={state.numberWarning.message}
          busy={state.busy}
          onCancel={() => state.setNumberWarning(null)}
          onConfirm={() => {
            const token = state.numberWarning?.warningToken;
            state.setNumberWarning(null);
            void submit(undefined, token);
          }}
        />
      )}
    </div>
  );
}
