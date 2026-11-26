import type { BranchActionPreviewResponse, BranchPreviewEntryDto } from '../../../types';
import { branchCountFormatter as fCount } from './branchManagementFormat';
import type { BranchManagementModel } from './useBranchManagement';

/** لوحة نتائج المعاينة الموحّدة — تُعرض بعد طلب المعاينة وقبل زر التنفيذ. */
export function BranchPreviewPanel({ preview }: { preview: BranchActionPreviewResponse }) {
  const hasErrors = (preview.errors ?? []).length > 0;
  return (
    <div className="mt-3 bg-white border border-gray-200 rounded-lg p-3 text-sm" role="status">
      {hasErrors && (
        <p role="alert" className="text-red-700 bg-red-50 border border-red-100 rounded-lg p-2 mb-2">
          {(preview.errors ?? []).join(' — ')}
        </p>
      )}
      {preview.summary && (
        <p className="text-gray-700 break-words">{preview.summary}</p>
      )}
      {(preview.warnings ?? []).length > 0 && (
        <ul className="mt-2 space-y-1">
          {(preview.warnings ?? []).map((w) => (
            <li key={w} className="text-amber-700 text-xs bg-amber-50 rounded-lg px-2 py-1 break-words">
              {w}
            </li>
          ))}
        </ul>
      )}
      {(preview.entries ?? []).length > 0 && (
        <ul className="mt-2 space-y-1">
          {(preview.entries ?? []).map((e: BranchPreviewEntryDto) => (
            <li key={e.entryId} className="flex flex-wrap items-center justify-between gap-1 text-xs text-gray-600">
              <span className="min-w-0 break-words">{e.branchName} — {e.governorate}</span>
              <span className="tabular-nums whitespace-nowrap">{fCount.format(e.documentCount)} ملف</span>
            </li>
          ))}
        </ul>
      )}
      <p className="mt-2 font-medium text-gray-800 tabular-nums">
        إجمالي الملفات المتأثرة: {fCount.format(preview.totalAffectedDocuments)}
      </p>
    </div>
  );
}

