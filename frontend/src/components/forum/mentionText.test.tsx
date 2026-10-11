import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { renderMentionText } from './mentionText';

function show(text: string, currentUserId?: number | null) {
  return render(<p>{renderMentionText(text, currentUserId)}</p>);
}

describe('renderMentionText', () => {
  it('يمرّر النص العادي حرفيًا بلا شرائح', () => {
    const { container } = show('مرحبا بالجميع');
    expect(container.textContent).toBe('مرحبا بالجميع');
    expect(container.querySelectorAll('span').length).toBe(0);
  });

  it('يحوّل الرمز القانوني إلى شريحة بالاسم دون المعرف', () => {
    show('راجع يا @[12:علي محمد] الملف');
    const chip = screen.getByLabelText('ذِكر: علي محمد');
    expect(chip).toHaveTextContent('@علي محمد');
    expect(chip.textContent).not.toContain('12');
  });

  it('يبرز ذِكر القارئ نفسه بلون مختلف وتسمية مميزة', () => {
    const { container } = show('أهلا @[7:أنا] و@[9:غيري]', 7);
    expect(screen.getByLabelText('ذُكرت: أنا').className).toContain('bg-emerald-700');
    expect(screen.getByLabelText('ذِكر: غيري').className).toContain('bg-sky-100');
    expect(container.textContent).toContain('أهلا');
  });

  it('يترك الشاذ من الصيغة نصًا حرفيًا (لا شريحة مزيفة)', () => {
    const { container } = show('سعر @[abc] ورمز @[5 غير مغلق وقوس @[] فارغ');
    expect(container.querySelectorAll('span').length).toBe(0);
    expect(container.textContent).toContain('@[abc]');
  });

  it('يهرب HTML حول الرمز وداخل النص (لا عناصر دخيلة)', () => {
    const { container } = show('<img src=x onerror=alert(1)> راجع @[3:سامي]');
    expect(container.querySelector('img')).toBeNull();
    expect(container.textContent).toContain('<img src=x onerror=alert(1)>');
    expect(screen.getByLabelText('ذِكر: سامي')).toBeInTheDocument();
  });
});
