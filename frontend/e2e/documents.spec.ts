import { expect, test } from '@playwright/test';
import { apiLogin, E2E_USERS } from './support/app.ts';

/**
 * `P6-04` — المستندات: عرض القائمة، وصول البحث إلى الـAPI، وفتح نموذج الإنشاء
 * والتحقق من حقل إلزامي **دون أي كتابة خادمية**.
 *
 * لا يُنشأ أي مستند: نختار «منفذ عليها» ونرسل نموذجًا ناقصًا، فيوقف التحقق
 * المحلي الطلب قبل `POST /api/documents` (يُثبته عدّاد الطلبات).
 */
test.describe('documents', () => {
  test.beforeEach(async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
  });

  test('list renders and search reaches the API with the typed query', async ({ page }) => {
    await page.goto('/documents');
    await expect(page.getByRole('heading', { name: 'الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'بحث في الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByText('لا توجد نتائج', { exact: true })).toBeVisible();

    const needle = 'e2e-no-such-document-20261126';
    const listRequest = page.waitForResponse((response) => {
      if (!response.url().includes('/api/documents/list')) return false;
      return new URL(response.url()).searchParams.get('q') === needle;
    });

    await page.getByRole('searchbox', { name: 'بحث في الملفات التنفيذية' }).fill(needle);

    const response = await listRequest;
    expect(response.ok(), `GET /api/documents/list?q=${needle}`).toBeTruthy();
    await expect(page.getByText('لا توجد نتائج', { exact: true })).toBeVisible();
  });

  test('create form opens and blocks an incomplete executed-side document locally', async ({ page }) => {
    await page.goto('/documents/new');
    await expect(page.getByRole('heading', { name: 'إدخال ملف جديد' })).toBeVisible();

    const submit = page.locator('form button[type="submit"]');
    await expect(submit).toBeEnabled();

    let documentPosts = 0;
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().includes('/api/documents')) documentPosts += 1;
    });

    // «منفذ عليها» يفرض رقم وسنة الملف — نموذج فارغ يجب أن يُرفض قبل الإرسال.
    await page.getByRole('radio', { name: 'الجهة العامة منفذ عليها' }).check();
    await submit.click();

    await expect(page.getByRole('alert')).toContainText('يجب أن يكون مقيدًا برقم وسنة الملف');
    await expect(page).toHaveURL(/\/documents\/new$/);
    expect(documentPosts, 'no destructive POST may leave the browser').toBe(0);
  });
});
