import { describe, expect, it } from 'vitest';
import { createQueryClient } from './queryClient';

/**
 * `P6-01b`: إعدادات العميل عقد أداء/سلوك — إعادة المحاولة للقراءات مسؤولية
 * `api/client` وحدها، ولا إعادة جلب عند العودة للتبويب، مع مخزَّن قصير يمنع التكرار.
 */
describe('queryClient', () => {
  it('يعطّل retry وrefetchOnWindowFocus ويضبط staleTime 30s', () => {
    const client = createQueryClient();
    const defaults = client.getDefaultOptions().queries ?? {};

    expect(defaults.retry).toBe(false);
    expect(defaults.refetchOnWindowFocus).toBe(false);
    expect(defaults.staleTime).toBe(30_000);
  });

  it('ينشئ عميلًا مستقلًا في كل نداء (لا تسرّب مخزَّن بين الاختبارات/الجلسات)', () => {
    expect(createQueryClient()).not.toBe(createQueryClient());
  });
});
