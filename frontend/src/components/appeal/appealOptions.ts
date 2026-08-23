import { isExecutedLike } from '../../utils/documentDisplay';
import { APPEAL_DIRECTION_APPELLANTS } from '../../utils/appealStatus';
import type { AppealDirection, DocumentResponse } from '../../types';

/** خيار المستأنف المحسوب من أطراف الملف الأساس. */
export interface AppellantOption {
  kind: string;
  partyId: number;
  name: string;
}

function triple(...parts: Array<string | null | undefined>): string {
  return parts
    .map((p) => p?.trim())
    .filter((p): p is string => Boolean(p))
    .join(' ');
}

/**
 * سجل كامل لأطراف الملف من الجهتين — أساس خانة «المستأنف عليهم»:
 * كل أطراف الملف ناقص المختارين ضمن المستأنف (مواجهة الجميع حكمًا).
 */
export function buildAllParties(doc: DocumentResponse): AppellantOption[] {
  const executed = isExecutedLike(doc.generalEntitySide);
  if (executed) {
    const naturalPersons = doc.executedNaturalPersons ?? [];
    return [
      ...(doc.executionApplicants ?? []).map((a) => ({
        kind: 'execution-applicant',
        partyId: a.id ?? 0,
        name: triple(a.name, a.father, a.family) || a.legalRepresentative || '—',
      })),
      ...naturalPersons.map((p) => ({
        kind: 'executed-natural',
        partyId: p.id ?? 0,
        name: triple(p.name, p.father, p.family),
      })),
      // الورثة كما في الخلفية: متداخلون تحت طالبي التنفيذ والطبيعيين، والجمع كامل.
      ...(doc.executionApplicants ?? []).flatMap((a) =>
        (a.heirs ?? []).map((h) => ({
          kind: 'executed-heir',
          partyId: h.id ?? 0,
          name: triple(h.heirName, h.heirFather, h.heirFamily),
        })),
      ),
      ...naturalPersons.flatMap((p) =>
        (p.heirs ?? []).map((h) => ({
          kind: 'executed-heir',
          partyId: h.id ?? 0,
          name: triple(h.heirName, h.heirFather, h.heirFamily),
        })),
      ),
      ...(doc.executedPublicEntities ?? []).map((e) => ({
        kind: 'executed-public',
        partyId: e.id ?? 0,
        name: e.entityName ?? '—',
      })),
    ];
  }
  return [
    ...(doc.applicantPublicEntities ?? []).map((e) => ({
      kind: 'applicant-entity',
      partyId: e.id ?? 0,
      name: e.name ?? '—',
    })),
    {
      kind: 'borrower',
      partyId: doc.id,
      name:
        triple(doc.borrowerName, doc.borrowerFather, doc.borrowerFamily) ||
        doc.borrowerRepresentativeName ||
        'المقترض',
    },
    ...(doc.guarantors ?? []).map((g) => ({
      kind: 'guarantor',
      partyId: g.id ?? 0,
      name: triple(g.name, g.father, g.family),
    })),
    ...(doc.borrowerHeirs ?? []).map((h) => ({
      kind: 'heir',
      partyId: h.id ?? 0,
      name: triple(h.name, h.father, h.family),
    })),
  ];
}

/**
 * خيارات «المستأنف» بحسب الاتجاه وصفة الملف — مرآة لمنطق الخلفية BuildOptions:
 * مستأنِفين ← الجهات العامة طالبة التنفيذ فقط؛
 * مستأنف علينا ← المنفذ عليهم (طبيعيون وورثتهم وجهات) في وضع «منفذ عليه»،
 * والمقترض والكفلاء وورثته في وضع «الجهة العامة طالبة تنفيذ».
 */
export function buildAppellantOptions(doc: DocumentResponse, direction: AppealDirection): AppellantOption[] {
  const executed = isExecutedLike(doc.generalEntitySide);
  if (direction === APPEAL_DIRECTION_APPELLANTS) {
    if (executed) {
      return (doc.executionApplicants ?? []).map((a) => ({
        kind: 'execution-applicant',
        partyId: a.id ?? 0,
        name: triple(a.name, a.father, a.family) || a.legalRepresentative || '—',
      }));
    }
    return (doc.applicantPublicEntities ?? []).map((e) => ({
      kind: 'applicant-entity',
      partyId: e.id ?? 0,
      name: e.name ?? '—',
    }));
  }
  if (executed) {
    // ترتيب مرآة للخلفية: الطبيعيون ثم الجهات المنفذ عليها ثم كل الورثة
    // (الورثة في الاستجابة متداخلون تحت طالبي التنفيذ والطبيعيين، والخلفية
    // تبني خياراتها من الجمع الكامل لهم).
    const naturalPersons = doc.executedNaturalPersons ?? [];
    const applicants = doc.executionApplicants ?? [];
    const heirOption = (h: { id?: number; heirName?: string; heirFather?: string; heirFamily?: string }) => ({
      kind: 'executed-heir',
      partyId: h.id ?? 0,
      name: triple(h.heirName, h.heirFather, h.heirFamily),
    });
    return [
      ...naturalPersons.map((p) => ({
        kind: 'executed-natural',
        partyId: p.id ?? 0,
        name: triple(p.name, p.father, p.family),
      })),
      ...(doc.executedPublicEntities ?? []).map((e) => ({
        kind: 'executed-public',
        partyId: e.id ?? 0,
        name: e.entityName ?? '—',
      })),
      ...applicants.flatMap((a) => (a.heirs ?? []).map(heirOption)),
      ...naturalPersons.flatMap((p) => (p.heirs ?? []).map(heirOption)),
    ];
  }
  return [
    {
      kind: 'borrower',
      partyId: doc.id,
      name:
        triple(doc.borrowerName, doc.borrowerFather, doc.borrowerFamily) ||
        doc.borrowerRepresentativeName ||
        'المقترض',
    },
    ...(doc.guarantors ?? []).map((g) => ({
      kind: 'guarantor',
      partyId: g.id ?? 0,
      name: triple(g.name, g.father, g.family),
    })),
    ...(doc.borrowerHeirs ?? []).map((h) => ({
      kind: 'heir',
      partyId: h.id ?? 0,
      name: triple(h.name, h.father, h.family),
    })),
  ];
}
