import { GOVERNORATES } from '../../utils/governorate';
import type { BranchesManagementModel } from './useBranchesManagement';

/** نافذة تعديل فرع: الحقول، تفعيل/إيقاف، الحفظ، وتأكيد الحذف في المكان نفسه. */
export function BranchEditModal({ m }: { m: BranchesManagementModel }) {
  if (!m.editing) return null;

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="تعديل فرع"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg max-h-[85vh] overflow-y-auto p-5">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-bold text-gray-800">تعديل فرع</h3>
          <button
            onClick={m.closeEdit}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="grid sm:grid-cols-2 gap-4">
          <div>
            <label htmlFor="edit-branch-name" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع</label>
            <input
              id="edit-branch-name"
              value={m.editName}
              onChange={(e) => m.setEditName(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="edit-branch-code" className="block text-xs font-medium text-gray-600 mb-1">كود الفرع</label>
            <input
              id="edit-branch-code"
              value={m.editCode}
              onChange={(e) => m.setEditCode(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="edit-branch-address" className="block text-xs font-medium text-gray-600 mb-1">العنوان</label>
            <input
              id="edit-branch-address"
              value={m.editAddress}
              onChange={(e) => m.setEditAddress(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div>
            <label htmlFor="edit-branch-phone" className="block text-xs font-medium text-gray-600 mb-1">الهاتف</label>
            <input
              id="edit-branch-phone"
              value={m.editPhone}
              onChange={(e) => m.setEditPhone(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>
          <div className="sm:col-span-2">
            <label htmlFor="edit-branch-governorate" className="block text-xs font-medium text-gray-600 mb-1">المحافظة <span aria-hidden="true" className="text-red-500">*</span></label>
            <select
              id="edit-branch-governorate"
              name="governorate"
              required
              value={m.editGovernorate}
              onChange={(e) => m.setEditGovernorate(e.target.value)}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">اختر المحافظة…</option>
              {GOVERNORATES.map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
            <p className="text-xs text-gray-400 mt-1">إجبارية — تحدد نطاق رئيس القسم في سجل الجهات العامة.</p>
          </div>
        </div>

        <label className="mt-4 inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
          <input
            type="checkbox"
            checked={m.editActive}
            onChange={(e) => m.setEditActive(e.target.checked)}
          />
          الفرع مفعّل
        </label>

        {m.editError && <p className="text-red-600 text-sm mt-3">{m.editError}</p>}

        {m.confirmingDelete ? (
          <div className="mt-5 border border-red-200 bg-red-50 rounded-lg p-4">
            <p className="text-sm text-red-700 mb-3">هل أنت متأكد من حذف هذا الفرع نهائياً؟</p>
            <div className="flex flex-wrap gap-2">
              <button
                onClick={m.deleteBranch}
                disabled={m.deleting}
                className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {m.deleting ? 'جارِ الحذف...' : 'تأكيد الحذف'}
              </button>
              <button
                onClick={() => m.setConfirmingDelete(false)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          </div>
        ) : (
          <div className="mt-5 flex flex-wrap gap-2 justify-between">
            <button
              onClick={m.saveEdit}
              disabled={m.editSaving}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {m.editSaving ? 'جارِ الحفظ...' : 'حفظ التعديل'}
            </button>
            <div className="flex gap-2">
              <button
                onClick={() => m.setConfirmingDelete(true)}
                className="border border-red-200 text-red-600 hover:bg-red-50 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                حذف الفرع
              </button>
              <button
                onClick={m.closeEdit}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
