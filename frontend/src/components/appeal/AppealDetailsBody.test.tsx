import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import AppealDetailsBody from './AppealDetailsBody';
import { makeDocument } from '../../test/factories';
import type { AppealDto } from '../../types';

function makeAppeal(overrides: Partial<AppealDto> = {}): AppealDto {
  const doc = makeDocument();
  return {
    id: 1,
    documentId: doc.id,
    documentLabel: 'أحمد خالد الخطيب',
    fileNumber: doc.fileNumber,
    fileType: doc.fileType,
    fileYear: doc.fileYear,
    court: doc.court,
    direction: 'appellants',
    directionLabel: 'مستأنِفين',
    status: 'pending',
    statusLabel: 'منظور',
    appellants: [{ kind: 'applicant-entity', partyId: 1, name: 'المؤسسة العامة للكهرباء' }],
    appellees: [{ kind: 'borrower', partyId: 9, name: 'أحمد خالد الخطيب' }],
    appealedDecisionText: 'نص القرار',
    depositBookNumber: 'K-9',
    depositBookDate: '2026-08-02',
    needsRotation: false,
    createdAt: '2026-08-01T00:00:00Z',
    createdById: 55,
    ...overrides,
  };
}

describe('AppealDetailsBody', () => {
  it('يخفي خلايا كتاب الإيداع في مسار «مستأنِفين» ويُبقي حقول المطالعة', () => {
    render(<AppealDetailsBody appeal={makeAppeal({ direction: 'appellants' })} />);

    expect(screen.queryByText('رقم كتاب إيداع الملف رئيس القسم')).not.toBeInTheDocument();
    expect(screen.queryByText('تاريخ كتاب إيداع الملف رئيس القسم')).not.toBeInTheDocument();
    expect(screen.queryByText('K-9')).not.toBeInTheDocument();
    expect(screen.getByText('رقم كتاب المطالعة وإيداع الملف رئيس القسم')).toBeInTheDocument();
  });

  it('يعرض خلايا كتاب الإيداع في مسار «مستأنف علينا» فقط', () => {
    render(
      <AppealDetailsBody
        appeal={makeAppeal({ direction: 'against-us', directionLabel: 'مستأنف علينا' })}
      />,
    );

    expect(screen.getByText('رقم كتاب إيداع الملف رئيس القسم')).toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب إيداع الملف رئيس القسم')).toBeInTheDocument();
    expect(screen.getByText('K-9')).toBeInTheDocument();
    expect(screen.getByText('رقم ورود سند تبليغ الاستئناف')).toBeInTheDocument();
  });

  it('hideOpinion يخفي «رأي المحامي المتابع» وحده ويُبقي اسم المحامي وسطر «سطّره» (ق9/ق10)', () => {
    const appeal = makeAppeal({
      direction: 'against-us',
      directionLabel: 'مستأنف علينا',
      defenseOpinion: 'رأي سري داخلي',
      assignedLawyerName: 'المحامي سامر',
      createdByName: 'المدخل',
    });
    render(<AppealDetailsBody appeal={appeal} hideOpinion />);

    expect(screen.queryByText('رأي المحامي المتابع بأسباب الاستئناف')).not.toBeInTheDocument();
    expect(screen.queryByText('رأي سري داخلي')).not.toBeInTheDocument();
    // ق9: اسم المحامي يظهر؛ ق10: سطر «سطّره: createdByName» يبقى.
    expect(screen.getByText('المحامي المتابع')).toBeInTheDocument();
    expect(screen.getByText(/المحامي سامر/)).toBeInTheDocument();
    expect(screen.getByText(/سطّره: المدخل/)).toBeInTheDocument();
  });

  it('بدون hideOpinion يُعرض «رأي المحامي المتابع» كالمعتاد (الداخلية)', () => {
    const appeal = makeAppeal({
      direction: 'against-us',
      directionLabel: 'مستأنف علينا',
      defenseOpinion: 'رأي سري داخلي',
    });
    render(<AppealDetailsBody appeal={appeal} />);

    expect(screen.getByText('رأي المحامي المتابع بأسباب الاستئناف')).toBeInTheDocument();
    expect(screen.getByText('رأي سري داخلي')).toBeInTheDocument();
  });
});
