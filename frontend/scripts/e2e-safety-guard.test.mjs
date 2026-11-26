import { describe, expect, it } from 'vitest';
import {
  assertSafeE2eDatabase,
  assertSafeE2eTarget,
  parseE2eConnectionString,
} from './e2e-safety-guard.mjs';

/**
 * `P6-04` — حراس السلامة: أهم ما يُختبر هنا أن **لا** يمكن تجاوز قاعدة
 * «محلي فقط» أو تشغيل E2E على قاعدة إنتاج/مشتركة براية staging وحدها.
 */
describe('e2e safety guard — base URL', () => {
  it('accepts loopback hosts', () => {
    expect(assertSafeE2eTarget('http://127.0.0.1:5173', {})).toEqual({ host: '127.0.0.1', staging: false });
    expect(assertSafeE2eTarget('http://localhost:5173', {})).toEqual({ host: 'localhost', staging: false });
    expect(assertSafeE2eTarget('http://[::1]:5173', {})).toEqual({ host: '::1', staging: false });
  });

  it('refuses a Render production URL', () => {
    expect(() => assertSafeE2eTarget('https://docgen.onrender.com', {})).toThrow(/non-local host/);
  });

  it('refuses Supabase even with the staging flag (name has no "staging")', () => {
    expect(() =>
      assertSafeE2eTarget('https://project.supabase.co', { E2E_ALLOW_STAGING: '1' }),
    ).toThrow(/does not contain "staging"/);
  });

  it('refuses a staging host without the explicit flag', () => {
    expect(() => assertSafeE2eTarget('https://docgen-staging.example.test', { E2E_ALLOW_STAGING: 'true' })).toThrow(
      /E2E_ALLOW_STAGING=1/,
    );
  });

  it('allows a declared staging host only with the flag', () => {
    expect(
      assertSafeE2eTarget('https://docgen-staging.example.test', { E2E_ALLOW_STAGING: '1' }),
    ).toEqual({ host: 'docgen-staging.example.test', staging: true });
  });

  it('refuses non-absolute and non-http targets', () => {
    expect(() => assertSafeE2eTarget('/login', {})).toThrow(/valid absolute URL/);
    expect(() => assertSafeE2eTarget('file:///c:/tmp/index.html', {})).toThrow(/http or https/);
    expect(() => assertSafeE2eTarget('', {})).toThrow(/empty/);
  });

  it('cannot disguise a remote host behind loopback userinfo or fragment', () => {
    expect(() => assertSafeE2eTarget('http://127.0.0.1@docgen.onrender.com', {})).toThrow(/non-local host/);
    expect(() => assertSafeE2eTarget('http://docgen.onrender.com#@127.0.0.1', {})).toThrow(/non-local host/);
  });
});

describe('e2e safety guard — disposable database', () => {
  it('accepts a local database whose name contains e2e', () => {
    expect(parseE2eConnectionString('Host=localhost;Port=5432;Database=docgen_e2e;Username=postgres')).toEqual({
      host: 'localhost',
      port: '5432',
      database: 'docgen_e2e',
      format: 'keyword',
    });
  });

  it('refuses shared/production-looking database names', () => {
    expect(() =>
      assertSafeE2eDatabase('Host=127.0.0.1;Database=docgen;Username=postgres', {}),
    ).toThrow(/must contain "e2e"/);
    expect(() =>
      assertSafeE2eDatabase('Host=127.0.0.1;Database=docgen_production_e2e;Username=postgres', {}),
    ).toThrow(/looks production/);
  });

  it('refuses remote database hosts without a declared staging name', () => {
    expect(() =>
      assertSafeE2eDatabase('Host=db.example.com;Database=docgen_e2e;Username=postgres', {}),
    ).toThrow(/non-local host/);
    expect(() =>
      assertSafeE2eDatabase('Host=db.example.com;Database=docgen_e2e;Username=postgres', {
        E2E_ALLOW_STAGING: '1',
      }),
    ).toThrow(/non-local host/);
  });

  it('accepts a declared staging database host', () => {
    expect(
      assertSafeE2eDatabase('Host=db-staging.example.test;Database=docgen_e2e;Username=postgres', {
        E2E_ALLOW_STAGING: '1',
      }),
    ).toMatchObject({ host: 'db-staging.example.test', database: 'docgen_e2e' });
  });

  it('fails closed instead of guessing missing host or database', () => {
    expect(() => assertSafeE2eDatabase('Host=127.0.0.1;Username=postgres', {})).toThrow(/no Database/);
    expect(() => assertSafeE2eDatabase('Database=docgen_e2e;Username=postgres', {})).toThrow(/no Host/);
    expect(() => assertSafeE2eDatabase('', {})).toThrow(/empty/);
  });

  it('refuses duplicate keys and URI query overrides', () => {
    expect(() =>
      assertSafeE2eDatabase(
        'Host=db.example.com;Database=docgen_e2e;Host=127.0.0.1;Username=postgres',
        {},
      ),
    ).toThrow(/more than once/);

    expect(() =>
      assertSafeE2eDatabase('postgres://postgres@localhost:5432/docgen_e2e?Host=db.example.com', {}),
    ).toThrow(/query parameters/);
  });

  it('never leaks the password in rejection messages', () => {
    const secret = 'SuperSecretE2ePassword';
    const connection = 'Host=db.example.com;Database=docgen_e2e;Username=postgres;Password=' + secret;
    let message = '';
    try {
      assertSafeE2eDatabase(connection, {});
    } catch (error) {
      message = error instanceof Error ? error.message : String(error);
    }
    expect(message).not.toContain(secret);
    expect(message).toContain('non-local host');
  });
});
