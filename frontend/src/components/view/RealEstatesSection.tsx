import type { DocumentResponse } from '../../types';

export function RealEstatesSection({ doc }: { doc: DocumentResponse }) {
  return (
    <div className="bg-white rounded-xl shadow p-5">
      <h3 className="font-bold text-emerald-800 mb-3">العقارات</h3>
      {doc.realEstates.length === 0 && <p className="text-gray-400 text-sm">لا توجد عقارات</p>}
      {doc.realEstates.map((r, i) => (
        <div
          key={r.id ?? i}
          className="flex flex-wrap items-center gap-x-5 gap-y-1 py-2 border-b border-gray-100 last:border-0 text-sm"
        >
          <span className="font-bold text-gray-700">عقار {r.property ?? i + 1}</span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">رقم العقار</span>
            <span className="text-gray-800">{r.propertyNumber || '—'}</span>
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">المنطقة العقارية</span>
            <span className="text-gray-800">{r.propertyDistrict || '—'}</span>
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">المصالح العقارية المختصة</span>
            <span className="text-gray-800">{r.landRegistry || '—'}</span>
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="text-gray-500">مالك العقار</span>
            <span className="text-gray-800">{(r.owners ?? []).join(' و ') || '—'}</span>
          </span>
        </div>
      ))}
    </div>
  );
}
