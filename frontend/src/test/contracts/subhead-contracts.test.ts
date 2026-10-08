import { describe, it, expect } from 'vitest';
import manifest from './subhead-contracts.json';
import type { UserDto, AppealDto, DelegationDto, CircuitStatsDto } from '../../types';

/**
 * حارس التطابق السلكي C#↔TS لعقود رئيس الشعبة عبر البيان المشترك:
 * الحرفيات الأربع مكتوبة ضد `types/index.ts` (أي حقل جديد/محذوف في
 * الواجهة يُفشل الترجمة)، ومفاتيحها تُقارَن بالبيان الذي تُقارَن مفاتيحه
 * بدورها بأعضاء C# في `SubHeadContractTests` — فلا يتمايز الطرفان بصمت
 * (قرار §2/حارس 8: `correspondence-contracts.json` + الحرفيات + `DetailMembers`).
 */
const userAuth: UserDto = {
  id: 1,
  username: 'sub_masyaf',
  fullName: 'رئيس شعبة مصياف',
  role: 'subhead',
  branchId: 4,
  branchName: 'فرع حماة',
  sectionId: 3,
  sectionName: 'شعبة مصياف',
};

const appeal: AppealDto = {
  id: 7,
  documentId: 10,
  documentLabel: 'ملف 10',
  fileNumber: '520',
  fileType: 'تنفيذي',
  fileYear: '2024',
  court: 'دائرة تنفيذ مصياف',
  direction: 'appellants',
  directionLabel: 'المستأنفون',
  status: 'pending',
  statusLabel: 'منظور',
  appealTypeLabel: 'استئناف قرار',
  appellants: [{ kind: 'person', partyId: 1, name: 'أحمد' }],
  appellees: [],
  appealedDecisionText: 'قرار',
  appealedDecisionSummary: 'ملخص',
  appealedDecisionDate: '2026-01-01',
  inspectionBookNumber: '1',
  inspectionBookDate: '2026-01-02',
  groundsSummary: 'أسباب',
  noticeNumber: '2',
  noticeDate: '2026-01-03',
  appellateCourt: 'محكمة الاستئناف',
  appealBaseNumber: '77',
  appealYear: '2026',
  depositBookNumber: '3',
  depositBookDate: '2026-01-04',
  defenseOpinion: 'رأي',
  registrationDate: '2026-01-05',
  decisionNumber: '4',
  decisionDate: '2026-02-01',
  decisionRuling: 'الحكم',
  outcome: 'in-favor',
  outcomeLabel: 'للصالح',
  struckOffDate: '2026-03-01',
  struckOffDecisionNumber: '5',
  notes: 'ملاحظات',
  needsRotation: false,
  currentBaseNumber: '77/2026',
  assignedLawyerId: 3,
  assignedLawyerName: 'محامي دمشق',
  createdAt: '2026-08-01T09:00:00Z',
  createdByName: 'محامي دمشق',
  createdById: 3,
  documentEffectiveNumber: '77',
  documentEffectiveYear: '2026',
  partiesDegraded: false,
  forwardState: 'Owned',
  sectionId: 3,
  executionCircuitId: 11,
  version: 1,
};

const delegation: DelegationDto = {
  id: 9,
  sourceDocumentId: 10,
  sourceDocumentLabel: 'ملف 10',
  sourceFileNumber: '520',
  sourceFileYear: '2024',
  targetDocumentId: null,
  delegatedCourt: 'دائرة تنفيذ مصياف',
  delegatedCircuitId: 11,
  isExternal: false,
  externalBranchId: null,
  externalBranchName: null,
  delegationDate: '2026-08-01',
  delegationText: 'نص الإنابة',
  depositBookNumber: '1',
  depositBookDate: '2026-08-02',
  assignedLawyerId: null,
  assignedLawyerName: null,
  returnDate: '',
  status: 'بانتظار رئيس القسم',
  createdAt: '2026-08-01T09:00:00Z',
  createdByName: 'سامر',
  createdById: 7,
  assets: [],
  saleCoversFullDebt: null,
  targetFileNumber: null,
  targetFileYear: null,
  sourceFileType: 'تنفيذي',
  targetExecStatus: null,
  sourceCourt: 'دائرة تنفيذ حماة',
  blocksAssets: true,
  targetTerminal: false,
  rejectReason: null,
  redirectedToSectionId: null,
  redirectedToSectionName: null,
  targetBranchId: null,
  targetBranchName: null,
  targetLawyerName: null,
  version: 1,
};

const circuitStats: CircuitStatsDto = {
  circuitId: 11,
  circuitName: 'دائرة مصياف',
  branchId: 4,
  branchName: 'فرع حماة',
  isActive: true,
  fileCount: 5,
  lawyerCount: 2,
  pendingCount: 1,
  sectionId: 3,
  sectionName: 'شعبة مصياف',
  version: 2,
};

describe('subhead-contracts', () => {
  it('مفاتيح هوية المستخدم تطابق البيان حرفيًا', () => {
    expect(Object.keys(userAuth).sort()).toEqual([...manifest.userAuth].sort());
  });

  it('مفاتيح الاستئناف تطابق البيان حرفيًا', () => {
    expect(Object.keys(appeal).sort()).toEqual([...manifest.appeal].sort());
  });

  it('مفاتيح الإنابة تطابق البيان حرفيًا', () => {
    expect(Object.keys(delegation).sort()).toEqual([...manifest.delegation].sort());
  });

  it('مفاتيح إحصاء الدوائر تطابق البيان حرفيًا', () => {
    expect(Object.keys(circuitStats).sort()).toEqual([...manifest.circuitStats].sort());
  });
});