/** تعديل تسمية فرع — معاينة ثم حفظ مع إقفال المراجعة. */
export function RenameBranchPanel({ m }: { m: BranchManagementModel }) {
  if (m.editingId === null) return null;
  return (
    <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 mb-4">
      <h4 className="font-bold text-sm text-gray-800 mb-1">تعديل تسمية الفرع</h4>
      <p className="text-xs text-gray-500 mb-3">تُحدَّث التسمية في كل الملفات المرتبطة (النشطة والمشطوبة والتريث والاستئنافات) فورًا.</p>
      <label htmlFor="bm-branch" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع الجديد</label>
      <input
        id="bm-branch"
        value={m.editBranch}
        onChange={(e) => { m.setEditBranch(e.target.value); m.clearPreview(); }}
        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
      />
      <label className="inline-flex items-center gap-2 text-sm cursor-pointer mt-3 min-h-11">
        <input
          type="checkbox"
          checked={m.editShowCoverage}
          onChange={(ev) => { m.setEditShowCoverage(ev.target.checked); if (!ev.target.checked) m.setEditCoverage(''); m.clearPreview(); }}
          className="h-4 w-4"
        />
        تغطية أكثر من محافظة
      </label>
      {m.editShowCoverage && (
        <input
          value={m.editCoverage}
          onChange={(e) => { m.setEditCoverage(e.target.value); m.clearPreview(); }}
          placeholder="مثال: دمشق وريفها"
          maxLength={150}
          aria-label="تسمية التغطية"
          className="mt-2 w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      )}
      <div className="flex flex-wrap gap-2 mt-3">
        <button
          onClick={m.renamePreview}
          disabled={m.busy || m.previewing}
          className="border border-emerald-300 text-emerald-800 hover:bg-emerald-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
        >
          {m.previewing && m.previewFor === 'rename' ? 'جارِ المعاينة…' : 'معاينة'}
        </button>
        {m.canCommit && m.previewFor === 'rename' && (
          <button
            onClick={m.renameCommit}
            disabled={m.busy}
            className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {m.busy ? 'جارِ الحفظ…' : 'حفظ التعديل وإقفال المراجعة'}
          </button>
        )}
        <button
          onClick={() => { m.setEditingId(null); m.clearPreview(); }}
          className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
        >
          إلغاء
        </button>
      </div>
      {m.preview && m.previewFor === 'rename' && <BranchPreviewPanel preview={m.preview} />}
    </div>
  );
}

/** دمج فرعين داخل الجهة — معاينة ثم تنفيذ. */
export function MergeBranchesPanel({ m }: { m: BranchManagementModel }) {
  return (
    <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 mb-4">
      <h4 className="font-bold text-sm text-gray-800 mb-2">دمج فرعين</h4>
      <p className="text-xs text-gray-500 mb-3">انقل روابط فرع مصدر إلى فرع هدف ضمن نفس الجهة — يُلغى المصدر.</p>
      <div className="grid sm:grid-cols-2 gap-3">
        <div>
          <label htmlFor="bm-src" className="block text-xs font-medium text-gray-600 mb-1">الفرع المصدر (سيُلغى)</label>
          <select
            id="bm-src"
            value={m.mergeSource}
            onChange={(e) => { m.setMergeSource(e.target.value ? Number(e.target.value) : ''); m.clearPreview(); }}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-500"
          >
            <option value="">اختر المصدر…</option>
            {m.activeEntries.map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="bm-tgt" className="block text-xs font-medium text-gray-600 mb-1">الفرع الهدف (يبقى)</label>
          <select
            id="bm-tgt"
            value={m.mergeTarget}
            onChange={(e) => { m.setMergeTarget(e.target.value ? Number(e.target.value) : ''); m.clearPreview(); }}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-500"
          >
            <option value="">اختر الهدف…</option>
            {m.activeEntries.filter((e) => e.id !== m.mergeSource).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
          </select>
        </div>
      </div>
      <div className="flex flex-wrap gap-2 mt-3">
        <button
          onClick={() => {
            if (m.mergeSource === '' || m.mergeTarget === '' || m.mergeSource === m.mergeTarget) {
              m.setError('اختر فرعًا مصدرًا وفرعًا هدفًا مختلفين');
              return;
            }
            void m.runPreview('merge', { action: 'merge', entryId: m.mergeSource, targetId: m.mergeTarget });
          }}
          disabled={m.busy || m.previewing || m.mergeSource === '' || m.mergeTarget === ''}
          className="border border-amber-300 text-amber-800 hover:bg-amber-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
        >
          {m.previewing && m.previewFor === 'merge' ? 'جارِ المعاينة…' : 'معاينة الدمج'}
        </button>
        {m.canCommit && m.previewFor === 'merge' && (
          <button
            onClick={m.mergeCommit}
            disabled={m.busy}
            className="bg-amber-700 hover:bg-amber-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
          >
            {m.busy ? 'جارِ الدمج…' : 'تنفيذ الدمج'}
          </button>
        )}
      </div>
      {m.preview && m.previewFor === 'merge' && <BranchPreviewPanel preview={m.preview} />}
    </div>
  );
}

/** إلغاء فرع (مع نقل اختياري لملفاته إلى هدف = دمج ضمني). */
export function AbolishBranchPanel({ m }: { m: BranchManagementModel }) {
  if (m.abolishId === null) return null;
  return (
    <div className="bg-red-50 border border-red-200 rounded-xl p-4 mb-4">
      <h4 className="font-bold text-sm text-red-900 mb-2">إلغاء فرع</h4>
      <p className="text-sm text-red-800 mb-3">
        فرع به ملفات مرتبطة يتطلب اختيار فرع هدف يُنقل إليه (دمج ضمني) — الإلغاء المباشر مسموح لفرع بلا روابط فقط.
      </p>
      <label htmlFor="bm-abolish-target" className="block text-xs font-medium text-red-800 mb-1">
        فرع الهدف (اختياري — يُنقل إليه الملفات)
      </label>
      <select
        id="bm-abolish-target"
        value={m.abolishTarget}
        onChange={(e) => { m.setAbolishTarget(e.target.value ? Number(e.target.value) : ''); m.clearPreview(); }}
        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-red-500"
      >
        <option value="">بلا هدف (إلغاء مباشر)…</option>
        {m.activeEntries.filter((e) => e.id !== m.abolishId).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
      </select>
      <div className="flex flex-wrap gap-2 mt-3">
        <button
          onClick={() => {
            void m.runPreview('abolish', { action: 'abolish', entryId: m.abolishId as number, targetId: m.abolishTarget === '' ? null : m.abolishTarget });
          }}
          disabled={m.busy || m.previewing}
          className="border border-red-300 text-red-800 hover:bg-red-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
        >
          {m.previewing && m.previewFor === 'abolish' ? 'جارِ المعاينة…' : 'معاينة الإلغاء'}
        </button>
        {m.canCommit && m.previewFor === 'abolish' && (
          <button
            onClick={m.abolishCommit}
            disabled={m.busy}
            className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
          >
            {m.busy ? 'جارِ الإلغاء…' : 'تأكيد الإلغاء'}
          </button>
        )}
        <button
          onClick={() => { m.setAbolishId(null); m.setAbolishTarget(''); m.clearPreview(); }}
          className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
        >
          تراجع
        </button>
      </div>
      {m.preview && m.previewFor === 'abolish' && <BranchPreviewPanel preview={m.preview} />}
    </div>
  );
}

/** توحيد تسميات فروع في فرع ناجٍ واحد — معاينة وتأكيد كتابة الاسم. */
export function UnifyBranchesPanel({ m }: { m: BranchManagementModel }) {
  if (m.activeEntries.length < 2) return null;
  return (
    <div className="bg-sky-50 border border-sky-200 rounded-xl p-4">
      <h4 className="font-bold text-sm text-gray-800 mb-2">توحيد تسميات فروع</h4>
      <p className="text-xs text-gray-500 mb-3">
        اجمع عدة فروع في فرع ناجٍ واحد (اختياريًا مع تصحيح كتابة اسمه) في معاملة واحدة.
      </p>
      <div className="grid sm:grid-cols-2 gap-3">
        <div>
          <label htmlFor="bm-unify-target" className="block text-xs font-medium text-gray-600 mb-1">الفرع الناجي (يبقى)</label>
          <select
            id="bm-unify-target"
            value={m.unifyTarget}
            onChange={(e) => { m.setUnifyTarget(e.target.value ? Number(e.target.value) : ''); m.clearPreview(); }}
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-sky-500"
          >
            <option value="">اختر الناجي…</option>
            {m.activeEntries.map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="bm-unify-absorbed" className="block text-xs font-medium text-gray-600 mb-1">الفروع الممتصة (تُلغى)</label>
          <select
            id="bm-unify-absorbed"
            multiple
            value={m.unifyAbsorbed.map(String)}
            onChange={(e) => {
              const picked = Array.from(e.target.selectedOptions).map((o) => Number(o.value));
              m.setUnifyAbsorbed(picked);
              m.clearPreview();
            }}
            className="w-full min-h-32 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-sky-500"
          >
            {m.activeEntries.filter((e) => e.id !== m.unifyTarget).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
          </select>
          <p className="text-[11px] text-gray-400 mt-1">اضغط مع Ctrl لتحديد أكثر من فرع</p>
        </div>
      </div>
      <div className="mt-3">
        <label htmlFor="bm-unify-corrected" className="block text-xs font-medium text-gray-600 mb-1">تصحيح كتابة اسم الناجي (اختياري)</label>
        <input
          id="bm-unify-corrected"
          value={m.unifyCorrected}
          onChange={(e) => { m.setUnifyCorrected(e.target.value); m.setUnifyConfirmText(''); m.clearPreview(); }}
          placeholder={m.unifyTarget !== '' ? `اسم الناجي الحالي: ${m.entries.find((e) => e.id === m.unifyTarget)?.branchName ?? ''}` : 'اكتب الكتابة الصحيحة…'}
          autoComplete="off"
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
        />
      </div>
      {m.preview && m.previewFor === 'unify' && (
        <div className="mt-3">
          <label htmlFor="bm-unify-confirm" className="block text-xs font-medium text-gray-600 mb-1">
            اكتب اسم الناجي بالضبط لتأكيد التوحيد: «{m.unifySurvivorName()}»
          </label>
          <input
            id="bm-unify-confirm"
            value={m.unifyConfirmText}
            onChange={(e) => m.setUnifyConfirmText(e.target.value)}
            autoComplete="off"
            className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
          />
        </div>
      )}
      <div className="flex flex-wrap gap-2 mt-3">
        <button
          onClick={() => {
            if (m.unifyTarget === '' || m.unifyAbsorbed.length === 0) {
              m.setError('اختر الفرع الناجي وفرعًا واحدًا على الأقل للامتصاص');
              return;
            }
            void m.runPreview('unify', {
              action: 'unify',
              entryId: m.unifyTarget,
              absorbedIds: m.unifyAbsorbed,
              correctedName: m.unifyCorrected.trim() || null,
            });
          }}
          disabled={m.busy || m.previewing || m.unifyTarget === '' || m.unifyAbsorbed.length === 0}
          className="border border-sky-300 text-sky-800 hover:bg-sky-50 disabled:opacity-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
        >
          {m.previewing && m.previewFor === 'unify' ? 'جارِ المعاينة…' : 'معاينة التوحيد'}
        </button>
        {m.canCommit && m.previewFor === 'unify' && m.unifyConfirmText.trim() === m.unifySurvivorName() && (
          <button
            onClick={m.unifyCommit}
            disabled={m.busy}
            className="bg-sky-700 hover:bg-sky-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
          >
            {m.busy ? 'جارِ التوحيد…' : 'تنفيذ التوحيد'}
          </button>
        )}
      </div>
      {m.preview && m.previewFor === 'unify' && <BranchPreviewPanel preview={m.preview} />}
    </div>
  );
}
