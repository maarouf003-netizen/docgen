import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';

/** عميل اختبار معزول لكل حالة: بلا إعادة محاولة، ومخزَّن يبقى طوال الحالة. */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        refetchOnWindowFocus: false,
        staleTime: 30_000,
        gcTime: Infinity,
      },
    },
  });
}

/**
 * يرسم مكوّنًا داخل مزوّد استعلامات معزول، ويعيد العميل للتحقق من المخزَّن/الطلبات.
 */
export function renderWithQueryClient(ui: ReactElement) {
  const queryClient = createTestQueryClient();
  return {
    queryClient,
    ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>),
  };
}
