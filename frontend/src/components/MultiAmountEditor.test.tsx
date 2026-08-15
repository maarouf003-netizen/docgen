import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import MultiAmountEditor from './MultiAmountEditor';

describe('MultiAmountEditor', () => {
  it('يفقد حقل المبلغ التركيز عند تمرير عجلة الفأرة فلا تتغير قيمته', () => {
    const onSet = vi.fn();
    render(
      <MultiAmountEditor
        idPrefix="test"
        amountKeys={['a1']}
        currencyKeys={['c1']}
        values={{ a1: 100, c1: 'ليرة سورية' }}
        onSet={onSet}
        slots={1}
        onSlotsChange={vi.fn()}
        firstLabel="المبلغ"
        otherLabel={() => 'مبلغ آخر'}
      />,
    );

    const input = screen.getByLabelText('المبلغ') as HTMLInputElement;
    input.focus();
    expect(document.activeElement).toBe(input);

    fireEvent.wheel(input);

    expect(document.activeElement).not.toBe(input);
    expect(onSet).not.toHaveBeenCalled();
  });

  it('يضع زر «مبلغ آخر» في نفس سطر حقل المبلغ وحقل العملة', () => {
    render(
      <MultiAmountEditor
        idPrefix="test"
        amountKeys={['a1']}
        currencyKeys={['c1']}
        values={{ a1: 100, c1: 'ليرة سورية' }}
        onSet={vi.fn()}
        slots={1}
        onSlotsChange={vi.fn()}
        firstLabel="المبلغ المطالب به"
        otherLabel={() => 'مبلغ آخر'}
      />,
    );

    const addButton = screen.getByRole('button', { name: '➕ مبلغ آخر' });
    const amountField = screen.getByLabelText('المبلغ المطالب به');
    const currencyField = screen.getByLabelText('العملة');
    expect(addButton.closest('.grid')).toBe(amountField.closest('.grid'));
    expect(addButton.closest('.grid')).toBe(currencyField.closest('.grid'));
  });
});
