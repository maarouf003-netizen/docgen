import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import MessageComposer from './MessageComposer';
import type { ForumUserLookupDto } from '../../types';

vi.mock('../../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api/client')>();
  return {
    ...original,
    api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

import { api } from '../../api/client';

const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;

const candidate = (overrides: Partial<ForumUserLookupDto> = {}): ForumUserLookupDto => ({
  id: 9,
  name: 'القارئ الثاني',
  role: 'lawyer',
  location: 'دمشق',
  section: null,
  ...overrides,
});

function Harness() {
  const [draft, setDraft] = useState('');
  return (
    <MessageComposer
      quote={null}
      onClearQuote={() => {}}
      draft={draft}
      onDraftChange={setDraft}
      onSend={() => {}}
      sending={false}
      editing={null}
      onCancelEdit={() => {}}
    />
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  apiGet.mockResolvedValue({ data: [candidate()] });
});

describe('MessageComposer mention picker', () => {
  it('يفتح قائمة المرشحين عند @ ويُدرج الرمز القانوني عند الاختيار', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.type(screen.getByLabelText(/نص الرسالة/), '@قا');
    expect(await screen.findByRole('listbox', { name: 'مرشحو الذِكر' })).toBeInTheDocument();
    const option = await screen.findByRole('option', { name: 'ذِكر القارئ الثاني' });
    expect(option).toBeInTheDocument();

    await user.click(within(option).getByRole('button', { name: 'ذِكر القارئ الثاني' }));
    expect(screen.getByLabelText(/نص الرسالة/)).toHaveValue('@[9:القارئ الثاني] ');
  });

  it('يستدعي البحث بالاستعلام بعد @ ويعرض الهوية بالصيغة نفسها', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.type(screen.getByLabelText(/نص الرسالة/), 'مرحبا @دم');
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith(
        '/forum/users/lookup',
        expect.objectContaining({ params: { q: 'دم' } }),
      );
    });
    expect(await screen.findByText('محامي — دمشق')).toBeInTheDocument();
  });

  it('يختار بلوحة المفاتيح (سهم + إدخال) ويغلق بـ Escape', async () => {
    const user = userEvent.setup();
    apiGet.mockResolvedValue({ data: [candidate(), candidate({ id: 10, name: 'ثالث' })] });
    render(<Harness />);
    const field = screen.getByLabelText(/نص الرسالة/);

    await user.type(field, '@');
    expect(await screen.findByRole('listbox', { name: 'مرشحو الذِكر' })).toBeInTheDocument();
    // انتظار المرشحين قبل لوحة المفاتيح (الجلب محاكى لكن غير فوري).
    await screen.findByRole('option', { name: 'ذِكر ثالث' });

    await user.keyboard('{ArrowDown}{Enter}');
    expect(field).toHaveValue('@[10:ثالث] ');
  });

  it('يغلق المنتقي بـ Escape بلا إدراج', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    const field = screen.getByLabelText(/نص الرسالة/);

    await user.type(field, '@قا');
    expect(await screen.findByRole('listbox', { name: 'مرشحو الذِكر' })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(field).toHaveValue('@قا');
  });

  it('لا يفتح المنتقي بلا @ قبل المؤشر', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.type(screen.getByLabelText(/نص الرسالة/), 'نص عادي بلا رمز');
    await waitFor(() => expect(apiGet).not.toHaveBeenCalled());
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });
});
