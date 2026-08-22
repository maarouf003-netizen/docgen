import { useEffect, useRef } from 'react';

export function useTimeout(callback: () => void, delayMs: number | null): void {
  const latestCallback = useRef(callback);

  useEffect(() => {
    latestCallback.current = callback;
  });

  useEffect(() => {
    if (delayMs === null) return;
    const timer = window.setTimeout(() => latestCallback.current(), delayMs);
    return () => window.clearTimeout(timer);
  }, [delayMs]);
}
