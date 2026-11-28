import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';

/**
 * قائمة تبييض عمدًا **فارغة**: أي مخالفة `critical`/`serious` تفشل. أي إضافة مستقبلية
 * يجب أن تحمل سببًا مكتوبًا في `docs/runbooks/e2e.md` وتُزال عند الإصلاح.
 */
export const A11Y_ALLOWLIST: ReadonlyArray<{ id: string; reason: string }> = [];

/** يفشل عند أي مخالفة `critical`/`serious` غير مبيَّضة، ويطبع البقية في تقرير Playwright. */
export async function analyzeBlocking(page: Page, label: string): Promise<void> {
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
    nodes: violation.nodes.slice(0, 5).map((node) => ({
      target: node.target.join(' '),
      html: node.html,
      failureSummary: node.failureSummary,
    })),
  }));

  expect(
    summary,
    `${label}: ${unexpected.length} blocking axe violation(s). ` +
      'Fix them or document a temporary allowlist with a reason in e2e/support/a11y.ts and docs/runbooks/e2e.md.',
  ).toEqual([]);
}
