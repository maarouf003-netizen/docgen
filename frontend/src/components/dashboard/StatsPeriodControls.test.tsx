import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PeriodScopeGroup, PeriodSelect } from './StatsPeriodControls';
import type { PeriodOption } from './dashboardTypes';

const OPTIONS: PeriodOption[] = [
  { value: '2026', label: 'السنة 2026', year: 2026 },
  { value: '2026-8', label: 'آب 2026', year: 2026, month: 8 },
];

describe('PeriodScopeGroup', () => {
  it('يعرض أزرار النطاق الثلاثة ويستدعي التغيير بالمفتاح', async () => {
    const user = userEvent.setup();
    const onPeriodChange = vi.fn();
    render(<PeriodScopeGroup period="monthly" onPeriodChange={onPeriodChange} />);

    const group = screen.getByRole('group', { name: 'نطاق الفترة' });
    expect(group).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'سنوي' }));
    expect(onPeriodChange).toHaveBeenCalledWith('yearly');
  });
});

describe('PeriodSelect', () => {
  it('يعرض الخيارات ويمرر التحديد الكامل عند التغيير', async () => {
    const user = userEvent.setup();
    const onSelectionChange = vi.fn();
    render(<PeriodSelect options={OPTIONS} selectedValue="2026" onSelectionChange={onSelectionChange} />);

    const select = screen.getByLabelText('الفترة');
    await user.selectOptions(select, '2026-8');
    expect(onSelectionChange).toHaveBeenCalledWith({ year: 2026, month: 8, quarter: undefined });
  });

  it('يعرض عبارة فارغة بلا خيارات', () => {
    render(<PeriodSelect options={[]} selectedValue="" onSelectionChange={vi.fn()} />);
    expect(screen.getByText('لا توجد فترات مسجلة')).toBeInTheDocument();
  });
});
