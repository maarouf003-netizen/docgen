import { useEffect, useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useIsMobile } from '../hooks/useMediaQuery';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { fileNumberLabel, tripleName } from '../utils/documentDisplay';
import type { PagedResult, RotationDocumentDto } from '../types';

const fullName = (r: RotationDocumentDto) =>
  r.displayName || tripleName(r.borrowerName, r.borrowerFather, r.borrowerFamily);
const displayFileNumber = (r: RotationDocumentDto) => fileNumberLabel(r.fileNumber, r.fileType);

const PER_PAGE = 20;

export default function Rotation() {
  const { user } = useAuth();
  const isMobile = useIsMobile();
  const [page, setPage] = useState(1);
  const [values, setValues] = useState<Record<number, string>>({});
  const [initial, setInitial] = useState<Record<number, string>>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const year = new Date().getFullYear();
  const canRotate = user?.role === 'lawyer';

  const rotationQuery = useCancellableRequest<PagedResult<RotationDocumentDto>>(
    (signal) =>
      api
        .get<PagedResult<RotationDocumentDto>>('/documents/rotate', {
          params: { page, perPage: PER_PAGE },
          signal,
        })
        .then((r) => r.data),
    [page],
    { enabled: Boolean(canRotate) },
  );

  const data = rotationQuery.data;
  const rows = useMemo(() => data?.items ?? [], [data]);
  const loading = rotationQuery.isLoading;
  const loadError = rotationQuery.error;

  // إعادة تهيئة الحقول القابلة للتعديل من بيانات الصفحة المجلوبة حديثًا.
  useEffect(() => {
    if (!data) return;
    const v: Record<number, string> = {};
    (data.items ?? []).forEach((row) => {
      v[row.documentId] = row.baseNumber ?? '';
    });
    setValues(v);
    setInitial(v);
  }, [data]);

  const changedEntries = useMemo(
    () =>
      rows
        .filter((row) => (values[row.documentId] ?? '').trim() !== (initial[row.documentId] ?? ''))
        .map((row) => ({
          documentId: row.documentId,
          baseNumber: (values[row.documentId] ?? '').trim() || null,
        })),
    [rows, values, initial],
  );

  const handleSave = async () => {
    setSaving(true);
    setError('');
    setMessage('');
    try {
      await api.put('/documents/rotate', { entries: changedEntries });
      setMessage('تم حفظ أرقام الأساس بنجاح');
      rotationQuery.refetch();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  if (!canRotate) {
    return (
      <div className="max-w-6xl mx-auto">
        <h2 className="text-2xl font-bold text-gray-800 mb-6">تدوير أرقام الأساس</h2>
        <div className="bg-white rounded-xl shadow p-8 text-center text-gray-500">
          لا تملك صلاحية تنفيذ هذا الإجراء
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-2 flex-wrap gap-3">
        <h2 className="text-2xl font-bold text-gray-800">تدوير أرقام الأساس</h2>
        <span className="text-sm font-medium text-emerald-900 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-1.5">
          سنة التدوير: {year}
        </span>
      </div>
      <p className="text-sm text-gray-600 mb-6">
        يعرض هذا الجدول الملفات المؤهلة للتدوير فقط: ملفات من سنوات سابقة لم تُدوَّر بعد لهذه
        السنة.
      </p>

      {(error || loadError) && <div className="text-red-600 mb-4">{error || loadError}</div>}
      {message && (
        <div className="text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-2 mb-4">
          {message}
        </div>
      )}

      {loading && <div className="text-gray-500">جارِ التحميل...</div>}

      {!loading && data && (data.totalCount ?? 0) === 0 && !error && !loadError && (
        <div className="bg-white rounded-xl shadow p-8 text-center text-gray-400">
          لا توجد ملفات مؤهلة للتدوير
        </div>
      )}

      {rows.length > 0 && (
        <>
          {isMobile ? (
            <div className="flex flex-col gap-4">
              {rows.map((row) => (
                <article key={row.documentId} className="bg-white rounded-xl shadow p-4">
                  <div className="text-emerald-800 font-bold text-lg mb-1">
                    {fullName(row) || `مستند ${row.documentId}`}
                  </div>
                  <div className="text-sm text-gray-600">
                    الدائرة: {row.court || '—'}
                  </div>
                  <div className="text-sm font-medium text-gray-800 mt-1">
                    رقم الملف: {displayFileNumber(row) || '—'} · نوعه: {row.fileType || '—'}
                  </div>
                  <label className="block mt-3">
                    <span className="block text-sm text-gray-600 mb-1">رقم أساس {year}</span>
                    <input
                      value={values[row.documentId] ?? ''}
                      onChange={(e) =>
                        setValues((prev) => ({ ...prev, [row.documentId]: e.target.value }))
                      }
                      maxLength={50}
                      aria-label={`رقم أساس ${fullName(row) || row.documentId}`}
                      placeholder="رقم الأساس"
                      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
                    />
                  </label>
                </article>
              ))}
            </div>
          ) : (
            <div className="bg-white rounded-xl shadow overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-emerald-900 font-bold">
                  <tr className="text-right">
                    <th className="px-4 py-3 w-[14%]">الدائرة</th>
                    <th className="px-4 py-3 w-[26%]">الاسم الثلاثي</th>
                    <th className="px-4 py-3 w-[16%]">رقم الملف</th>
                    <th className="px-4 py-3 w-[14%]">نوعه</th>
                    <th className="px-4 py-3 w-[30%]">رقم أساس {year}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {rows.map((row) => (
                    <tr key={row.documentId} className="hover:bg-gray-50">
                      <td className="px-4 py-3">{row.court || '—'}</td>
                      <td className="px-4 py-3 font-medium text-gray-800">
                        {fullName(row) || `مستند ${row.documentId}`}
                      </td>
                      <td className="px-4 py-3">{displayFileNumber(row)}</td>
                      <td className="px-4 py-3">{row.fileType || '—'}</td>
                      <td className="px-4 py-3">
                        <input
                          value={values[row.documentId] ?? ''}
                          onChange={(e) =>
                            setValues((prev) => ({ ...prev, [row.documentId]: e.target.value }))
                          }
                          maxLength={50}
                          aria-label={`رقم أساس ${fullName(row) || row.documentId}`}
                          placeholder="رقم الأساس"
                          className="w-full max-w-56 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11"
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="flex items-center justify-between mt-4 text-sm text-gray-600 flex-wrap gap-2">
            <span>
              صفحة {data?.page ?? 1} من {data?.totalPages || 1} ({data?.totalCount ?? 0} نتيجة)
            </span>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={page <= 1 || loading}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                السابق
              </button>
              <button
                type="button"
                disabled={page >= (data?.totalPages ?? 1) || loading}
                onClick={() => setPage((p) => Math.min(data?.totalPages ?? 1, p + 1))}
                className="px-3 py-1.5 border border-gray-300 rounded-lg disabled:opacity-40 hover:bg-gray-50 min-h-11"
              >
                التالي
              </button>
            </div>
          </div>

          <div className="flex items-center justify-end mt-4">
            <button
              type="button"
              onClick={handleSave}
              disabled={saving || changedEntries.length === 0}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-6 py-2.5 text-sm font-medium min-h-11 disabled:opacity-50"
            >
              {saving ? 'جارِ الحفظ...' : 'حفظ أرقام الأساس'}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
