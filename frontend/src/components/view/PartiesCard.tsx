import type { ReactNode } from 'react';
import type {
  ApplicantPublicEntityDto,
  DocumentResponse,
  ExecutionApplicantDto,
} from '../../types';
import { isExecutedLike } from '../../utils/documentDisplay';
import {
  applicantNaturalRows,
  buildExecutedHeirLines,
  buildHeirLines,
  executedPersonRows,
  fullName,
  legalPartyRows,
  personRows,
  representationSuffix,
  representativeLine,
  representativeRows,
} from './viewFormat';
import type { PartyModal } from './viewTypes';

/** صف طرف داخل بطاقة الأطراف: تسمية + اسم + سطر فرعي، قابل للنقر عند الحاجة. */
function PartyRow({
  label,
  name,
  subtitle,
  onClick,
}: {
  label: string;
  name: string;
  subtitle?: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className="w-full flex items-center justify-between gap-3 py-2.5 px-2 -mx-2 rounded-lg border-b border-gray-100 last:border-0 text-right hover:bg-gray-50 disabled:hover:bg-transparent min-h-11"
    >
      <span className="min-w-0 flex-1">
        <span className="block text-xs text-gray-500">{label}</span>
        <span className="block font-medium text-gray-800 text-sm">{name}</span>
        {subtitle ? <span className="block text-xs text-gray-500 mt-0.5">{subtitle}</span> : null}
      </span>
      {onClick ? <span className="text-gray-400 text-sm shrink-0" aria-hidden="true">←</span> : null}
    </button>
  );
}

/** مجموعة «طالب التنفيذ» أو «المنفذ عليه» داخل بطاقة الأطراف. */
function PartiesGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="mt-4 first:mt-0">
      <h4 className="font-bold text-gray-700 text-sm mb-2 border-b border-gray-200 pb-2">{title}</h4>
      {children}
    </div>
  );
}

/** تسمية الجهة الطالبة للتنفيذ: «اسم الجهة (الفرع) - محافظة X». */
function applicantEntityName(e: ApplicantPublicEntityDto): string {
  const name = (e.name ?? '').trim();
  const branch = (e.branch ?? '').trim();
  const governorate = (e.governorate ?? '').trim();
  if (!name) return '';
  const core = branch ? `${name} (${branch})` : name;
  return governorate ? `${core} - محافظة ${governorate}` : core;
}

/** صفوف الهوية الكاملة لطرف طبيعي (مقترض/كفيل) في وضع «طالبة تنفيذ» مع الممثل الشرعي. */
function naturalRows(p: {
  name?: string;
  father?: string;
  family?: string;
  mother?: string;
  birth?: string;
  register?: string;
  nationalId?: string;
  addressType?: string;
  address?: string;
  representativeName?: string;
  representativeFather?: string;
  representativeFamily?: string;
  representativeCapacity?: string;
  representativeAddressType?: string;
  representativeAddress?: string;
}) {
  return [...personRows(p), ...representativeRows(p)];
}

/** الجهات العامة طالبة التنفيذ في وضع «طالبة تنفيذ» (اسم + فرع، بلا نقر). */
function ApplicantEntities({ entities }: { entities: ApplicantPublicEntityDto[] }) {
  const visible = entities.filter((e) => (e.name ?? '').trim());
  if (visible.length === 0) {
    return <p className="text-gray-400 text-sm py-2">لا يوجد طالب تنفيذ</p>;
  }
  return (
    <div>
      {visible.map((e, i) => (
        <PartyRow key={e.id ?? i} label={`الجهة ${i + 1}`} name={applicantEntityName(e)} />
      ))}
    </div>
  );
}

/** طالبو التنفيذ في وضع «منفذ عليه» (طبيعي/اعتباري) مع نافذة تفاصيل عند الضغط. */
function ExecutedApplicants({ applicants, onOpen }: { applicants: ExecutionApplicantDto[]; onOpen: (m: PartyModal) => void }) {
  if (applicants.length === 0) {
    return <p className="text-gray-400 text-sm py-2">لا يوجد طالب تنفيذ</p>;
  }
  return (
    <div>
      {applicants.map((a, i) => {
        const full = fullName(a);
        if (a.nature === 'legal') {
          return (
            <PartyRow
              key={a.id ?? i}
              label={`طالب التنفيذ ${i + 1}`}
              name={full || (a.name ?? '') || '—'}
              onClick={() => onOpen({ kind: 'person', title: 'طالب التنفيذ (شخص اعتباري)', rows: legalPartyRows(a) })}
            />
          );
        }
        return (
          <PartyRow
            key={a.id ?? i}
            label={`طالب التنفيذ ${i + 1}`}
            name={full || '—'}
            subtitle={[representationSuffix(a), representativeLine(a)].filter(Boolean).join(' — ')}
            onClick={() => onOpen({ kind: 'person', title: 'طالب التنفيذ', rows: applicantNaturalRows(a) })}
          />
        );
      })}
    </div>
  );
}

