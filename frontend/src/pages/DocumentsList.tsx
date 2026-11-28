import { Link } from 'react-router-dom';
import ExecutionActionsModal from '../components/ExecutionActionsModal';
import { STATUS_OPTIONS } from '../utils/documentStatus';
import {
  applicantName,
  displayFileNumber,
  fullName,
  publicEntityBranch as entityBranchDisplay,
} from '../utils/documentDisplay';
import {
  ActionsCell,
  AppealBadge,
  ColumnFilter,
  FileNumber,
  FilterSelect,
  MoreMenu,
  StatusBadge,
} from './documentsList/DocumentsListDisplay';
import { useDocumentsList } from './documentsList/useDocumentsList';

// توافق الوحدات القائمة: كانت `FileNumber`/`AppealBadge` تُصدَّر من هذه الصفحة.
export { AppealBadge, FileNumber } from './documentsList/DocumentsListDisplay';

/**
 * قائمة الملفات التنفيذية — قشرة عرض بعد `P6-02e`: المنطق في `useDocumentsList`
 * والخلايا/الفلاتر في `DocumentsListDisplay`، والسلوك منقول بلا تغيير.
 */
export default function DocumentsList() {
  const {
    isMobile,
    query,
    setQuery,
    status,
    setStatus,
    setStatusTouched,
    applicant,
    setApplicant,
    court,
    setCourt,
    lawyer,
    setLawyer,
    administrativeBranch,
    setAdministrativeBranch,
    executedEntity,
    setExecutedEntity,
    publicEntityBranch,
    setPublicEntityBranch,
    applicants,
    courts,
    lawyers,
    administrativeBranches,
    executedEntities,
    publicEntityBranches,
    canSeeAdministrativeBranch,
    canSeeAssignedLawyer,
    canSearchByLawyer,
    canCreate,
    canViewDeleted,
    canRotate,
    exporting,
    exportMsg,
    exportExcel,
    resetPaging,
    listQuery,
    loading,
    data,
    items,
    loadMore,
    loadingMore,
    loadMoreError,
    effectiveNextCursor,
    focusId,
    focusName,
    focusVisible,
    actionsDocId,
    setActionsDocId,
    openDocument,
    colSpan,
  } = useDocumentsList();

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
        <h2 className="text-2xl font-bold text-gray-800 text-balance">الملفات التنفيذية</h2>
        <div className="flex items-center gap-2 flex-wrap">
          <MoreMenu
            canViewDeleted={canViewDeleted}
            canRotate={canRotate}
            exporting={exporting}
            onExport={exportExcel}
          />
          {canCreate && (
            <Link
              to="/documents/new"
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center"
            >
              + ادخال ملف جديد
            </Link>
          )}
        </div>
      </div>

      {exportMsg && (
        <div
          role="alert"
          className="bg-amber-50 border border-amber-300 text-amber-800 rounded-lg px-4 py-3 mb-6 text-sm"
        >
          {exportMsg}
        </div>
      )}
      {exporting && !exportMsg && (
        <p role="status" className="text-xs text-gray-500 mb-6">
          التصدير قيد التنفيذ — الكبير منه قد يستغرق دقائق، لا تغلق الصفحة
        </p>
      )}

      <div className="bg-white rounded-xl shadow p-4 mb-6 flex flex-col sm:flex-row flex-wrap gap-3">
        <div className="flex-1 min-w-0">
          <label htmlFor="documents-search" className="sr-only">
            بحث في الملفات التنفيذية
          </label>
          <input
            id="documents-search"
            name="q"
            type="search"
            autoComplete="off"
            value={query}
            onChange={(e) => { setQuery(e.target.value); resetPaging(); }}
            placeholder="بحث بالاسم الثنائي أو الثلاثي لأحد المنفذ عليهم أو ورثة المتوفى، رقم العقد، دائرة التنفيذ…"
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 min-h-11"
          />
        </div>
        {isMobile && (
          <>
            <FilterSelect
              value={status}
              onChange={(v) => { setStatus(v); setStatusTouched(true); resetPaging(); }}
              ariaLabel="فلترة الحالة"
              allLabel="كل الحالات"
              options={STATUS_OPTIONS}
              className="sm:w-44"
            />
            {applicants.length > 0 && (
              <FilterSelect
                value={applicant}
                onChange={(v) => { setApplicant(v); resetPaging(); }}
                ariaLabel="فلترة طالب التنفيذ"
                allLabel="كل طالبي التنفيذ"
                options={applicants}
                className="sm:w-44"
              />
            )}
            {courts.length > 0 && (
              <FilterSelect
                value={court}
                onChange={(v) => { setCourt(v); resetPaging(); }}
                ariaLabel="فلترة دائرة التنفيذ"
                allLabel="كل دوائر التنفيذ"
                options={courts}
                className="sm:w-44"
              />
            )}
            {canSearchByLawyer && lawyers.length > 0 && (
              <FilterSelect
                value={lawyer}
                onChange={(v) => { setLawyer(v); resetPaging(); }}
                ariaLabel="فلترة المحامي المختص"
                allLabel="كل المحامين"
                options={lawyers}
                className="sm:w-44"
              />
            )}
            {canSeeAdministrativeBranch && administrativeBranches.length > 0 && (
              <FilterSelect
                value={administrativeBranch}
                onChange={(v) => { setAdministrativeBranch(v); resetPaging(); }}
                ariaLabel="فلترة فرع الإدارة"
                allLabel="كل فروع الإدارة"
                options={administrativeBranches}
                className="sm:w-44"
              />
            )}
            {executedEntities.length > 0 && (
              <FilterSelect
                value={executedEntity}
                onChange={(v) => { setExecutedEntity(v); resetPaging(); }}
                ariaLabel="فلترة الجهة العامة المنفذ عليها"
                allLabel="كل الجهات العامة المنفذ عليها"
                options={executedEntities}
                className="sm:w-44"
              />
            )}
            {publicEntityBranches.length > 0 && (
              <FilterSelect
                value={publicEntityBranch}
                onChange={(v) => { setPublicEntityBranch(v); resetPaging(); }}
                ariaLabel="فلترة فرع الجهة العامة"
                allLabel="كل فروع الجهات العامة"
                options={publicEntityBranches}
                className="sm:w-44"
              />
            )}
          </>
        )}
      </div>

      {loading && <div className="text-gray-500" role="status">جارِ البحث…</div>}

      {listQuery.error && !data && (
        <div
          role="alert"
          className="mb-4 bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 flex items-center justify-between gap-3 flex-wrap"
        >
          <span>{listQuery.error}</span>
          <button
            type="button"
            onClick={listQuery.refetch}
            className="min-h-11 px-4 rounded-lg bg-emerald-800 hover:bg-emerald-700 text-white font-medium"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {listQuery.error && data && (
        <div
          role="status"
          className="mb-4 bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800 flex items-center justify-between gap-3 flex-wrap"
        >
          <span>تعذر تحديث القائمة — تُعرض بيانات سابقة.</span>
          <button
            type="button"
            onClick={listQuery.refetch}
            className="min-h-11 px-4 rounded-lg border border-amber-300 hover:bg-amber-100 text-amber-900 font-medium"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {focusId != null && !focusVisible && focusName && (
        <div className="mb-4 flex items-center justify-between gap-3 flex-wrap bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-2 text-sm text-emerald-800">
          <span>كنت تعمل على ملف «{focusName}»</span>
          <Link to={`/documents/${focusId}`} className="font-bold underline whitespace-nowrap min-h-11 inline-flex items-center">
            فتح الملف
          </Link>
        </div>
      )}

      {data && (
        <>
          {isMobile ? (
            <div className="flex flex-col gap-4">
              {items.map((d) => (
                <article
                  key={d.id}
                  id={`doc-row-${d.id}`}
                  className={d.id === focusId ? 'bg-emerald-50 ring-2 ring-emerald-500 rounded-xl shadow p-4' : 'bg-white rounded-xl shadow p-4'}
                >
                  {canSeeAdministrativeBranch && (
                    <div className="text-xs text-gray-500 mb-2">
                      فرع الإدارة: {d.administrativeBranchName || '—'}
                    </div>
                  )}
                  <div className="flex items-center justify-between gap-2 mb-2">
                    <div className="flex flex-wrap items-center gap-1.5 min-w-0">
                      <StatusBadge d={d} />
                      <AppealBadge d={d} />
                    </div>
                    <span
                      className={`shrink-0 text-sm font-medium tabular-nums ${
                        d.needsRotation ? 'text-red-600 font-bold' : 'text-gray-800'
                      }`}
                    >
                      {displayFileNumber(d) || '—'}
                    </span>
                  </div>
                  <Link
                    to={`/documents/${d.id}`}
                    onClick={() => openDocument(d.id)}
                    className="text-emerald-800 font-bold text-lg hover:underline flex items-center min-h-11 mb-1 gap-2 min-w-0 break-words focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded"
                  >
                    {fullName(d) || displayFileNumber(d) || `مستند ${d.id}`}
                    {d.id === focusId && (
                      <span className="text-[10px] font-medium px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 whitespace-nowrap">
                        آخر ملف تم فتحه
                      </span>
                    )}
                  </Link>
                  <div className="text-sm text-gray-600">
                    {[applicantName(d), entityBranchDisplay(d), d.court].filter(Boolean).join(' · ') || '—'}
                  </div>
                  {canSeeAssignedLawyer && (
                    <div className="text-xs text-gray-500 mt-1">
                      المحامي المختص: {d.lawyer || '—'}
                    </div>
                  )}
                  <div className="mt-3 pt-3 border-t border-gray-100">
                    <ActionsCell d={d} compact onClick={() => setActionsDocId(d.id)} />
                  </div>
                </article>
              ))}
              {items.length === 0 && (
                <div className="bg-white rounded-xl shadow p-8 text-center text-gray-600">
                  لا توجد نتائج
                </div>
              )}
            </div>
          ) : (
            <div className="bg-white rounded-xl shadow overflow-x-auto">
              <table className="w-full table-fixed text-sm">
                <thead className="bg-gray-50 text-emerald-900 font-bold">
                  <tr className="text-right">
                    {canSeeAdministrativeBranch && (
                      <th className="px-4 py-3 align-top w-[9%]">
                        <ColumnFilter
                          label="فرع الإدارة"
                          ariaLabel="فلترة فرع الإدارة"
                          value={administrativeBranch}
                          onChange={(v) => { setAdministrativeBranch(v); resetPaging(); }}
                          allLabel="كل فروع الإدارة"
                          options={administrativeBranches}
                        />
                      </th>
                    )}
                    <th className="px-4 py-3 align-top w-[8%]">
                      <ColumnFilter
                        label="الحالة"
                        ariaLabel="فلترة الحالة"
                        value={status}
                        onChange={(v) => { setStatus(v); setStatusTouched(true); resetPaging(); }}
                        allLabel="كل الحالات"
                        options={STATUS_OPTIONS}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[12%]">
                      <ColumnFilter
                        label="طالب التنفيذ"
                        ariaLabel="فلترة طالب التنفيذ"
                        value={applicant}
                        onChange={(v) => { setApplicant(v); resetPaging(); }}
                        allLabel="كل طالبي التنفيذ"
                        options={applicants}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[10%]">
                      <ColumnFilter
                        label="فرع الجهة العامة"
                        ariaLabel="فلترة فرع الجهة العامة"
                        value={publicEntityBranch}
                        onChange={(v) => { setPublicEntityBranch(v); resetPaging(); }}
                        allLabel="كل فروع الجهات العامة"
                        options={publicEntityBranches}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[14%]">
                      <ColumnFilter
                        label="المنفذ عليه"
                        ariaLabel="فلترة الجهة العامة المنفذ عليها"
                        value={executedEntity}
                        onChange={(v) => { setExecutedEntity(v); resetPaging(); }}
                        allLabel="كل الجهات العامة المنفذ عليها"
                        options={executedEntities}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[11%]">
                      <ColumnFilter
                        label="دائرة التنفيذ"
                        ariaLabel="فلترة دائرة التنفيذ"
                        value={court}
                        onChange={(v) => { setCourt(v); resetPaging(); }}
                        allLabel="كل دوائر التنفيذ"
                        options={courts}
                      />
                    </th>
                    <th className="px-4 py-3 align-top w-[9%]">رقم الملف</th>
                    {canSeeAssignedLawyer && (
                      <th className="px-4 py-3 align-top w-[10%]">
                        <ColumnFilter
                          label="المحامي المختص"
                          ariaLabel="فلترة المحامي المختص"
                          value={lawyer}
                          onChange={(v) => { setLawyer(v); resetPaging(); }}
                          allLabel="كل المحامين"
                          options={canSearchByLawyer ? lawyers : []}
                        />
                      </th>
                    )}
                    <th className="px-4 py-3 align-top w-[14%]">الإجراءات والملاحظات</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {items.map((d) => (
                    <tr
                      key={d.id}
                      id={`doc-row-${d.id}`}
                      className={d.id === focusId ? 'bg-emerald-50' : 'hover:bg-gray-50'}
                    >
                      {canSeeAdministrativeBranch && (
                        <td className="px-4 py-3">{d.administrativeBranchName || '—'}</td>
                      )}
                      <td className="px-4 py-3">
                        <StatusBadge d={d} />
                      </td>
                      <td className="px-4 py-3">{applicantName(d) || '—'}</td>
                      <td className="px-4 py-3">{entityBranchDisplay(d) || '—'}</td>
                      <td className="px-4 py-3">
                        <Link
                          to={`/documents/${d.id}`}
                          onClick={() => openDocument(d.id)}
                          className="inline-flex items-center gap-2 min-h-11 text-emerald-800 font-bold hover:underline"
                        >
                          {fullName(d) || displayFileNumber(d) || `مستند ${d.id}`}
                          {d.id === focusId && (
                            <span className="text-[10px] font-medium px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 whitespace-nowrap">
                              آخر ملف تم فتحه
                            </span>
                          )}
                        </Link>
                      </td>
                      <td className="px-4 py-3">{d.court || '—'}</td>
                      <td className="px-4 py-3">
                        <FileNumber d={d} />
                      </td>
                      {canSeeAssignedLawyer && (
                        <td className="px-4 py-3">{d.lawyer || '—'}</td>
                      )}
                      <td className="px-4 py-3">
                        <ActionsCell d={d} onClick={() => setActionsDocId(d.id)} />
                      </td>
                    </tr>
                  ))}
                  {items.length === 0 && (
                    <tr>
                      <td colSpan={colSpan} className="px-4 py-8 text-center text-gray-600">
                        لا توجد نتائج
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          <div className="flex items-center justify-between mt-4 text-sm text-gray-600 flex-wrap gap-2">
            <span className="tabular-nums">
              عرض {items.length} من {data.totalCount} نتيجة
            </span>
            <div className="flex flex-col items-end gap-2">
              {loadMoreError && <span role="alert" className="text-red-600">{loadMoreError}</span>}
              {effectiveNextCursor ? (
                <button
                  type="button"
                  onClick={() => void loadMore()}
                  disabled={loadingMore || loading}
                  className="px-4 py-2 border border-emerald-700 text-emerald-800 rounded-lg hover:bg-emerald-50 disabled:opacity-40 min-h-11"
                >
                  {loadingMore ? 'جارِ التحميل…' : 'تحميل المزيد'}
                </button>
              ) : (
                <span className="text-gray-600">نهاية النتائج</span>
              )}
            </div>
          </div>
        </>
      )}

      {actionsDocId !== null && (
        <ExecutionActionsModal
          documentId={actionsDocId}
          onClose={() => setActionsDocId(null)}
          onChanged={listQuery.refetch}
        />
      )}
    </div>
  );
}
