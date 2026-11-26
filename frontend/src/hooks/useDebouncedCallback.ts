import { useEffect, useMemo, useRef } from 'react';

/**
 * تأجيل نداء دالة آخر قيمة (debounce) مع تنظيف المؤقّت عند التركيب/الإزالة.
 * الدالة الأخيرة تُحفظ في مرجع فلا يُعاد بناء المؤقّت عند كل رسم، ويعيد الخطّاف
 * دالة مستقرة الهوية ما دامت `delayMs` ثابتة — تُستعمل في بحث السجل المرجعي.
 */
export function useDebouncedCallback<T extends unknown[]>(
  fn: (...args: T) => void,
  delayMs: number,
): (...args: T) => void {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const fnRef = useRef(fn);
  fnRef.current = fn;

  const debounced = useMemo(
    () =>
      (...args: T) => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(() => fnRef.current(...args), delayMs);
      },
    [delayMs],
  );

  useEffect(
    () => () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    },
    [],
  );

  return debounced;
}
