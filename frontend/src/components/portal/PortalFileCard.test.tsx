import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import PortalFileCard from './PortalFileCard';

const baseFile = {
  id: 7,
  documentType: 'سند دين',
  isDraft: false,
  borrowerName: 'أحمد',
  borrowerFather: 'خالد',
  borrowerFamily: 'الخطيب',
  applicant: null,
  executedEntitiesSummary: '',
  amountNumeric: 0,
  currency: null,
  execStatus: null,
  createdAt: '2026-08-01',
  updatedAt: '2026-08-20',
  fileType: 'سند مصارف',
  court: 'دائرة تنفيذ اللاذقية',
  displayBaseNumber: '1500',
  displayBaseYear: '2026',
  matchedEntries: [{ id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true }],
};

function renderCard(file: Record<string, unknown> = {}) {
  return render(
    <MemoryRouter>
      <ul><PortalFileCard file={{ ...baseFile, ...file } as never} canonicalName="المصرف التجاري السوري" /></ul>
    </MemoryRouter>,
  );
}

describe('PortalFileCard', () => {
  it('يعرض الثلاثي وتحته الفرع وشارة متداول ونهاية الشريط أساس/نوع/دائرة', () => {
    renderCard();
    expect(screen.getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText(/المصرف التجاري السوري · اللاذقية\/فرع 1/)).toBeInTheDocument();
    expect(screen.getByText('متداول')).toBeInTheDocument();
    expect(screen.getByText('رقم الأساس: 1500 لعام 2026')).toBeInTheDocument();
    expect(screen.getByText('نوع الملف: سند مصارف')).toBeInTheDocument();
    expect(screen.getByText('دائرة التنفيذ المختصة: دائرة تنفيذ اللاذقية')).toBeInTheDocument();
  });

  it('يخفي الدائرة عند فراغها ولا يترك أثرًا', () => {
    renderCard({ court: null });
    expect(screen.queryByText(/دائرة التنفيذ المختصة/)).not.toBeInTheDocument();
    expect(screen.getByText('رقم الأساس: 1500 لعام 2026')).toBeInTheDocument();
  });

  it('يخفي الدائرة البيضاء (مسافات فقط) تمامًا', () => {
    renderCard({ court: '   ' });
    expect(screen.queryByText(/دائرة التنفيذ المختصة/)).not.toBeInTheDocument();
  });

  it('يسقط إلى DocumentType عند فراغ FileType (لا بطاقة بلا نوع)', () => {
    renderCard({ fileType: null });
    expect(screen.getByText('نوع الملف: سند دين')).toBeInTheDocument();
  });

  it('يجمع جهتين مطابفتين بـ +n عند التعدد', () => {
    renderCard({
      matchedEntries: [
        { id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true },
        { id: 12, governorate: 'دمشق', branchName: 'الفرع الرئيسي', isActive: true },
        { id: 13, governorate: 'حلب', branchName: 'فرع حلب', isActive: true },
      ],
    });
    expect(screen.getByText(/\+1/)).toBeInTheDocument();
  });

  it('يعرض الجهة الأم بلا لاحقة فرعية', () => {
    renderCard({ matchedEntries: [{ id: 11, governorate: 'دمشق', branchName: 'الجهة الأم', isActive: true }] });
    expect(screen.getByText(/المصرف التجاري السوري · دمشق$/)).toBeInTheDocument();
  });

  it('الشارة من displayStatus لا من الخام', () => {
    renderCard({ execStatus: 'منفذ جبريا', displayStatus: 'منفذ' });
    const badge = screen.getByText('منفذ');
    expect(badge.className).toContain('bg-sky-100');
    expect(badge.className).not.toContain('bg-emerald-100');
  });

  it('شارة الجزئي مميزة عن المتداول', () => {
    renderCard({ execStatus: 'منفذ جبريا', displayStatus: 'متداول / منفذ جزئيا' });
    expect(screen.getByText('متداول / منفذ جزئيا').className).toContain('bg-cyan-100');
  });

  it('شارة المسترد مستقلة رغم احتسابه منفذا', () => {
    renderCard({ execStatus: 'مسترد', displayStatus: 'مسترد' });
    expect(screen.getByText('مسترد').className).toContain('bg-fuchsia-100');
  });

  it('احتياط الحمولات القديمة بلا displayStatus', () => {
    renderCard({ execStatus: 'تريث', displayStatus: null });
    expect(screen.getByText('تريث').className).toContain('bg-amber-100');
  });
});