/** المنفذ عليهم في وضع «منفذ عليه»: جهات عامة + أشخاص طبيعيون + شخصيات اعتبارية. */
function ExecutedDebtors({
  doc,
  onOpen,
}: {
  doc: DocumentResponse;
  onOpen: (m: PartyModal) => void;
}) {
  const entries: ReactNode[] = [];

  doc.executedPublicEntities.forEach((e, i) => {
    if (e.nature === 'legal') {
      entries.push(
        <PartyRow
          key={`legal-entity-${e.id ?? i}`}
          label="المنفذ عليه"
          name={e.entityName || '—'}
          subtitle={e.governorate ? `محافظة ${e.governorate}` : undefined}
          onClick={() =>
            onOpen({
              kind: 'person',
              title: 'شخص اعتباري',
              rows: legalPartyRows({ name: e.entityName, registrationNumber: e.registrationNumber, representedBy: e.representedBy, addressType: e.addressType, address: e.address, governorate: e.governorate }),
            })
          }
        />,
      );
    } else {
      entries.push(
        <PartyRow
          key={`entity-${e.id ?? i}`}
          label="المنفذ عليه"
          name={`${e.entityName || '—'}${e.entityBranch ? ` (${e.entityBranch})` : ''}`}
          subtitle={e.governorate ? `محافظة ${e.governorate}` : undefined}
          onClick={() => onOpen({ kind: 'entity', name: e.entityName ?? '', branch: e.entityBranch ?? '', governorate: e.governorate })}
        />,
      );
    }
  });

  doc.executedNaturalPersons.forEach((p, i) => {
    const full = fullName(p);
    const deceased = fullName({ name: p.deceasedName, father: p.deceasedFather, family: p.deceasedFamily }) || full;
    const heirs = (p.heirs ?? []).filter((h) => (h.heirName ?? '').trim());
    if (p.representationType === 'إضافة لتركة' && heirs.length > 0) {
      entries.push(
        <PartyRow
          key={`person-heirs-${p.id ?? i}`}
          label="المنفذ عليه"
          name={`ورثة المتوفى (${deceased})`}
          onClick={() => onOpen({ kind: 'heirs', deceasedName: deceased, lines: buildExecutedHeirLines(heirs) })}
        />,
      );
    } else {
      entries.push(
        <PartyRow
          key={`person-${p.id ?? i}`}
          label="المنفذ عليه"
          name={full || '—'}
          subtitle={[representationSuffix(p), representativeLine(p)].filter(Boolean).join(' — ')}
          onClick={() => onOpen({ kind: 'person', title: 'شخص طبيعي', rows: executedPersonRows(p) })}
        />,
      );
    }
  });

  if (entries.length === 0) {
    return <p className="text-gray-400 text-sm py-2">لا يوجد منفذ عليه</p>;
  }
  return <div>{entries}</div>;
}

