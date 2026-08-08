import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RichTextEditor from './RichTextEditor';

describe('RichTextEditor', () => {
  it('يكتب نصًا ويُصدر HTML مغلفًا بفقرة', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RichTextEditor value="" onChange={onChange} />);

    const editor = await screen.findByLabelText('محرر النص');
    await user.click(editor);
    await user.keyboard('نص عادي');

    expect(onChange).toHaveBeenCalledWith('<p>نص عادي</p>');
  });

  it('يطبّق التنسيق العريض على النص المكتوب', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RichTextEditor value="" onChange={onChange} />);

    const editor = await screen.findByLabelText('محرر النص');
    await user.click(screen.getByRole('button', { name: 'عريض' }));
    await user.type(editor, 'مهم');

    expect(onChange).toHaveBeenCalledWith('<p><strong>مهم</strong></p>');
  });

  it('يطبّق لون النص المختار على النص المكتوب', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RichTextEditor value="" onChange={onChange} />);

    const editor = await screen.findByLabelText('محرر النص');
    await user.click(screen.getByRole('button', { name: 'نص أحمر' }));
    await user.type(editor, 'تنبيه');

    const lastCall = onChange.mock.lastCall?.[0] as string;
    expect(lastCall).toContain('تنبيه');
    expect(lastCall).toMatch(/<span style="color:/);
  });

  it('يمسح التنسيق عند الضغط على مسح التنسيق', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RichTextEditor value="" onChange={onChange} />);

    const editor = await screen.findByLabelText('محرر النص');
    await user.click(screen.getByRole('button', { name: 'عريض' }));
    await user.click(screen.getByRole('button', { name: 'مسح التنسيق' }));
    await user.type(editor, 'عادي');

    expect(onChange).toHaveBeenCalledWith('<p>عادي</p>');
  });

  it('يتراجع عن الكتابة ويعيدها عبر زري التراجع والإعادة', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RichTextEditor value="" onChange={onChange} />);

    const editor = await screen.findByLabelText('محرر النص');
    const undoButton = screen.getByRole('button', { name: 'تراجع' });
    const redoButton = screen.getByRole('button', { name: 'إعادة' });

    expect(undoButton).toBeDisabled();
    expect(redoButton).toBeDisabled();

    await user.click(editor);
    await user.keyboard('نص');
    expect(onChange).toHaveBeenLastCalledWith('<p>نص</p>');
    expect(undoButton).not.toBeDisabled();

    await user.click(undoButton);
    expect(onChange).toHaveBeenLastCalledWith('<p></p>');

    await user.click(redoButton);
    expect(onChange).toHaveBeenLastCalledWith('<p>نص</p>');
  });
});
