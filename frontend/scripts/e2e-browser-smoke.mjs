/**
 * `P6-04` — دخان تشغيل المتصفح فقط (بلا API/قاعدة).
 *
 * يثبت أن القناة المختارة تُشغّل متصفحًا فعليًا:
 *   - محليًا: `E2E_BROWSER_CHANNEL=chrome` (Chrome المثبَّت) افتراضيًا.
 *   - CI: `E2E_BROWSER_CHANNEL=chromium` (Chromium المثبَّت بالإصدار عبر playwright install).
 *
 * التشغيل: `npm run e2e:browser`
 */
import { chromium } from '@playwright/test';

const channel = process.env.E2E_BROWSER_CHANNEL ?? (process.env.CI ? '' : 'chrome');
const launchOptions = channel && channel !== 'chromium' ? { channel } : {};

const browser = await chromium.launch({ headless: true, ...launchOptions });
try {
  const page = await browser.newPage();
  await page.setContent('<h1>browser-smoke</h1>');
  const text = await page.textContent('h1');
  if (text !== 'browser-smoke') {
    throw new Error(`Unexpected page content: ${String(text)}`);
  }
  console.log(`browser smoke ok: version ${browser.version()} (${channel || 'bundled chromium'})`);
} finally {
  await browser.close();
}
