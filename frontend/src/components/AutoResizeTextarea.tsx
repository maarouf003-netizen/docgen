import { useEffect, useRef, type FocusEvent, type KeyboardEvent, type MouseEvent } from 'react';

interface AutoResizeTextareaProps {
  value: string;
  onChange: (value: string) => void;
  id?: string;
  placeholder?: string;
  minRows?: number;
  maxHeight?: number;
  className?: string;
  /** تمرير أحداث لوحة المفاتيح/النقر (اختياري — لمنتقي الذِكر وتتبع المؤشر). */
  onKeyDown?: (e: KeyboardEvent<HTMLTextAreaElement>) => void;
  onKeyUp?: (e: KeyboardEvent<HTMLTextAreaElement>) => void;
  onClick?: (e: MouseEvent<HTMLTextAreaElement>) => void;
  onBlur?: (e: FocusEvent<HTMLTextAreaElement>) => void;
  /** خاصية `aria` للمنتقي (موسّع/المتحكم) — غائبة بلا منتقي. */
  ariaExpanded?: boolean;
  ariaControls?: string;
}

export default function AutoResizeTextarea({
  value,
  onChange,
  id,
  placeholder,
  minRows = 2,
  maxHeight = 240,
  className,
  onKeyDown,
  onKeyUp,
  onClick,
  onBlur,
  ariaExpanded,
  ariaControls,
}: AutoResizeTextareaProps) {
  const ref = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, maxHeight)}px`;
  }, [value, maxHeight]);

  return (
    <textarea
      ref={ref}
      id={id}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      rows={minRows}
      className={className}
      onKeyDown={onKeyDown}
      onKeyUp={onKeyUp}
      onClick={onClick}
      onBlur={onBlur}
      aria-expanded={ariaExpanded}
      aria-controls={ariaControls}
    />
  );
}
