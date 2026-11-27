import { QueryClient } from '@tanstack/react-query';

/**
 * `P6-01b`: عميل `TanStack Query` الموحّد.
 *
 * - `retry: false`: إعادة المحاولة للقراءات مسؤولية `api/client` وحدها (3 محاولات،
 *   تباعد أُسّي مشوّش، واحترام `Retry-After`) — تكرارها هنا يضاعف الطلبات.
 * - `refetchOnWindowFocus: false`: لا تغيير سلوك الصفحات القائم عند العودة للتبويب.
 * - `staleTime: 30s`: يمنع الطلبات المكررة المتلاحقة (تنقّل سريع/تركيب مزدوج) مع
 *   بقاء البيانات حديثة عمليًا؛ يظهر المخزَّن فورًا ثم يُحدَّث عند التقادم.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        refetchOnWindowFocus: false,
        staleTime: 30_000,
      },
    },
  });
}

/** العميل الواحد للتطبيق (يُمرَّر إلى `QueryClientProvider` في `main.tsx`). */
export const queryClient = createQueryClient();
