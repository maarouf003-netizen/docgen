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
  /**
   * تصفير البيانات عند تغيّر مفتاح الهوية (deps) — لاستعلامات الكيان المفرد
   * فقط (ملف/استئناف بالمعرف)، فلا يُعرض كيان سابق أثناء تحميل الجديد.
   * الافتراضي false عمدًا: القوائم تُبقي بياناتها أثناء الفلترة والترقيم
   * (لا وميض). إعادة الجلب refetch لا تُصفِّر أبدًا في الحالتين.
   *
   * عقد إلزامي: طول `deps` ثابت مدى حياة المكوّن — التبعيات منثورة في
   * مصفوفة التأثير، فتغيّر الطول بين الرندرات يُطلق تحذير React ويُعيد
   * الجلب عبثًا (يُكتشف في التطوير عبر `console.error` أدناه).
   */
  resetOnDepsChange?: boolean;
}

// تُحسب مرة واحدة على مستوى الوحدة: الحارس تطويري فقط ولا أثر له إنتاجيًا.
const isDev = (() => {
  try {
    return (import.meta as unknown as { env?: { DEV?: boolean } })?.env?.DEV === true;
  } catch {
    return false;
  }
})();

export function useCancellableRequest<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  deps: readonly unknown[],
  options: UseCancellableRequestOptions = {},
): CancellableRequestResult<T> {
  const enabled = options.enabled ?? true;
  const resetOnDepsChange = options.resetOnDepsChange ?? false;

  const [data, setData] = useState<T | null>(null);
  const [isLoading, setIsLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  const latestFetcher = useRef(fetcher);
  // آخر مفتاح هوية شوهِد: التصفير يحدث فقط عند تغيّره فعلًا، لا عند
  // إعادة الجلب (attempt) ولا عند أول تركيب — فيبقى سلوك القوائم كما هو.
  const prevDeps = useRef<readonly unknown[] | null>(null);

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

    // تغيّر الهوية (لا إعادة الجلب): تُصفَّر بيانات الكيان السابق فورًا
    // عند تفعيل الخيار، فلا يظهر كيان قديم تحت عنوان جديد أثناء التحميل.
    const previous = prevDeps.current;
    if (isDev && previous !== null && previous.length !== deps.length) {
      console.error(
        '[useCancellableRequest] تغيّر طول مصفوفة الهوية (deps) بين الرندرات — ' +
          'مرّر مصفوفة ثابتة الطول وإلا أُعيد الجلب مع تحذير React.',
      );
    }
    prevDeps.current = deps;
    const keyChanged =
      resetOnDepsChange &&
      previous !== null &&
      (previous.length !== deps.length ||
        previous.some((d, i) => !Object.is(d, deps[i])));
    if (keyChanged) {
      setData(null);
    }

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
