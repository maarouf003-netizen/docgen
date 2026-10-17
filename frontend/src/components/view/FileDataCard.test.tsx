import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FileDataCard } from './FileDataCard';
import type { DocumentOccurrenceDto, DocumentResponse } from '../../types';

function makeDoc(occurrences: DocumentOccurrenceDto[]): DocumentResponse {
  return {
    id: 1,
    court: 'محكمة دمشق',
    fileNumber: '101',
    displayFileYear: '2026',
    occurrences,
  } as unknown as DocumentResponse;
}

function moved(occurrenceType: 'source-file-moved' | 'mirror-file-moved', id: number): DocumentOccurrenceDto {
  return {
    id,
    occurrenceType,
    occurrenceTypeLabel: occurrenceType === 'source-file-moved' ? 'نقل الملف المنيب' : 'نقل الملف المناب',
    fileNumber: '66',
    fileType: 'صلح',
    year: 2026,
    fromCircuitName: 'دائرة المصدر',
    toCircuitName: 'دائرة الوجهة',
  };
}

const baseProps = {
  isLawyer: false,
  showBranch: true,
  showLawyer: false,
  canViewChanges: false,
  onOpenBaseNumbers: vi.fn(),
  onOpenAssignments: vi.fn(),
  onOpenChanges: vi.fn(),
};

describe('FileDataCard (عزل مستهلكي circuit-referred — C2-ب)', () => {
  it('يعرض «الرقم السابق قبل النقل» من وقعة circuit-referred كما هو', () => {
    render(
      <FileDataCard
        {...baseProps}
        doc={makeDoc([
          {
            id: 1,
            occurrenceType: 'circuit-referred',
            occurrenceTypeLabel: 'إحالة نقل دائرة',
            fileNumber: '66',
            fileType: 'صلح',
            year: 2026,
            fromCircuitName: 'دائرة المصدر',
            toCircuitName: 'دائرة الوجهة',
          },
        ])}
      />,
    );
    expect(screen.getByText(/الرقم السابق قبل النقل: 66 صلح/)).toBeInTheDocument();
  });

  it('لا يعرض «الرقم السابق قبل النقل» بأي من النوعين الجديدين (الفلتر الحرفي لا يتأثر)', () => {
    render(
      <FileDataCard {...baseProps} doc={makeDoc([moved('source-file-moved', 2), moved('mirror-file-moved', 3)])} />,
    );
    expect(screen.queryByText(/الرقم السابق قبل النقل/)).not.toBeInTheDocument();
  });
});
