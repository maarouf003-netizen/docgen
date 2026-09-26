import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import CorrespondenceLengthCounter from './CorrespondenceLengthCounter';

describe('CorrespondenceLengthCounter', () => {
  it('يعرض العدّاد رماديًا بلا تحذير ضمن الحد', () => {
    const { container } = render(
      <CorrespondenceLengthCounter counterId="test-counter" plainLen={5} tooLong={false} />,
    );

    const counter = container.querySelector('#test-counter');
    expect(counter?.textContent).toBe('5 / 10000');
    expect(counter?.className).toContain('text-gray-400');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('يُحمرّ العدّاد ويُظهر التحذير عند التجاوز', () => {
    const { container } = render(
      <CorrespondenceLengthCounter counterId="test-counter" plainLen={10001} tooLong />,
    );

    const counter = container.querySelector('#test-counter');
    expect(counter?.textContent).toBe('10001 / 10000');
    expect(counter?.className).toContain('text-red-600');
    expect(screen.getByRole('alert')).toHaveTextContent(/تجاوز النص الحد الأقصى/);
  });
});
