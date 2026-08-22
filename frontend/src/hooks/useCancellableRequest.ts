import { useCallback, useEffect, useRef, useState } from 'react';
import { isCancel } from 'axios';
import { getApiErrorMessage } from '../api/client';

export interface CancellableRequestResult<T> {
  data: T | null;
  isLoading: boolean;
  error: string | null;
  refetch: () => void;
  setData: (value: T | null | ((prev: T | null) => T | null)) => void;
}

export interface UseCancellableRequestOptions {
  enabled?: boolean;
}

export function useCancellableRequest<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  deps: readonly unknown[],
  options: UseCancellableRequestOptions = {},
): CancellableRequestResult<T> {
  const enabled = options.enabled ?? true;

  const [data, setData] = useState<T | null>(null);
  const [isLoading, setIsLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  const latestFetcher = useRef(fetcher);

  useEffect(() => {
    latestFetcher.current = fetcher;
  });

  useEffect(() => {
    if (!enabled) {
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();
    let active = true;

    setIsLoading(true);
    setError(null);

    latestFetcher.current(controller.signal)
      .then((result) => {
        if (!active) return;
        setData(result);
        setIsLoading(false);
      })
      .catch((cause: unknown) => {
        if (!active) return;
        setIsLoading(false);
        if (controller.signal.aborted || isCancel(cause)) return;
        setError(getApiErrorMessage(cause));
      });

    return () => {
      active = false;
      controller.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- deps مصفوفة يديرها المستدعى عمداً (نمط useAsync/SWR)
  }, [enabled, attempt, ...deps]);

  const refetch = useCallback(() => setAttempt((n) => n + 1), []);
  const applyData = useCallback(
    (value: T | null | ((prev: T | null) => T | null)) =>
      setData((prev) =>
        typeof value === 'function' ? (value as (prev: T | null) => T | null)(prev) : value,
      ),
    [],
  );

  return { data, isLoading, error, refetch, setData: applyData };
}
