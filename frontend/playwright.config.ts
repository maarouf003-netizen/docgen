import { defineConfig, type PlaywrightTestConfig } from '@playwright/test';
import { assertSafeE2eDatabase, assertSafeE2eTarget } from './scripts/e2e-safety-guard.mjs';

/**
 * `P6-04` — إعداد `Playwright` للتدفقات الحرجة، وإمكانية الوصول (`axe`)،
 * وتجاوز الجوال الأفقي.
 *
 * قواعد السلامة (إلزامية — انظر `docs/runbooks/e2e.md`):
 *  - `E2E_BASE_URL` محلي فقط (`localhost`/`127.0.0.1`) إلا مع `E2E_ALLOW_STAGING=1`
 *    **و** احتواء المضيف على `staging`. الحارس `scripts/e2e-safety-guard.mjs` يفشل
 *    الإعداد كله قبل أي متصفح أو خادم.
 *  - في وضع `E2E_MANAGED_SERVERS=1` تُشترط قاعدة مخصّصة (`E2E_DB_CONNECTION`) اسمها
 *    يحتوي `e2e` ومضيفها محلي/staging. لا قيمة افتراضية مضمّنة (لا كلمات مرور في المستودع).
 *  - لا خوادم خارجية مشتركة: API يُشغَّل بـ`reuseExistingServer: false` كي لا تُجرى
 *    الاختبارات صامتةً على قاعدة مطوّر أخرى.
 *
 * المتغيّرات:
 *  - `E2E_BASE_URL` (افتراضي `http://127.0.0.1:5173`).
 *  - `E2E_API_URL` (افتراضي `http://127.0.0.1:5080`) — هدف وسيط `Vite`.
 *  - `E2E_MANAGED_SERVERS=1` لتشغيل API + الواجهة تلقائيًا وانتظار `/readyz`.
 *  - `E2E_DB_CONNECTION` سلسلة اتصال القاعدة المؤقتة (إلزامية مع الإدارة).
 *  - `E2E_BROWSER_CHANNEL` (`chrome` محليًا افتراضيًا، `chromium` في CI —
 *    القيمة `chromium` تعني متصفح Playwright المثبَّت والمثبَّت بالإصدار).
 *  - `E2E_ALLOW_STAGING=1` لبيئة staging معلنة فقط.
 */

