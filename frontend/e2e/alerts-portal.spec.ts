import { expect, test } from '@playwright/test';
import { apiLogin, E2E_USERS, formatConsoleProblems, trackConsoleProblems } from './support/app.ts';

/**
 * `P6-04` — شاشتان للقراءة فقط: التنبيهات (مدير) وبوابة مندوب الجهة.
 * المطلوب هنا: تركيبهما يعمل بلا استثناءات JavaScript ولا أخطاء `console.error`
 * (عدا ضجيج الشبكة المعلن في `support/app.ts`).
 */
test.describe('read-only screens', () => {
  test('alerts page renders for a manager without console errors', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);

    const problems = trackConsoleProblems(page);
    await page.goto('/alerts');

    await expect(page.getByRole('heading', { name: 'التنبيهات' })).toBeVisible();
    await expect(page.getByText(/لا توجد تنبيهات/)).toBeVisible();
    expect(problems, formatConsoleProblems(problems)).toEqual([]);
  });

  test('portal files page renders for an entity manager without console errors', async ({ page }) => {
    await apiLogin(page, E2E_USERS.portal);

    const problems = trackConsoleProblems(page);
    await page.goto('/portal/files');

    await expect(page.getByRole('heading', { name: 'الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'بحث في الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByText(/لا توجد ملفات/)).toBeVisible();
    expect(problems, formatConsoleProblems(problems)).toEqual([]);
  });
});
