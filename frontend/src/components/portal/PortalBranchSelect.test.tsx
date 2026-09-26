import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PortalBranchSelect from './PortalBranchSelect';
import type { PortalScopeEntryDto } from '../../types';

const entry = (over: Partial<PortalScopeEntryDto> = {}): PortalScopeEntryDto =>
  ({ id: 11, governorate: 'دمشق', branchName: 'فرع 1', isActive: true, ...over } as PortalScopeEntryDto);

function renderSelect(entries: PortalScopeEntryDto[]) {
  const onChange = vi.fn();
  render(<PortalBranchSelect entries={entries} value="" onChange={onChange} />);
  return { onChange, select: screen.getByLabelText('اختيار الفرع') as HTMLSelectElement };
}

function optionLabels(select: HTMLSelectElement): string[] {
  return [...select.options].map((o) => o.textContent ?? '');
}

describe('PortalBranchSelect', () => {
  it('يعرض خيار «كل الفروع (الإجمالي» أولًا وقيمة فارغة', () => {
    const { select } = renderSelect([entry()]);
    expect(optionLabels(select)[0]).toBe('كل الفروع (الإجمالي)');
    expect(select.options[0].value).toBe('');
  });

  it('يعرض «المحافظة/الفرع» لكل فرع فيخيار واحد', () => {
    const { select } = renderSelect([
      entry({ id: 11, governorate: 'دمشق', branchName: 'فرع 1' }),
      entry({ id: 12, governorate: 'حلب', branchName: 'فرع حلب' }),
    ]);
    expect(optionLabels(select)).toEqual(['كل الفروع (الإجمالي)', 'دمشق/فرع 1', 'حلب/فرع حلب']);
  });

  it('يعرض الجهة الأم بالمحافظة وحدها بلا تكرار فرعي', () => {
    const { select } = renderSelect([entry({ branchName: 'الجهة الأم' })]);
    expect(optionLabels(select)).toEqual(['كل الفروع (الإجمالي)', 'دمشق']);
  });

  it('يقرلم الاسمين فلا تظهر مسافات طرفية في الخيارات', () => {
    const { select } = renderSelect([entry({ governorate: ' دمشق ', branchName: ' فرع 1 ' })]);
    expect(optionLabels(select)).toEqual(['كل الفروع (الإجمالي)', 'دمشق/فرع 1']);
  });

  it('يعرض اسم الفرع وحده حين لا محافظة (بلا «/» معلّقة)', () => {
    const { select } = renderSelect([entry({ governorate: '', branchName: 'فرع 1' })]);
    expect(optionLabels(select)).toEqual(['كل الفروع (الإجمالي)', 'فرع 1']);
  });

  it('يبعّد معرّف الفرع المختار لا نصّه', async () => {
    const user = userEvent.setup();
    const { onChange, select } = renderSelect([
      entry({ id: 11, governorate: 'دمشق', branchName: 'فرع 1' }),
      entry({ id: 12, governorate: 'حلب', branchName: 'فرع حلب' }),
    ]);
    await user.selectOptions(select, '12');
    expect(onChange).toHaveBeenCalledWith('12');
  });

  it('الخيار قابل للوصول باسم دال (sr-only) ولا يعتمد على الموضع', () => {
    renderSelect([entry()]);
    expect(screen.getByLabelText('اختيار الفرع').tagName).toBe('SELECT');
  });
});
