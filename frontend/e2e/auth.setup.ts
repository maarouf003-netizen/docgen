import { test as setup, expect } from '@playwright/test';
import path from 'path';
import { fileURLToPath } from 'url';

const authDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '.auth');

// Logs every role in through the real login form and persists cookies.
// Required env: E2E_PASSWORD. Optional overrides (defaults are seed names):
// E2E_LAWYER, E2E_HEAD, E2E_SUBHEAD, E2E_MANAGER, E2E_ADMIN, E2E_DELEGATE.

function creds(envName: string, fallback: string): string {
  const v = (process.env[envName] ?? '').trim();
  return v === '' ? fallback : v;
}

const password = (process.env.E2E_PASSWORD ?? '').trim();
if (password === '') {
  throw new Error('E2E_PASSWORD is required (dev password, never committed)');
}

const roles: Array<{ key: string; user: string }> = [
  { key: 'lawyer', user: creds('E2E_LAWYER', 'lawyer1') },
  { key: 'head', user: creds('E2E_HEAD', 'head1') },
  { key: 'subhead', user: creds('E2E_SUBHEAD', 'subhead1') },
  { key: 'manager', user: creds('E2E_MANAGER', 'manager') },
  { key: 'admin', user: creds('E2E_ADMIN', 'admin') },
  { key: 'delegate', user: creds('E2E_DELEGATE', '') },
];

for (const { key, user } of roles) {
  setup(`login as ${key}`, async ({ page }) => {
    setup.skip(user === '', `no user configured for ${key}`);
    await page.goto('/login');
    await page.getByLabel('اسم المستخدم').fill(user);
    await page.getByLabel('كلمة المرور').fill(password);
    await page.getByRole('button', { name: 'دخول' }).click();
    await expect(page).not.toHaveURL(/\/login/, { timeout: 20000 });
    await page.context().storageState({ path: path.join(authDir, `${key}.json`) });
  });
}
