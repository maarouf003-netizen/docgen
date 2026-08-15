import { useState } from 'react';
import { api } from '../../api/client';
import type { DocumentResponse, HeirDto, RealEstateDto } from '../../types';
import { Toast } from '../Toast';
import { EstateSelection } from './EstateSelection';
import { fullName } from './viewFormat';

type BasicDoc = { code: string; label: string };

const BASIC_DOCS: BasicDoc[] = [
  { code: '001', label: 'استدعاء تنفيذي' },
  { code: '002', label: 'محضر تنفيذي' },
  { code: '004', label: 'حجز منظومة' },
  { code: 'PS', label: 'حجز عقاري' },
];

export function DocumentGenerationSection({ doc, id }: { doc: DocumentResponse; id: string | undefined }) {
  const [noticeSel, setNoticeSel] = useState<number[]>([]);
  const [estateSel, setEstateSel] = useState<number[]>([]);
  const [generating, setGenerating] = useState('');
  const [downloadError, setDownloadError] = useState('');
  const [downloadSuccess, setDownloadSuccess] = useState('');

  const isOrdinary = doc.contractTypeSelector === 'عادي';
  const debtor = {
    name: doc.borrowerName,
    father: doc.borrowerFather,
    family: doc.borrowerFamily,
    mother: doc.borrowerMother,
    birth: doc.borrowerBirth,
    register: doc.borrowerRegister,
    nationalId: doc.borrowerNationalId,
    addressType: doc.borrowerAddressType,
    address: doc.borrowerAddress,
  };

  const toggleNotice = (number: number) => {
    setNoticeSel((prev) =>
      prev.includes(number) ? prev.filter((x) => x !== number) : [...prev, number],
    );
  };

  const toggleEstate = (estateId: number) => {
    setEstateSel((prev) =>
      prev.includes(estateId) ? prev.filter((x) => x !== estateId) : [...prev, estateId],
    );
  };

  const downloadOne = async (code: string, recipient: number, estateIds: number[], heirId?: number) => {
    const res = await api.get(`/documents/${id}/generate`, {
      params: {
        template: code,
        recipient,
        heirId: heirId || undefined,
        estateIds: estateIds.length > 0 ? estateIds : undefined,
      },
      responseType: 'blob',
    });
    const disposition = (res.headers['content-disposition'] as string | undefined) ?? '';
    const match = disposition.match(/filename="?([^";]+)"?/i);
    const filename = match?.[1] ?? `مستند_${code}.docx`;

    const url = URL.createObjectURL(res.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  };

  const runGeneration = async (
    code: string,
    task: () => Promise<void>,
    successMessage: string,
    failMessage: string,
  ) => {
    setGenerating(code);
    setDownloadError('');
    setDownloadSuccess('');
    try {
      await task();
      setDownloadSuccess(successMessage);
    } catch {
      setDownloadError(failMessage);
    } finally {
      setGenerating('');
    }
  };

  const generateBasic = async (code: string, label: string) => {
    await runGeneration(
      code,
      () => downloadOne(code, 0, []),
      `✅ تم إنشاء ${label} بنجاح`,
      `فشل توليد ${label} — تحقق من اكتمال البيانات`,
    );
  };

  const generateSeizure = async () => {
    if (estateSel.length === 0) {
      setDownloadError('حجز عقاري: اختر عقاراً واحداً على الأقل');
      return;
    }
    await runGeneration(
      'PS',
      async () => {
        for (const estateId of estateSel) {
          await downloadOne('PS', 0, [estateId]);
        }
      },
      `✅ تم إنشاء ${estateSel.length} مستند حجز عقاري`,
      'فشل توليد حجز عقاري — تحقق من اكتمال البيانات',
    );
  };

  const generateNotice = async (code: '003' | '007') => {
    if (noticeSel.length === 0) {
      setDownloadError('اختر شخصاً واحداً على الأقل من قائمة المنفَّذ عليهم');
      return;
    }
    const label = code === '007' ? 'إخطار تنفيذي بالصحف' : 'إخطار تنفيذي';
    await runGeneration(
      code,
      async () => {
        for (const person of noticePersons) {
          if (!noticeSel.includes(person.number)) continue;
          await downloadOne(code, person.heirId != null ? 0 : person.number, [], person.heirId);
        }
      },
      code === '007'
        ? `✅ تم إنشاء ${noticeSel.length} إخطار تنفيذي بالصحف بنجاح`
        : `✅ تم إنشاء ${noticeSel.length} إخطار بنجاح`,
      `فشل توليد ${label} — تحقق من اكتمال البيانات`,
    );
  };

  const generateEstateNotice = async (code: '005' | '006') => {
    if (estateSel.length === 0) {
      setDownloadError('اختر عقاراً واحداً على الأقل من قائمة العقارات');
      return;
    }
    if (multiOwner) {
      setDownloadError('يجب أن تكون العقارات لنفس المالك');
      return;
    }
    const label = code === '005' ? 'إخطار بيع أموال غير منقولة' : 'إخطار بيع أموال غير منقولة بالصحف';
    const ownerHeirs = findEstateHeirs(selectedEstates[0]);
    const heirList = ownerHeirs ?? [];
    const hasIdHeirs = heirList.filter((h) => h.id != null);
    const perHeir = heirList.length > 0 && hasIdHeirs.length > 0;
    await runGeneration(
      code,
      () =>
        perHeir
          ? (async () => {
              for (const heir of hasIdHeirs) {
                await downloadOne(code, 0, estateSel, heir.id);
              }
            })()
          : downloadOne(code, 0, estateSel),
      perHeir
        ? code === '005'
          ? `✅ تم إنشاء ${hasIdHeirs.length} إخطار بيع أموال غير منقولة بنجاح`
          : `✅ تم إنشاء ${hasIdHeirs.length} إخطار بيع أموال غير منقولة بالصحف بنجاح`
        : code === '005'
          ? '✅ تم إنشاء إخطار بيع أموال غير منقولة بنجاح'
          : 'تم إنشاء إخطار بيع أموال غير منقولة بالصحف بنجاح',
      `فشل توليد ${label} — تحقق من اكتمال البيانات`,
    );
  };

  const findEstateHeirs = (estate?: RealEstateDto): HeirDto[] | null => {
    const owners = (estate?.owners ?? []).filter((o) => (o ?? '').trim());
    if (owners.length === 0) return null;

    const result: HeirDto[] = [];
    const seen = new Set<number | string>();
    const add = (heir: HeirDto) => {
      const key = heir.id ?? fullName(heir);
      if (!key || seen.has(key)) return;
      seen.add(key);
      result.push(heir);
    };

    for (const owner of owners) {
      if (doc.borrowerHeirs?.length && owner === fullName(debtor)) {
        doc.borrowerHeirs.forEach(add);
        continue;
      }
      const guarantor = doc.guarantors.find((x) => fullName(x) === owner);
      if (guarantor?.heirs?.length) {
        guarantor.heirs.forEach(add);
        continue;
      }
      [
        ...(doc.borrowerHeirs ?? []).filter((h) => fullName(h) === owner),
        ...doc.guarantors.flatMap((x) =>
          (x.heirs ?? []).filter((h) => fullName(h) === owner),
        ),
      ].forEach(add);
    }

    return result.length > 0 ? result : null;
  };

  const noticePersons: { number: number; heirId?: number; label: string }[] = [];
  let heirKey = 100;
  const pushHeirs = (heirs: HeirDto[] | undefined, deceasedFull: string) => {
    (heirs ?? []).forEach((h) => {
      const name = fullName(h);
      if (!name) return;
      noticePersons.push({
        number: heirKey++,
        heirId: h.id,
        label: `الوريث :  ${name} — إضافة لتركة ${deceasedFull}`,
      });
    });
  };
  if (doc.borrowerName) {
    if (doc.borrowerHeirs && doc.borrowerHeirs.length > 0) {
      pushHeirs(doc.borrowerHeirs, fullName(debtor));
    } else {
      noticePersons.push({
        number: 0,
        label: `المقترض :  ${[doc.borrowerName, doc.borrowerFamily].filter(Boolean).join(' ')}`,
      });
    }
  }
  doc.guarantors.forEach((g) => {
    const role = isOrdinary ? 'منفذ عليه' : 'كفيل';
    const family = g.family ? `  ${g.family}` : '';
    const gFull = fullName(g);
    if (g.heirs && g.heirs.length > 0) {
      pushHeirs(g.heirs, gFull || `${role} ${g.guarantorNumber}`);
    } else {
      noticePersons.push({
        number: g.guarantorNumber,
        label: `${role} ${g.guarantorNumber} :  ${g.name ?? ''}${family}`,
      });
    }
  });
  const selectedEstates = doc.realEstates.filter(
    (r) => r.id !== undefined && estateSel.includes(r.id),
  );
  const estateOwnersKey = (r: RealEstateDto) => [...(r.owners ?? [])].sort().join('|');
  const multiOwner = new Set(selectedEstates.map(estateOwnersKey)).size > 1;

  return (
    <div className="bg-white rounded-xl shadow p-5 mt-6">
      <h3 className="font-bold text-gray-800 mb-3">توليد المستندات التنفيذية</h3>
      {downloadError && <Toast type="error" message={downloadError} onClose={() => setDownloadError('')} />}
      {downloadSuccess && (
        <Toast type="success" message={downloadSuccess} onClose={() => setDownloadSuccess('')} />
      )}

      <div className="space-y-4">
        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <div className="bg-gray-800 text-white px-4 py-2 font-bold">المستندات الأساسية</div>
          {BASIC_DOCS.map((row) => (
            <div
              key={row.code}
              className="flex items-center justify-between gap-3 px-4 py-2 border-b border-gray-100 last:border-0"
            >
              <button
                onClick={() => (row.code === 'PS' ? generateSeizure() : generateBasic(row.code, row.label))}
                disabled={generating !== ''}
                className="bg-gray-800 hover:bg-gray-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {generating === row.code ? 'جارِ التوليد...' : 'توليد'}
              </button>
              <span className="font-bold text-gray-800">{row.label}</span>
            </div>
          ))}
          <div className="px-4 py-3 border-t border-gray-100">
            <p className="text-gray-600 text-sm mb-2">اختر العقارات التي تريد الحجز عليها :</p>
            <EstateSelection estates={doc.realEstates} selected={estateSel} onToggle={toggleEstate} />
          </div>
        </div>

        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <div className="bg-red-800 text-white px-4 py-2 font-bold">إخطار تنفيذي</div>
          <div className="flex flex-col md:flex-row gap-4 p-4">
            <div className="flex flex-col gap-2 md:w-56">
              <button
                onClick={() => generateNotice('003')}
                disabled={generating !== ''}
                className="bg-red-800 hover:bg-red-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {generating === '003' ? 'جارِ التوليد...' : 'توليد إخطار تنفيذي'}
              </button>
              <button
                onClick={() => generateNotice('007')}
                disabled={generating !== ''}
                className="bg-red-800 hover:bg-red-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {generating === '007' ? 'جارِ التوليد...' : 'توليد إخطار بالصحف'}
              </button>
            </div>
            <div className="flex-1">
              <p className="text-gray-600 text-sm">اختر المنفَّذ عليهم الذين تريد تسطير إخطار تنفيذي لهم :</p>
              <div className="mt-2 border border-gray-200 rounded-lg p-3 space-y-2">
                {noticePersons.length === 0 ? (
                  <p className="text-gray-400 text-sm">لا يوجد أشخاص — أدخل المقترض والكفلاء أولاً</p>
                ) : (
                  noticePersons.map((p) => (
                    <label key={p.number} className="inline-flex items-center gap-2 text-sm cursor-pointer min-h-11">
                      <input
                        type="checkbox"
                        checked={noticeSel.includes(p.number)}
                        onChange={() => toggleNotice(p.number)}
                      />
                      {p.label}
                    </label>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <div className="bg-orange-700 text-white px-4 py-2 font-bold">إخطار بيع أموال غير منقولة</div>
          <div className="flex flex-col md:flex-row gap-4 p-4">
            <div className="flex flex-col gap-2 md:w-56">
              <button
                onClick={() => generateEstateNotice('005')}
                disabled={generating !== ''}
                className="bg-orange-700 hover:bg-orange-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {generating === '005' ? 'جارِ التوليد...' : 'توليد إخطار بيع غير منقولة'}
              </button>
              <button
                onClick={() => generateEstateNotice('006')}
                disabled={generating !== ''}
                className="bg-orange-700 hover:bg-orange-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {generating === '006' ? 'جارِ التوليد...' : 'توليد إخطار بيع بالصحف'}
              </button>
            </div>
            <div className="flex-1">
              <p className="text-gray-600 text-sm">
                اختر العقارات التي تريد تسطير إخطار بيع أموال غير منقولة بالنسبة لها{' '}
                <span className="text-red-600 font-bold">(يجب أن تكون لنفس المالك)</span>
              </p>
              <div className="mt-2 border border-gray-200 rounded-lg p-3 space-y-2">
                <EstateSelection estates={doc.realEstates} selected={estateSel} onToggle={toggleEstate} />
                {multiOwner && (
                  <p className="text-red-600 text-xs">⚠️  العقارات لمالكين مختلفين — اختر عقارات مالك واحد فقط</p>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