const baseURL = (process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5173').replace(/\/+$/, '');
assertSafeE2eTarget(baseURL, process.env);

const managedServers = process.env.E2E_MANAGED_SERVERS === '1';
const apiUrl = (process.env.E2E_API_URL ?? 'http://127.0.0.1:5080').replace(/\/+$/, '');
assertSafeE2eTarget(apiUrl, process.env);

/**
 * `chrome` محليًا يستهلك Chrome المثبَّت على الجهاز (لا تنزيل إضافي).
 * `chromium` (أو فراغ في CI) يستخدم Chromium المثبَّت بـ`npx playwright install chromium`
 * — وهو الخيار الحتمي في CI.
 */
const browserChannel = process.env.E2E_BROWSER_CHANNEL ?? (process.env.CI ? '' : 'chrome');
const channel = browserChannel && browserChannel !== 'chromium' ? { channel: browserChannel } : {};

const databaseConnection = process.env.E2E_DB_CONNECTION ?? '';
if (managedServers && !databaseConnection.trim()) {
  throw new Error(
    'E2E_MANAGED_SERVERS=1 requires E2E_DB_CONNECTION pointing at a disposable database ' +
      '(name must contain "e2e", for example docgen_e2e). No default credentials are embedded.',
  );
}
// يُفحص متى وُجد — حتى عند الاتصال بخادم قائم، فلا تمر سلسلة إنتاج بالخطأ.
if (databaseConnection.trim()) {
  assertSafeE2eDatabase(databaseConnection, process.env);
}

/**
 * مفتاح توقيع JWT لخادم E2E المؤقت فقط — ليس سرًّا ولا يصل أي بيئة منشورة.
 * يمكن تجاوزه عبر `E2E_JWT_SECRET` (32 بايت على الأقل).
 */
const e2eJwtSecret =
  process.env.E2E_JWT_SECRET ?? 'e2e-not-a-real-signing-key-used-only-for-disposable-local-runs';

const webServers: PlaywrightTestConfig['webServer'] = managedServers
  ? [
      {
        // قاعدة بيانات مؤقتة + بذر حتمي مُطبَّقان قبل هذا الأمر (انظر runbook/CI).
        command:
          'dotnet run --project ../backend/src/DocGenerator.Api --no-build --no-launch-profile --urls http://127.0.0.1:5080',
        url: `${apiUrl}/readyz`,
        timeout: 180_000,
        // لا نعيد استخدام خادم قائم: قد يكون على قاعدة أخرى (سلامة البيانات أولًا).
        reuseExistingServer: false,
        stdout: 'pipe' as const,
        stderr: 'pipe' as const,
        env: {
          ASPNETCORE_ENVIRONMENT: 'Development',
          // Development يفتح أدوات محلية (Swagger/CORS) ويجعل كوكي CSRF غير Secure
          // فوق http — يلزم راية صريحة كما يفرض `Program.cs`.
          Security__AllowDevelopmentEnvironment: 'true',
          ConnectionStrings__DefaultConnection: databaseConnection,
          // الهجرات مُطبَّقة صراحةً قبل التشغيل؛ البذر الحتمي أُدخل بـSQL فلا يبذر التطبيق.
          Database__AutoMigrate: 'false',
          Jwt__Secret: e2eJwtSecret,
          // تقليل ضجيج EF/SQL في سجل webServer (قراءة فقط، لا أسرار) — يمنع سجلًا ضخمًا
          // في CI ويُبقي تحذيرات التطبيق ظاهرة.
          Logging__LogLevel__Default: 'Warning',
          Logging__LogLevel__Microsoft__EntityFrameworkCore: 'Warning',
          // حد دخول مرتفع لهذا التشغيل فقط (سجلّات الدخول المتكررة بين الملفات).
          // حد المعدل نفسه مغطّى باختبارات الخلفية، وليس هدف E2E.
          RateLimiting__LoginIpPerMinute: '200',
          AllowedHosts: '*',
        },
      },
      {
        command: 'npm run dev -- --host 127.0.0.1 --port 5173 --strictPort',
        url: baseURL,
        timeout: 120_000,
        reuseExistingServer: !process.env.CI,
        stdout: 'pipe' as const,
        stderr: 'pipe' as const,
        env: {
          // وسيط Vite يوجّه /api إلى الـAPI المحلي (نفس الأصل في المتصفح).
          VITE_API_TARGET: apiUrl,
        },
      },
    ]
  : undefined;

export default defineConfig({
  testDir: './e2e',
  outputDir: './test-results',
  // قاعدة بذر واحدة مشتركة + حد معدل دخول: عامل واحد يمنع تصادم البيانات
  // والطلبات المتوازية، والحزمة صغيرة بما لا يبرر التوازي.
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { outputFolder: 'playwright-report', open: 'never' }]],
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [
    {
      name: 'desktop',
      testIgnore: /(?:mobile-overflow|a11y-mobile)\.spec\.ts/,
      use: {
        browserName: 'chromium',
        viewport: { width: 1280, height: 800 },
        ...channel,
      },
    },
    {
      name: 'mobile',
      testMatch: /(?:mobile-overflow|a11y-mobile)\.spec\.ts/,
      use: {
        browserName: 'chromium',
        viewport: { width: 375, height: 812 },
        isMobile: true,
        hasTouch: true,
        deviceScaleFactor: 3,
        ...channel,
      },
    },
  ],
  webServer: webServers,
});
