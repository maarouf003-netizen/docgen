import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { apiLogin, E2E_USERS } from './support/app.ts';

/**
 * `P6-04` — تدقيق `axe` آلي على الشاشات الثلاث المطلوبة.
 *
 * العتبة: تفشل الاختبارات عند أي مخالفة `critical` أو `serious` فقط (المعيار
 * العملي لـ`DoD`)، وتُطبع بقية المخالفات (`moderate`/`minor`) في تقرير Playwright.
 *
 * قائمة التبييض عمدًا **فارغة**: أي مخالفة حرجة/خطيرة تفشل. أي إضافة مستقبلية
 * يجب أن تحمل سببًا مكتوبًا هنا وفي `docs/runbooks/e2e.md`، وتُزال عند الإصلاح.
 */
const A11Y_ALLOWLIST: ReadonlyArray<{ id: string; reason: string }> = [];

async function analyzeBlocking(page: Page, label: string): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22a', 'wcag22aa'])
    .analyze();

  const blocking = results.violations.filter(
    (violation) => violation.impact === 'critical' || violation.impact === 'serious',
  );
  const unexpected = blocking.filter(
    (violation) => !A11Y_ALLOWLIST.some((entry) => entry.id === violation.id),
  );

  const summary = unexpected.map((violation) => ({
    id: violation.id,
    impact: violation.impact,
    help: violation.help,
    nodes: violation.nodes.slice(0, 5).map((node) => node.target.join(' ')),
  }));

  expect(
    summary,
    `${label}: ${unexpected.length} blocking axe violation(s). ` +
      'Fix them or document a temporary allowlist with a reason in e2e/a11y.spec.ts and docs/runbooks/e2e.md.',
  ).toEqual([]);
}

test.describe('accessibility (axe)', () => {
  test('login page has no critical/serious violations', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'مسار' })).toBeVisible();
    await analyzeBlocking(page, '/login');
  });

  test('documents list has no critical/serious violations', async ({ page }) => {
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/documents');
    await expect(page.getByRole('heading', { name: 'الملفات التنفيذية' })).toBeVisible();
    await expect(page.getByText('لا توجد نتائج', { exact: true })).toBeVisible();
    await analyzeBlocking(page, '/documents');
  });

  test('alerts page has no critical/serious violations', async ({ page }) => {
    // رئيس قسم (له فرع) يرى تبويبَي «مستلمة/مرسلة» — تغطية أوسع من عرض المدير
    // (الذي لا يرى تبويبات)، فتدقيق axe يلامس عقد `role=tab`/`aria-controls` أيضًا.
    await apiLogin(page, E2E_USERS.head);
    await page.goto('/alerts');
    await expect(page.getByRole('heading', { name: 'التنبيهات' })).toBeVisible();
    await expect(page.getByRole('tab', { name: /مستلمة/ })).toBeVisible();
    await expect(page.getByText(/لا توجد تنبيهات/)).toBeVisible();
    await analyzeBlocking(page, '/alerts');
  });

  test('alerts oversight view (manager) has no critical/serious violations', async ({ page }) => {
    // عرض المدير مسار تركيب مختلف: بلا تبويبات ولا `tabpanel` — يُدقَّق مستقلًا
    // كي لا يمر مرجع ARIA إلى عنصر غير مُصيَّر.
    await apiLogin(page, E2E_USERS.manager);
    await page.goto('/alerts');
    await expect(page.getByRole('heading', { name: 'التنبيهات' })).toBeVisible();
    await expect(page.getByText(/لا توجد تنبيهات/)).toBeVisible();
    await analyzeBlocking(page, '/alerts (oversight)');
  });
});
