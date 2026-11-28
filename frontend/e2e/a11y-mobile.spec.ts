import { expect, test, type Page } from '@playwright/test';
import { apiLogin, E2E_USERS, expectNoHorizontalOverflow } from './support/app.ts';
import { analyzeBlocking } from './support/a11y.ts';

/**
 * `P6-04a` — `axe` + تجاوز أفقي على جوال (375px) لشاشات تفاصيل الملف الأربعة.
 *
 * يُنشئ ملفًا عبر الـAPI (اختبار فقط، قاعدة E2E مؤقتة) ثم **يحذفه في `finally`**
 * حتى لا يعتمد ترتيب الاختبارات على صف مشترك (اختبارات «القائمة الفارغة»).
 */
async function createDocument(page: Page): Promise<{ id: number; csrf: string }> {
  const cookies = await page.context().cookies();
  const csrf = cookies.find((cookie) => cookie.name === 'docgen_csrf')?.value;
  expect(csrf, 'CSRF cookie after API login').toBeTruthy();

  const response = await page.request.post('/api/documents', {
    headers: { 'X-CSRF-Token': csrf! },
    data: {
      documentType: 'بيان دعوى',
      borrowerName: 'منفذ عليه اختبار الوصول',
      applicant: 'طالب تنفيذ اختبار الوصول',
      court: 'دمشق',
      contractType: 'تعهد',
      amountNumeric: 100,
      branchName: 'فرع دمشق — اختبار E2E',
    },
  });
  expect(response.status(), await response.text()).toBe(201);
  return { id: (await response.json()).id as number, csrf: csrf! };
}

test.describe('accessibility (mobile detail)', () => {
  test('document detail tabs at 375px have no critical/serious violations', async ({ page }) => {
    await apiLogin(page, E2E_USERS.lawyer);
    const { id, csrf } = await createDocument(page);

    try {
      await page.goto(`/documents/${id}`);

      for (const label of ['المعلومات', 'السند والأموال', 'الإنابات والوقوعات', 'الحالة']) {
        const tab = page.getByRole('tab', { name: label });
        await expect(tab).toBeVisible();
        if (label !== 'المعلومات') await tab.click();
        await expect(tab).toHaveAttribute('aria-selected', 'true');
        await expectNoHorizontalOverflow(page, `/documents/${id}#${label}`);
        await analyzeBlocking(page, `/documents/${id} (${label}, 375px)`);
      }
    } finally {
      // حذف منطقي: القائمة الافتراضية تستثني المحذوف فلا تتلوث اختبارات الفراغ.
      const deleted = await page.request.delete(`/api/documents/${id}`, {
        headers: { 'X-CSRF-Token': csrf },
      });
      expect(deleted.status(), 'cleanup soft-delete').toBe(204);
    }
  });
});
