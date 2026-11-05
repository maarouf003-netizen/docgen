import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MessageBubble from './MessageBubble';
import type { ForumMessageDto } from '../../types';

const message = (overrides: Partial<ForumMessageDto> = {}): ForumMessageDto => ({
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
});

const noop = () => {};

function renderBubble() {
  return render(
    <MessageBubble
      message={message()}
      mine={false}
      isAdmin={false}
      currentUserId={1}
      highlighted={false}
      onCopy={noop}
      onQuote={noop}
      onEdit={noop}
      onDelete={noop}
      onTogglePin={noop}
      onShowReaders={noop}
      onJumpToQuote={noop}
    />,
  );
}

describe('MessageBubble — قائمة الإجراءات', () => {
  it('تفتح القائمة وتُعرض إجراءات الرسالة', async () => {
    const user = userEvent.setup();
    renderBubble();

    await user.click(screen.getByRole('button', { name: 'إجراءات الرسالة من المحامي الأول' }));

    const menu = screen.getByRole('menu', { name: 'إجراءات الرسالة' });
    expect(menu).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'نسخ' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'اقتباس رد' })).toBeInTheDocument();
  });

  it('تفتح للأسفل افتراضيًا عندما تكفي المساحة تحتها', async () => {
    const user = userEvent.setup();
    renderBubble();

    await user.click(screen.getByRole('button', { name: 'إجراءات الرسالة من المحامي الأول' }));

    expect(screen.getByRole('menu').className).toContain('top-full');
  });

  it('تفتح للأعلى عندما تضيق المساحة تحتها وتكفي فوقها', async () => {
    const user = userEvent.setup();
    // الفقاعة قرب أسفل نافذة العرض: لا مساحة كافية للقائمة تحتها.
    const rect = {
      top: 120,
      bottom: window.innerHeight - 8,
      left: 0,
      right: 0,
      width: 0,
      height: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    } as DOMRect;
    const spy = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue(rect);
    try {
      renderBubble();
      await user.click(screen.getByRole('button', { name: 'إجراءات الرسالة من المحامي الأول' }));
      expect(screen.getByRole('menu').className).toContain('bottom-full');
    } finally {
      spy.mockRestore();
    }
  });

  it('لا تحسب مساحة شريط التنقل السفلي ضمن المتاح تحت الزر على الجوال', async () => {
    const user = userEvent.setup();
    const originalMatchMedia = window.matchMedia;
    window.matchMedia = vi.fn((query: string) => ({
      matches: query === '(max-width: 1023px)',
      media: query,
    })) as unknown as typeof window.matchMedia;
    // مساحة تحتها تكفي القائمة القصيرة نظريًا (100px)، لكن بعد حجز ارتفاع
    // شريط التنقل السفلي تصبح غير كافية (36px) — فتُفتح للأعلى.
    const rect = {
      top: 600,
      bottom: window.innerHeight - 100,
      left: 0,
      right: 0,
      width: 0,
      height: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    } as DOMRect;
    const spy = vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue(rect);
    try {
      renderBubble();
      await user.click(screen.getByRole('button', { name: 'إجراءات الرسالة من المحامي الأول' }));
      expect(screen.getByRole('menu').className).toContain('bottom-full');
    } finally {
      spy.mockRestore();
      window.matchMedia = originalMatchMedia;
    }
  });
});
