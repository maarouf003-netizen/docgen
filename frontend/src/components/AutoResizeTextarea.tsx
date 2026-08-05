import { useEffect, useRef } from 'react';

interface AutoResizeTextareaProps {
  value: string;
  onChange: (value: string) => void;
  id?: string;
  placeholder?: string;
  minRows?: number;
  maxHeight?: number;
  className?: string;
}

export default function AutoResizeTextarea({
  value,
  onChange,
  id,
  placeholder,
  minRows = 2,
  maxHeight = 240,
  className,
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
    />
  );
}
