import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import { CurrentYearContext, fallbackYear } from '../hooks/useCurrentYear';

// يُشارَك وعد واحد بين نسخ الموفر المتعددة، فلا تتكرر طلبات /api/meta/current-year
// عند إعادة التركيب (StrictMode) أو وجود أكثر من موفر في الشجرة.
let sharedFetch: Promise<number> | null = null;

function fetchServerYear(): Promise<number> {
  sharedFetch ??= api
    .get<{ currentYear: number }>('/meta/current-year')
    .then((r) => r.data?.currentYear ?? fallbackYear())
    .catch(() => fallbackYear())
    .finally(() => {
      sharedFetch = null;
    });
  return sharedFetch;
}

/**
 * موفر سنة النظام: يجلب سنة «قرار السنة» الحالية من الخادم مرة واحدة عند التركيب
 * ويعرضها على كل المستهلكين. يبدأ بقيمة احتياطية (سنة المتصفح) فلا تتوقف الواجهة
 * على الجلب، ويُعلن حالةُ التحميل عبر CurrentYearBanner لتجنّب التقديم على سنة غير مؤكدة.
 */
export default function CurrentYearProvider({ children }: { children: ReactNode }) {
  const [currentYear, setCurrentYear] = useState<number>(fallbackYear);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let alive = true;
    void fetchServerYear().then((year) => {
      if (!alive) return;
      setCurrentYear(year);
      setIsLoading(false);
    });
    return () => {
      alive = false;
    };
  }, []);

  const value = useMemo(
    () => ({ currentYear, isLoading }),
    [currentYear, isLoading],
  );
  return (
    <CurrentYearContext.Provider value={value}>
      {children}
    </CurrentYearContext.Provider>
  );
}