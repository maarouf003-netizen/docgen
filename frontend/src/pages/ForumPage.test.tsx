import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import ForumPage from './ForumPage';
import type { ForumMessageDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
  };
});

import { api } from '../api/client';

const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;
const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
const apiPut = api.put as unknown as ReturnType<typeof vi.fn>;

function message(overrides: Partial<ForumMessageDto> = {}): ForumMessageDto {
  return {
    id: 1,
    body: 'مرحبا بالمنتدى',
    authorId: 3,
    authorName: 'المحامي الأول',
    authorRole: 'lawyer',
    authorLocation: 'اللاذقية',
    authorSection: null,
    isPinned: false,
    quotedMessageId: null,
    quotedAuthorName: null,
    quotedExcerpt: null,
    createdAt: new Date(Date.now() - 5 * 60_000).toISOString(),
    editedById: null,
    editedAtUtc: null,
    readCount: 0,
    ...overrides,
  };
}

function mockStream(items: ForumMessageDto[] = [message()], pinned: ForumMessageDto | '' = '') {
  apiGet.mockImplementation((url: string) => {
    if (url === '/forum/messages') return Promise.resolve({ data: { items, hasOlder: false } });
    if (url === '/forum/pinned') return Promise.resolve({ data: pinned });
    if (url.includes('/readers'))
      return Promise.resolve({ data: [{ userId: 9, userName: 'القارئ', readAtUtc: new Date().toISOString() }] });
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
  apiPost.mockImplementation((url: string) => {
    if (url === '/forum/read') return Promise.resolve({ data: { maxReadMessageId: 1 } });
    return Promise.reject(new Error(`unexpected POST ${url}`));
  });
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ForumPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  useAuthMock.mockReturnValue({ user: { id: 3, role: 'lawyer', fullName: 'المحامي الأول' } });
  window.scrollTo = vi.fn() as unknown as typeof window.scrollTo;
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
});

/**
 * تنبيه ترتيب: `userEvent.setup()` يزرع `navigator.clipboard` خاصًا به عند غيابه،
 * فيُزرع المزيف بعده لا في `beforeEach` — وإلا استُبدل بغير جاسوس.
 */
function mockClipboard() {
  const writeText = vi.fn().mockResolvedValue(undefined);
  Object.defineProperty(window.navigator, 'clipboard', {
    value: { writeText },
    configurable: true,
  });
  return writeText;
}

describe('ForumPage', () => {
  it('يعرض التيار بسطر الهوية الكامل والطابع النسبي', async () => {
    mockStream();
    renderPage();

    expect(await screen.findByText('المحامي الأول — محامي — اللاذقية')).toBeInTheDocument();
    expect(screen.getByText('مرحبا بالمنتدى')).toBeInTheDocument();
    expect(screen.getByText('منذ 5 دقائق')).toBeInTheDocument();
    // توثيق القراءة حتى الأحدث عند الفتح.
    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith('/forum/read', { upToMessageId: 1 });
    });
  });

  it('يعرض حالة فارغة مصممة بلا تيار', async () => {
    mockStream([]);
    renderPage();

    expect(await screen.findByText('لا رسائل بعد — كن أول من يكتب…')).toBeInTheDocument();
  });

  it('يهرب نص الرسالة (لا عناصر HTML حقيقية)', async () => {
    mockStream([message({ body: '<img src=x onerror=alert(1)>نص' })]);
    renderPage();

    const article = await screen.findByRole('article', { name: /رسالة من/ });
    expect(article.textContent).toContain('<img src=x onerror=alert(1)>نص');
    expect(article.querySelector('img')).toBeNull();
  });

  it('يرسل الرسالة ويمسح المسودة ويعطّل الزر أثناء الطلب', async () => {
    const user = userEvent.setup();
    mockStream([]);
    apiPost.mockImplementation((url: string) => {
      if (url === '/forum/read') return Promise.resolve({ data: { maxReadMessageId: 0 } });
      if (url === '/forum/messages')
        return new Promise(() => {
          /* معلّق عمدًا لفحص التعطيل */
        });
      return Promise.reject(new Error(`unexpected POST ${url}`));
    });
    renderPage();
    await screen.findByText('لا رسائل بعد — كن أول من يكتب…');

    await user.type(screen.getByLabelText(/نص الرسالة/), 'رسالة جديدة');
    await user.click(screen.getByRole('button', { name: 'إرسال الرسالة' }));

    expect(screen.getByRole('button', { name: 'إرسال الرسالة' })).toBeDisabled();
    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith('/forum/messages', { body: 'رسالة جديدة', quotedMessageId: null });
    });
  });

  it('ينسخ النص ويعرض تنبيه نجاح', async () => {
    const user = userEvent.setup();
    const writeText = mockClipboard();
    mockStream();
    renderPage();
    await screen.findByText('مرحبا بالمنتدى');

    await user.click(screen.getByRole('button', { name: /إجراءات الرسالة/ }));
    await user.click(screen.getByRole('menuitem', { name: 'نسخ' }));

    expect(writeText).toHaveBeenCalledWith('مرحبا بالمنتدى');
    expect(await screen.findByText('نُسخت الرسالة')).toBeInTheDocument();
  });

  it('يفتح نافذة «شوهدت بواسطة» من القائمة', async () => {
    const user = userEvent.setup();
    mockStream([message({ readCount: 1 })]);
    renderPage();
    await screen.findByText('مرحبا بالمنتدى');

    await user.click(screen.getByRole('button', { name: /إجراءات الرسالة/ }));
    await user.click(screen.getByRole('menuitem', { name: 'شوهدت بواسطة' }));

    expect(await screen.findByRole('dialog', { name: 'شوهدت بواسطة' })).toBeInTheDocument();
    expect(screen.getByText('القارئ')).toBeInTheDocument();
  });

  it('يعدّل الكاتب رسالته ويظهر ختم «عُدّل»', async () => {
    const user = userEvent.setup();
    mockStream();
    apiPut.mockResolvedValue({
      data: message({ body: 'بعد التعديل', editedById: 3, editedAtUtc: new Date().toISOString() }),
    });
    renderPage();
    await screen.findByText('مرحبا بالمنتدى');

    await user.click(screen.getByRole('button', { name: /إجراءات الرسالة/ }));
    await user.click(screen.getByRole('menuitem', { name: 'تعديل' }));
    expect(screen.getByLabelText(/نص الرسالة/)).toHaveValue('مرحبا بالمنتدى');

    await user.clear(screen.getByLabelText(/نص الرسالة/));
    await user.type(screen.getByLabelText(/نص الرسالة/), 'بعد التعديل');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith('/forum/messages/1', { body: 'بعد التعديل' });
    });
    expect(await screen.findByText('بعد التعديل')).toBeInTheDocument();
    expect(screen.getByText(/عُدّل/)).toBeInTheDocument();
  });

  it('يستعيد المسودة الجارية بعد إلغاء التعديل (لا ضياع صامت)', async () => {
    const user = userEvent.setup();
    mockStream();
    renderPage();
    await screen.findByText('مرحبا بالمنتدى');

    await user.type(screen.getByLabelText(/نص الرسالة/), 'مسودة جارية');
    await user.click(screen.getByRole('button', { name: /إجراءات الرسالة/ }));
    await user.click(screen.getByRole('menuitem', { name: 'تعديل' }));
    expect(screen.getByLabelText(/نص الرسالة/)).toHaveValue('مرحبا بالمنتدى');

    await user.click(screen.getByRole('button', { name: 'إلغاء التعديل' }));
    expect(screen.getByLabelText(/نص الرسالة/)).toHaveValue('مسودة جارية');
  });

  it('يعرض شريط المثبّتة عند وجودها', async () => {
    mockStream([message()], message({ id: 9, body: 'إعلان مهم', isPinned: true }));
    renderPage();

    const banner = await screen.findByRole('button', { name: /الانتقال إلى الرسالة المثبتة/ });
    expect(within(banner).getByText('إعلان مهم')).toBeInTheDocument();
  });

  it('يبحث بالنص بعد التوقف عن الكتابة', async () => {
    const user = userEvent.setup();
    mockStream();
    renderPage();
    await screen.findByText('مرحبا بالمنتدى');
    expect(apiGet).toHaveBeenCalledWith('/forum/messages', expect.objectContaining({ params: { limit: 30 } }));

    await user.type(screen.getByLabelText(/بحث في نص الرسائل/), 'جلسة');
    await waitFor(
      () => {
        expect(apiGet).toHaveBeenCalledWith(
          '/forum/messages',
          expect.objectContaining({ params: { limit: 30, q: 'جلسة' } }),
        );
      },
      { timeout: 3000 },
    );
  });
});
