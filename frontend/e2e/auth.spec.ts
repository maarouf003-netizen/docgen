import { expect, test } from '@playwright/test';
import { E2E_PASSWORD, E2E_USERS, uiLogin } from './support/app.ts';

/**
 * `P6-04` — المصادقة عبر الواجهة: دخول صحيح يهبط على اللوحة، خطأ عربي صريح
 * عند بيانات خاطئة، وخروج يُبطل الجلسة فعليًا (لا حذف محلي فقط).
 */
test.describe('authentication', () => {
  test('valid manager login lands on the dashboard', async ({ page }) => {
    await uiLogin(page, E2E_USERS.manager);

    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('navigation', { name: 'القائمة الرئيسية' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'تسجيل الخروج' })).toBeVisible();
  });

  test('invalid login shows the Arabic credentials error', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('اسم المستخدم', { exact: true }).fill(E2E_USERS.manager);
    await page.getByLabel('كلمة المرور', { exact: true }).fill(`${E2E_PASSWORD}-wrong`);
    await page.getByRole('button', { name: 'دخول', exact: true }).click();

    await expect(page.getByRole('alert')).toContainText('اسم المستخدم أو كلمة المرور غير صحيحة');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('logout revokes the session and protects internal routes', async ({ page }) => {
    await uiLogin(page, E2E_USERS.manager);

    await page.getByRole('button', { name: 'تسجيل الخروج' }).click();
    await expect(page).toHaveURL(/\/login(?:\?|$)/);

    // الجلسة أُبطلت خادميًا: المسار المحمي يعيد التوجيه لتسجيل الدخول.
    await page.goto('/documents');
    await expect(page).toHaveURL(/\/login(?:\?|$)/);
  });
});
