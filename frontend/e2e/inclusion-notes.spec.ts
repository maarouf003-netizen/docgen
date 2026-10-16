import { test, expect, request } from '@playwright/test';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
test.use({ storageState: path.join(here, '.auth', 'lawyer.json') });

const API = 'http://localhost:5199';

async function csrfHeader(authFile: string): Promise<Record<string, string>> {
  const raw = JSON.parse(fs.readFileSync(authFile, 'utf-8')) as {
    cookies?: Array<{ name: string; value: string }>;
  };
  const csrf = (raw.cookies ?? []).find((c) => c.name === 'docgen_csrf')?.value ?? '';
  return csrf === '' ? {} : { 'X-CSRF-Token': csrf };
}

test('ordinary file carries ملاحظات from form to detail', async ({ page }) => {
  const tag = `E2E-${Date.now()}`;
  await page.goto('/documents/new');

  await page.getByLabel('نوع السند').selectOption('عادي');
  const inclusion = page.getByLabel('المتضمن');
  const notes = page.getByLabel('ملاحظات', { exact: true });
  await expect(inclusion).toBeVisible();
  await expect(notes).toBeVisible();
  await expect(notes).toHaveAttribute('placeholder', 'اكتب القرارات الأخرى هنا...');

  // Order: المتضمن before ملاحظات in the DOM.
  const boxes = page.locator('#inclusionText, #inclusionNotes');
  await expect(boxes.nth(0)).toHaveAttribute('id', 'inclusionText');
  await expect(boxes.nth(1)).toHaveAttribute('id', 'inclusionNotes');

  await page.getByLabel('الاسم', { exact: true }).first().fill(`${tag}-borrower`);
  await inclusion.fill(`${tag} summary`);
  await notes.fill(`${tag} other decisions`);

  // Execution circuit is mandatory: pick the first one.
  await page.getByRole('button', { name: 'اختيار الدائرة' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /^اختيار / }).first().click();
  await expect(dialog).toBeHidden();

  const [resp] = await Promise.all([
    page.waitForResponse(
      (r) => r.url().includes('/api/documents') && r.request().method() === 'POST',
    ),
    page.getByRole('button', { name: /حفظ/ }).click(),
  ]);
  expect(resp.ok()).toBeTruthy();
  const created = (await resp.json()) as { id: number };
  expect(created.id).toBeGreaterThan(0);

  await page.goto(`/documents/${created.id}`);
  await expect(page.getByText('ملاحظات', { exact: true }).first()).toBeVisible();
  await expect(page.getByText(`${tag} other decisions`)).toBeVisible();

  // Self-cleanup: lawyer owns an unlinked file, DELETE succeeds.
  const ctx = await request.newContext({
    storageState: path.join(here, '.auth', 'lawyer.json'),
  });
  try {
    const del = await ctx.delete(`${API}/api/documents/${created.id}`, {
      headers: await csrfHeader(path.join(here, '.auth', 'lawyer.json')),
    });
    expect(del.status()).toBeLessThan(300);
  } finally {
    await ctx.dispose();
  }
});

test('mobile 375px: notes field visible with no horizontal overflow', async ({
  browser,
}) => {
  const mctx = await browser.newContext({
    viewport: { width: 375, height: 667 },
    storageState: path.join(here, '.auth', 'lawyer.json'),
  });
  const mob = await mctx.newPage();
  await mob.goto('/documents/new');
  await mob.getByLabel('نوع السند').selectOption('عادي');
  await expect(mob.getByLabel('ملاحظات', { exact: true })).toBeVisible();
  const overflow = await mob.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  await mctx.close();
});
