import { createContext, useContext } from 'react';

/** قيمة سياق سنة النظام: السنة الجارية المعتمدة من الخادم (سنة «قرار السنة»). */
export interface CurrentYearContextValue {
  currentYear: number;
  isLoading: boolean;
}

export const CurrentYearContext = createContext<CurrentYearContextValue | null>(null);

/** احتياطي عند غياب الموفر أو فشل الجلب: سنة عداد المتصفح المحلية (تكافؤ السلوك السابق). */
export const fallbackYear = (): number => new Date().getFullYear();

/**
 * سنة النظام الحالية (من الخادم). خارج الموفر تعيد سنة المتصفح كقيمة احتياطية
 * دون أي جلب، فتبقى المكوّنات معزولة وقابلة للاختبار كما كانت قبل تعميم الخادم.
 */
export function useCurrentYear(): CurrentYearContextValue {
  const ctx = useContext(CurrentYearContext);
  return ctx ?? { currentYear: fallbackYear(), isLoading: false };
}