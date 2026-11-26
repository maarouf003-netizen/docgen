import { expect, type Page } from '@playwright/test';

/**
 * `P6-04` — مساعدات مشتركة لاختبارات E2E.
 *
 * كلمة المرور ثابتة ومعلنة عمدًا: قاعدة البيانات مؤقتة تُنشأ من `scripts/e2e/seed.sql`
 * (كلمة مرور معروفة للاختبار فقط، لا تصل أي بيئة منشورة).
 * انظر `docs/runbooks/e2e.md` §2.
 */
export const E2E_PASSWORD = 'E2eTest!Pass2026';

export const E2E_USERS = {
  manager: 'e2e.manager',
  head: 'e2e.head',
  lawyer: 'e2e.lawyer',
  portal: 'e2e.portal',
} as const;

/** دخول عبر الـAPI (كوكي الجلسة يُخزَّن في سياق المتصفح نفسه) — يختصر شاشة الدخول. */
export async function apiLogin(page: Page, username: string, password = E2E_PASSWORD): Promise<void> {
  const response = await page.request.post('/api/auth/login', {
    data: { username, password },
  });
  expect(response.status(), `API login for "${username}"`).toBe(200);
  const body = (await response.json()) as { user?: { username?: string } };
  expect(body.user?.username, 'login response user').toBe(username);
}

/** دخول عبر النموذج (للتحقق من تدفق الواجهة نفسه). */
export async function uiLogin(page: Page, username: string, password = E2E_PASSWORD): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('اسم المستخدم', { exact: true }).fill(username);
  await page.getByLabel('كلمة المرور', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'دخول', exact: true }).click();
  await expect(page).not.toHaveURL(/\/login(?:[?#]|$)/);
}

/**
 * فحص تجاوز الجوال الأفقي (المطلوب: `scrollWidth <= innerWidth + 1`).
 * الهامش 1px لتقريب sub-pixel في المتصفحات.
 */
export async function expectNoHorizontalOverflow(page: Page, label: string): Promise<void> {
  const metrics = await page.evaluate(() => ({
    documentScrollWidth: document.documentElement.scrollWidth,
    bodyScrollWidth: document.body.scrollWidth,
    innerWidth: window.innerWidth,
  }));

  expect(
    metrics.documentScrollWidth,
    `${label}: documentElement.scrollWidth (${metrics.documentScrollWidth}) exceeds innerWidth (${metrics.innerWidth})`,
  ).toBeLessThanOrEqual(metrics.innerWidth + 1);

  expect(
    metrics.bodyScrollWidth,
    `${label}: body.scrollWidth (${metrics.bodyScrollWidth}) exceeds innerWidth (${metrics.innerWidth})`,
  ).toBeLessThanOrEqual(metrics.innerWidth + 1);
}

export interface ConsoleProblem {
  kind: 'pageerror' | 'console.error';
  text: string;
  location: string;
}

/**
 * قائمة تبييض موثّقة لضجيج المتصفح الشبكي (ليس خطأ JavaScript):
 *  - `401` على `/api/auth/me` قبل الدخول: فحص جلسة متوقع في `AuthProvider`
 *    (يُترجم إلى `user=null` بلا استثناء).
 *  - `404` على `/favicon.ico`: لا ملف أيقونة في `public/`؛ لا أثر وظيفي.
 * أي شيء آخر يفشل الاختبار.
 */
const ALLOWED_NETWORK_NOISE: ReadonlyArray<{ status: number; path: string; reason: string }> = [
  { status: 401, path: '/api/auth/me', reason: 'anonymous session probe before login (AuthProvider)' },
  { status: 404, path: '/favicon.ico', reason: 'no favicon asset served by Vite' },
];

function isAllowedNetworkNoise(text: string, location: string): boolean {
  const match = /Failed to load resource: the server responded with a status of (\d{3})/.exec(text);
  if (!match) return false;
  const status = Number(match[1]);
  return ALLOWED_NETWORK_NOISE.some(
    (entry) => entry.status === status && new URL(location, 'http://127.0.0.1').pathname === entry.path,
  );
}

/**
 * يجمع أخطاء الصفحة غير الملتقطة و`console.error` (عدا ضجيج الشبكة المعلن أعلاه).
 * يبدأ من لحظة الاستدعاء — يُستدعى قبل `page.goto` للصفحة المراد فحصها.
 */
export function trackConsoleProblems(page: Page): ConsoleProblem[] {
  const problems: ConsoleProblem[] = [];

  page.on('pageerror', (error) => {
    problems.push({ kind: 'pageerror', text: error.message, location: '' });
  });

  page.on('console', (message) => {
    if (message.type() !== 'error') return;
    const text = message.text();
    const location = message.location().url ?? '';
    if (isAllowedNetworkNoise(text, location)) return;
    problems.push({ kind: 'console.error', text, location });
  });

  return problems;
}

export function formatConsoleProblems(problems: readonly ConsoleProblem[]): string {
  if (problems.length === 0) return 'no console errors';
  return problems.map((problem) => `${problem.kind}: ${problem.text} (${problem.location || 'unknown'})`).join('\n');
}
