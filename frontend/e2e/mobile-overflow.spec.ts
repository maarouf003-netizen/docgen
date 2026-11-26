import { expect, test } from '@playwright/test';
import { apiLogin, E2E_USERS, expectNoHorizontalOverflow } from './support/app.ts';

/**
 * `P6-04` — فحص الجوال 375×812: لا تجاوز أفقي فعلي
 * (`documentElement.scrollWidth <= innerWidth + 1`) على المسارات الرئيسية.
 *
 * هذا الملف يعمل في مشروع `mobile` وحده (انظر `projects.testMatch` في الإعداد).
 */
test.describe('mobile 375px — no horizontal overflow', () => {
  test('login page fits 375px', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('button', { name: 'دخول', exact: true })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/login');
  });

  test('documents list fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/documents');

    await expect(page.getByRole('heading', { name: 'الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByText('لا توجد نتائج', { exact: true })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/documents');
  });

  test('alerts page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/alerts');

    await expect(page.getByRole('heading', { name: 'التنبيهات' })).toBeVisible();
    await expect(page.getByText(/لا توجد تنبيهات/)).toBeVisible();
    await expectNoHorizontalOverflow(page, '/alerts');
  });

  test('portal files page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.portal);
    await page.goto('/portal/files');

    await expect(page.getByRole('heading', { name: 'الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByText(/لا توجد ملفات/)).toBeVisible();
    await expectNoHorizontalOverflow(page, '/portal/files');
  });
});
