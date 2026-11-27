import { GOVERNORATES } from '../utils/governorate';
import { BranchEditModal } from './branchesManagement/BranchEditModal';
import { BranchSectionsPanel } from './branchesManagement/BranchSectionsPanel';
import { BranchesTable } from './branchesManagement/BranchesTable';
import { useBranchesManagement } from './branchesManagement/useBranchesManagement';

/**
 * إدارة الفروع: إنشاء/تعديل/حذف، وعرض الجدول/البطاقات، وإدارة الشعب ونقل الدوائر
 * وسجل التعاقب. الحالة والقيادة في `branchesManagement/useBranchesManagement`،
 * والعرض في مكوّنات `branchesManagement/*`.
 */
export default function BranchesManagement() {
  const m = useBranchesManagement();

  return (
    <div className="max-w-5xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">إدارة الفروع</h2>

      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="text-sm text-gray-600">
          {m.loading ? 'جارِ التحميل...' : `${m.branches.length} فرع`}
        </div>
        <button
          onClick={() => m.setShowForm((v) => !v)}
          className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
        >
          {m.showForm ? 'إلغاء' : '+ إضافة فرع'}
        </button>
      </div>

      {m.showForm && (
        <form
          onSubmit={m.submit}
          noValidate
          className="bg-white rounded-xl shadow p-4 mb-4 grid sm:grid-cols-2 gap-4"
        >
          <div>
            <label htmlFor="branch-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع</label>
            <input
              id="branch-name"
              value={m.name}
              onChange={(e) => m.setName(e.target.value)}
              placeholder="مثال: فرع دمشق..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-code" className="block text-xs font-medium text-gray-600 mb-1">كود الفرع</label>
            <input
              id="branch-code"
              value={m.code}
              onChange={(e) => m.setCode(e.target.value)}
              placeholder="مثال: DAM..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-address" className="block text-xs font-medium text-gray-600 mb-1">العنوان</label>
            <input
              id="branch-address"
              value={m.address}
              onChange={(e) => m.setAddress(e.target.value)}
              placeholder="اختياري..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-phone" className="block text-xs font-medium text-gray-600 mb-1">الهاتف</label>
            <input
              id="branch-phone"
              value={m.phone}
              onChange={(e) => m.setPhone(e.target.value)}
              placeholder="اختياري..."
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="branch-governorate" className="block text-xs font-medium text-gray-600 mb-1">المحافظة <span aria-hidden="true" className="text-red-500">*</span></label>
            <select
              id="branch-governorate"
              name="governorate"
              required
              value={m.governorate}
              onChange={(e) => m.setGovernorate(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">اختر المحافظة…</option>
              {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
          </div>
          {m.formError && <p className="text-red-600 text-sm sm:col-span-2">{m.formError}</p>}
          <div className="sm:col-span-2 flex gap-2">
            <button
              type="submit"
              disabled={m.saving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {m.saving ? 'جارِ الحفظ...' : 'إنشاء الفرع'}
            </button>
          </div>
        </form>
      )}

      {m.fetchError && <div className="text-red-600 mb-4">{m.fetchError}</div>}

      <BranchesTable
        branches={m.branches}
        loading={m.loading}
        loadFailed={!!m.fetchError}
        onEdit={m.openEdit}
      />

      <BranchSectionsPanel m={m} />

      <BranchEditModal m={m} />
    </div>
  );
}
