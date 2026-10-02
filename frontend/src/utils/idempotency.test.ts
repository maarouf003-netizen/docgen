import { describe, it, expect } from 'vitest';
import { IDEMPOTENCY_KEY_HEADER, idempotencyHeaders, newIdempotencyKey } from './idempotency';

describe('idempotency (RF-011)', () => {
  it('يولّد مفتاحًا فريدًا بصيغة uuid في كل استدعاء', () => {
    const a = newIdempotencyKey();
    const b = newIdempotencyKey();
    expect(a).not.toBe(b);
    expect(a).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(b).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });

  it('يبني ترويسة المفتاح باسمها المعتمد', () => {
    expect(IDEMPOTENCY_KEY_HEADER).toBe('X-Idempotency-Key');
    expect(idempotencyHeaders('k-1')).toEqual({ headers: { 'X-Idempotency-Key': 'k-1' } });
  });
});
