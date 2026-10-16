import { test, expect } from '@playwright/test';
import path from 'path';
import { fileURLToPath } from 'url';

// Auth persistence per role: after setup login, reload must stay
// authenticated (no bounce back to /login). Delegate must land in /portal.
const here = path.dirname(fileURLToPath(import.meta.url));
const auth = (role: string) => path.join(here, '.auth', `${role}.json`);

for (const role of ['lawyer', 'head', 'manager', 'admin']) {
  test(`${role} session persists`, async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: auth(role) });
    const page = await ctx.newPage();
    await page.goto('/');
    await expect(page).not.toHaveURL(/\/login/);
    // Authenticated layout renders instead of the login form (logout
    // affordance differs per dashboard, so absence of the form is the proof).
    await expect(page.getByLabel('اسم المستخدم')).toHaveCount(0);
    await ctx.close();
  });
}

test('delegate lands in portal', async ({ browser }) => {
  const ctx = await browser.newContext({ storageState: auth('delegate') });
  const page = await ctx.newPage();
  await page.goto('/');
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page).toHaveURL(/\/portal/);
  await ctx.close();
});
