import { SUCCESSION_EVENT_LABELS } from './branchesManagementModel';
import type { BranchesManagementModel, SubTab } from './useBranchesManagement';

const SUB_TABS: SubTab[] = ['sections', 'transfer', 'succession'];

/** الشعب ونقل الدوائر وسجل التعاقب للفرع الأب المختار (§8 — مدير/مشرف). */
export function BranchSectionsPanel({ m }: { m: BranchesManagementModel }) {
  return (
    <section aria-label="الشعب ونقل الدوائر وسجل التعاقب" className="bg-white rounded-xl shadow p-4 mt-4">
      <h3 className="text-lg font-bold text-gray-800 mb-3">الشعب ونقل الدوائر وسجل التعاقب</h3>
      <div className="mb-3">
        <label htmlFor="sub-branch" className="block text-xs font-medium text-gray-600 mb-1">الفرع الأب</label>
        <select
          id="sub-branch"
          value={m.subBranchId}
          onChange={(e) => m.pickSubBranch(e.target.value ? Number(e.target.value) : '')}
          className="w-full sm:w-72 min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
        >
          <option value="">اختر الفرع...</option>
          {m.branches.map((b) => (
            <option key={b.id} value={b.id}>{b.name}</option>
          ))}
        </select>
      </div>

      {m.subBranchId !== '' && (
        <>
          <div className="flex flex-wrap gap-2 mb-4" role="tablist" aria-label="أقسام إدارة الشعب">
            {SUB_TABS.map((t) => (
              <button
                key={t}
                type="button"
                role="tab"
                aria-selected={m.subTab === t}
                onClick={() => m.setSubTab(t)}
                className={`min-h-11 rounded-lg px-4 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 ${
                  m.subTab === t ? 'bg-emerald-800 text-white' : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                }`}
              >
                {t === 'sections' ? 'الشعب' : t === 'transfer' ? 'نقل دائرة' : 'سجل التعاقب'}
              </button>
            ))}
          </div>

          {m.subTab === 'sections' && (
            <div role="tabpanel">
              <div className="flex flex-col sm:flex-row gap-2 sm:items-end mb-3">
                <div className="flex-1">
                  <label htmlFor="new-section-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الشعبة الجديدة</label>
                  <input
                    id="new-section-name"
                    value={m.newSectionName}
                    onChange={(e) => m.setNewSectionName(e.target.value)}
                    placeholder="مثال: شعبة مصياف…"
                    autoComplete="off"
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  />
                </div>
                <button
                  type="button"
                  onClick={m.createSection}
                  disabled={m.sectionBusy}
                  className="min-h-11 bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm"
                >
                  {m.sectionBusy ? 'جارِ الحفظ...' : '+ إضافة شعبة'}
                </button>
              </div>
              {m.sectionsError && <p role="alert" className="text-red-600 text-sm mb-3">{m.sectionsError}</p>}
              {m.sectionsLoading ? (
                <p className="text-gray-500 py-4 text-center">جارِ تحميل الشعب...</p>
              ) : m.sections.length === 0 ? (
                <p className="text-gray-500 py-4 text-center">لا توجد شعب — أضف أول شعبة لهذا الفرع</p>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {m.sections.map((s) => (
                    <li key={s.id} className="py-3 flex flex-wrap items-center justify-between gap-2">
                      <div className="min-w-0">
                        <span className="font-bold text-gray-800 break-words">{s.name}</span>
                        <span className="text-xs text-gray-500 ms-2 tabular-nums">
                          {s.circuitCount} دائرة{s.headName ? ` · الرئيس: ${s.headName}` : ' · بلا رئيس'}
                        </span>
                        {!s.isActive && (
                          <span className="ms-2 rounded-full bg-gray-100 text-gray-600 px-2 py-0.5 text-[11px]">معطلة</span>
                        )}
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => { m.setRenaming(s); m.setRenameName(s.name); }}
                          aria-label={`إعادة تسمية ${s.name}`}
                          className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                        >
                          تسمية
                        </button>
                        <button
                          type="button"
                          onClick={() => m.setSectionActive(s, !s.isActive)}
                          disabled={m.sectionBusy}
                          aria-label={`${s.isActive ? 'تعطيل' : 'تفعيل'} ${s.name}`}
                          className="border border-gray-300 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                        >
                          {s.isActive ? 'تعطيل' : 'تفعيل'}
                        </button>
                        <button
                          type="button"
                          onClick={() => m.deleteSection(s)}
                          disabled={m.sectionBusy}
                          aria-label={`حذف ${s.name}`}
                          className="border border-red-200 text-red-700 rounded-lg px-3 py-2 text-xs min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
                        >
                          حذف
                        </button>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              {m.renaming && (
                <div className="mt-3 border border-gray-200 rounded-lg p-3 flex flex-col sm:flex-row gap-2 sm:items-end">
                  <div className="flex-1">
                    <label htmlFor="rename-section-name" className="block text-xs font-medium text-gray-600 mb-1">
                      الاسم الجديد لشعبة «{m.renaming.name}»
                    </label>
                    <input
                      id="rename-section-name"
                      value={m.renameName}
                      onChange={(e) => m.setRenameName(e.target.value)}
                      autoComplete="off"
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    />
                  </div>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={m.saveRename}
                      disabled={m.sectionBusy}
                      className="min-h-11 bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm"
                    >
                      حفظ
                    </button>
                    <button
                      type="button"
                      onClick={() => m.setRenaming(null)}
                      className="min-h-11 border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                    >
                      إلغاء
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}

          {m.subTab === 'transfer' && (
            <div role="tabpanel">
              <p className="text-xs text-gray-500 mb-3">
                النقل داخل الفرع نفسه — المفتوح ينتقل والمغلق تبقى رؤيته للمالك الجديد، والإحالات المفتوحة تُعاد توجيهها تلقائيًا.
              </p>
              {m.circuitsError && <p role="alert" className="text-red-600 text-sm mb-3">{m.circuitsError}</p>}
              <div className="grid sm:grid-cols-2 gap-3 mb-3">
                <div>
                  <label htmlFor="transfer-circuit" className="block text-xs font-medium text-gray-600 mb-1">الدائرة</label>
                  <select
                    id="transfer-circuit"
                    value={m.transferCircuitId}
                    onChange={(e) => m.setTransferCircuitId(e.target.value ? Number(e.target.value) : '')}
                    disabled={m.circuitsLoading}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  >
                    <option value="">{m.circuitsLoading ? 'جارِ التحميل...' : 'اختر الدائرة...'}</option>
                    {m.circuits.map((c) => (
                      <option key={c.circuitId} value={c.circuitId}>
                        {c.circuitName} — {c.sectionName ?? 'القسم'}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="transfer-target" className="block text-xs font-medium text-gray-600 mb-1">المالك الجديد</label>
                  <select
                    id="transfer-target"
                    value={m.transferTarget}
                    onChange={(e) => m.setTransferTarget(e.target.value ? Number(e.target.value) : '')}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  >
                    <option value="">قسم الفرع</option>
                    {m.sections.map((s) => (
                      <option key={s.id} value={s.id}>شعبة {s.name}</option>
                    ))}
                  </select>
                </div>
              </div>
              <button
                type="button"
                onClick={m.submitTransfer}
                disabled={m.transferBusy || m.transferCircuitId === ''}
                className="min-h-11 bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm"
              >
                {m.transferBusy ? 'جارِ النقل...' : 'نقل الدائرة'}
              </button>
              {m.transferMsg && <p role="status" className="text-sm text-gray-700 mt-3">{m.transferMsg}</p>}
            </div>
          )}

          {m.subTab === 'succession' && (
            <div role="tabpanel">
              {m.successionError && <p role="alert" className="text-red-600 text-sm mb-3">{m.successionError}</p>}
              {m.successionLoading ? (
                <p className="text-gray-500 py-4 text-center">جارِ تحميل السجل...</p>
              ) : m.succession.length === 0 ? (
                <p className="text-gray-500 py-4 text-center">لا توجد أحداث تعاقب لهذا الفرع</p>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {m.succession.map((h) => (
                    <li key={h.id} className="py-2 text-sm">
                      <span className="font-bold text-gray-800 break-words">{h.userName ?? '—'}</span>
                      <span className="text-gray-500"> · {h.role} · {SUCCESSION_EVENT_LABELS[h.event] ?? h.event}</span>
                      {h.sectionName ? <span className="text-gray-500"> · شعبة {h.sectionName}</span> : null}
                      <span className="block text-xs text-gray-400 mt-0.5 break-words">
                        {h.actorName ?? ''} · {h.at}{h.reason ? ` · ${h.reason}` : ''}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </>
      )}
    </section>
  );
}
