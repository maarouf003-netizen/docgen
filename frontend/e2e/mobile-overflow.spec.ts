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

  // P6-03b: توسيع فحص 375px إلى شاشات القوائم/الإدارة/التقويم.
  test('forum page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/forum');

    await expect(page.getByRole('heading', { name: 'منتدى المحامين' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/forum');
  });

  test('statistics page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/stats');

    await expect(page.getByRole('heading', { name: 'الإحصائيات' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/stats');
  });

  test('branches management fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/branches/manage');

    await expect(page.getByRole('heading', { name: 'إدارة الفروع' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/branches/manage');
  });

  test('calendar page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.lawyer);
    await page.goto('/calendar');

    await expect(page.getByRole('heading', { name: 'التقويم' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/calendar');
  });

  test('suggestions page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/suggestions');

    await expect(page.getByRole('heading', { name: 'اقتراحات التطوير' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/suggestions');
  });

  test('circuit statistics page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/circuit-stats');

    await expect(page.getByRole('heading', { name: 'إحصائيات الدوائر' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/circuit-stats');
  });

  test('audit logs page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/audit-logs');

    await expect(page.getByRole('heading', { name: 'سجل التدقيق' })).toBeVisible();
    await expect(page.getByText(/إجمالي السجلات/)).toBeVisible();
    await expectNoHorizontalOverflow(page, '/audit-logs');
  });

  test('delegation requests page fits 375px', async ({ page }) => {
    await apiLogin(page, E2E_USERS.head);
    await page.goto('/delegations/requests');

    await expect(page.getByRole('heading', { name: 'طلبات الإنابة' })).toBeVisible();
    await expectNoHorizontalOverflow(page, '/delegations/requests');
  });
});
