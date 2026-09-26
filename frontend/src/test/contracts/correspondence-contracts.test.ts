import { describe, it, expect } from 'vitest';
import manifest from './correspondence-contracts.json';
import type { CorrespondenceDto, CorrespondenceListItemDto } from '../../types';

/**
 * حارس التطابق السلكي C#↔TS عبر البيان المشترك:
 * الحرفيّتان أدناه مكتوبتان ضد `types/index.ts` (أي حقل جديد/محذوف في
 * الواجهة يُفشل الترجمة)، ومفاتيحهما تُقارَن بالبيان الذي يُقارَن بدوره
 * بمفاتيح JSON الفعلية في اختبار تكامل الخلفية — فلا يتمايز الطرفان بصمت.
 */
const listItem: CorrespondenceListItemDto = {
  id: 1,
  correspondenceNumber: 'DAM-2026-1234',
  correspondenceDate: '2026-08-01T09:00:00Z',
  importance: 'urgent',
  documentId: null,
  fileContext: null,
  creatorName: 'المحامي الأول',
  targetName: 'مندوب الجهة',
  snippet: 'نطلب موافاتنا بالبيانات',
  lastKind: 'letter',
  viewStatus: 'pending',
  canMarkSeen: true,
  canReply: true,
  messagesCount: 1,
  administrativeBranchName: null,
  governorate: 'دمشق',
  updatedAt: '2026-08-01T09:00:00Z',
};

const detail: CorrespondenceDto = {
  id: 1,
  correspondenceNumber: 'DAM-2026-1234',
  correspondenceDate: '2026-08-01T09:00:00Z',
  importance: 'urgent',
  documentId: null,
  fileContext: null,
  branchId: null,
  governorate: 'دمشق',
  administrativeBranchName: null,
  creatorId: 3,
  creatorName: 'المحامي الأول',
  creatorRole: 'lawyer',
  targetUserId: 11,
  targetName: 'مندوب الجهة',
  targetRole: 'entitymanager',
  viewStatus: 'pending',
  canMarkSeen: true,
  canReply: true,
  messages: [],
  receipts: [],
  createdAt: '2026-08-01T09:00:00Z',
};

describe('correspondence-contracts', () => {
  it('مفاتيح سطر القائمة تطابق البيان المشترك حرفيًا', () => {
    expect(Object.keys(listItem).sort()).toEqual([...manifest.list].sort());
  });

  it('مفاتيح التفاصيل تطابق البيان المشترك حرفيًا', () => {
    expect(Object.keys(detail).sort()).toEqual([...manifest.detail].sort());
  });
});