/** المنفذ عليهم في وضع «طالبة تنفيذ»: المقترض/المنفذ عليه والكفلاء (مصرفي) أو المقترض والكفلاء (عادي). */
function ApplicantSideDebtors({
  doc,
  isOrdinary,
  onOpen,
}: {
  doc: DocumentResponse;
  isOrdinary: boolean;
  onOpen: (m: PartyModal) => void;
}) {
  const rows: ReactNode[] = [];
  const borrowerFull = fullName({ name: doc.borrowerName, father: doc.borrowerFather, family: doc.borrowerFamily });
  const borrowerTitle = isOrdinary ? 'منفذ عليه' : 'مقترض';
  const borrowerHeirs = (doc.borrowerHeirs ?? []).filter((h) => (h.name ?? '').trim());
  const borrowerDeceased = borrowerFull;

  if (borrowerHeirs.length > 0) {
    rows.push(
      <PartyRow
        key="borrower-heirs"
        label={borrowerTitle}
        name={`ورثة المتوفى (${borrowerDeceased})`}
        onClick={() =>
          onOpen({
            kind: 'heirs',
            deceasedName: borrowerDeceased,
            lines: buildHeirLines(borrowerHeirs),
          })
        }
      />,
    );
  } else if (borrowerFull) {
    rows.push(
      <PartyRow
        key="borrower"
        label={borrowerTitle}
        name={borrowerFull}
        subtitle={
          doc.borrowerNature === 'legal'
            ? undefined
            : representativeLine({
                representativeName: doc.borrowerRepresentativeName,
                representativeFather: doc.borrowerRepresentativeFather,
                representativeFamily: doc.borrowerRepresentativeFamily,
                representativeCapacity: doc.borrowerRepresentativeCapacity,
              }) || undefined
        }
        onClick={() =>
          onOpen({
            kind: 'person',
            title: borrowerTitle,
            rows:
              doc.borrowerNature === 'legal'
                ? legalPartyRows({ name: doc.borrowerName, registrationNumber: doc.borrowerRegistrationNumber, representedBy: doc.borrowerRepresentedBy, addressType: doc.borrowerAddressType, address: doc.borrowerAddress })
                : naturalRows({
                    name: doc.borrowerName,
                    father: doc.borrowerFather,
                    family: doc.borrowerFamily,
                    mother: doc.borrowerMother,
                    birth: doc.borrowerBirth,
                    register: doc.borrowerRegister,
                    nationalId: doc.borrowerNationalId,
                    addressType: doc.borrowerAddressType,
                    address: doc.borrowerAddress,
                    representativeName: doc.borrowerRepresentativeName,
                    representativeFather: doc.borrowerRepresentativeFather,
                    representativeFamily: doc.borrowerRepresentativeFamily,
                    representativeCapacity: doc.borrowerRepresentativeCapacity,
                    representativeAddressType: doc.borrowerRepresentativeAddressType,
                    representativeAddress: doc.borrowerRepresentativeAddress,
                  }),
          })
        }
      />,
    );
  }

  doc.guarantors.forEach((g, i) => {
    const gFull = fullName(g);
    if (!gFull) return;
    const role = isOrdinary ? 'منفذ عليه' : 'كفيل';
    const n = isOrdinary ? i + 2 : (g.guarantorNumber ?? i + 1);
    const gHeirs = (g.heirs ?? []).filter((h) => (h.name ?? '').trim());
    if (gHeirs.length > 0) {
      rows.push(
        <PartyRow
          key={`guarantor-heirs-${g.id ?? i}`}
          label={`${role} ${n}`}
          name={`ورثة المتوفى (${gFull})`}
          onClick={() =>
            onOpen({
              kind: 'heirs',
              deceasedName: gFull,
              lines: buildHeirLines(gHeirs),
            })
          }
        />,
      );
    } else {
      rows.push(
        <PartyRow
          key={`guarantor-${g.id ?? i}`}
          label={`${role} ${n}`}
          name={gFull}
          onClick={() =>
            onOpen({
              kind: 'person',
              title: `${role} ${n}`,
              rows: g.nature === 'legal' ? legalPartyRows(g) : naturalRows(g),
            })
          }
        />,
      );
    }
  });

  if (rows.length === 0) {
    return <p className="text-gray-400 text-sm py-2">لا يوجد منفذ عليه</p>;
  }
  return <div>{rows}</div>;
}

/** بطاقة «أطراف الملف التنفيذي» الموحّدة: طالب التنفيذ + المنفذ عليه لكل الصفات. */
export function PartiesCard({ doc, onOpen }: { doc: DocumentResponse; onOpen: (m: PartyModal) => void }) {
  const isExecuted = isExecutedLike(doc.generalEntitySide);
  const isOrdinary = doc.contractTypeSelector === 'عادي';

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4">
      <h3 className="font-bold text-emerald-800 mb-3">أطراف الملف التنفيذي</h3>
      <PartiesGroup title={doc.generalEntitySide === 'deposit' ? 'طالب العرض' : 'طالب التنفيذ'}>
        {isExecuted ? (
          <ExecutedApplicants applicants={doc.executionApplicants} onOpen={onOpen} />
        ) : (
          <ApplicantEntities entities={doc.applicantPublicEntities ?? []} />
        )}
      </PartiesGroup>
      <PartiesGroup title="المنفذ عليه">
        {isExecuted ? (
          <ExecutedDebtors doc={doc} onOpen={onOpen} />
        ) : (
          <ApplicantSideDebtors doc={doc} isOrdinary={isOrdinary} onOpen={onOpen} />
        )}
      </PartiesGroup>
    </div>
  );
}
