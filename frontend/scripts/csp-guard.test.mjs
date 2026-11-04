import { describe, expect, it } from 'vitest';
import { analyzeHtml, analyzeScript } from './csp-guard.mjs';

/**
 * اختبارات حارس `CSP` (W0-CSP): تثبّت أن الحارس **يكشف** الأصناف الأربعة التي تُبيّض
 * الصفحة عند الفرض، وأنه **لا يعطي إيجابيات كاذبة** على شكل مخرجات `Vite` الفعلي.
 * الحارس نفسه يُشغَّل على `dist` بعد البناء في `CI`؛ هذه الاختبارات تحرس منطقه.
 */
describe('analyzeHtml', () => {
  const viteShapedHtml = `<!doctype html>
<html lang="ar" dir="rtl">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <title>مسار</title>
    <script type="module" crossorigin src="/assets/index-BQTTNOeP.js"></script>
    <link rel="modulepreload" crossorigin href="/assets/jsx-runtime-DaNgFy8Z.js" />
    <link rel="stylesheet" crossorigin href="/assets/index-CqeT7bEW.css" />
  </head>
  <body><div id="root"></div></body>
</html>`;

  it('يقبل شكل مخرجات Vite النظيف بلا مخالفات', () => {
    expect(analyzeHtml(viteShapedHtml)).toEqual([]);
  });

  it('يرفض السكربت المضمّن بلا src', () => {
    const violations = analyzeHtml('<body><script>alert(1)</script></body>');

    expect(violations).toHaveLength(1);
    expect(violations[0]).toContain('سكربت مضمّن');
  });

  it('يقبل السكربت المضمّن الفارغ (نمط Vite القديم)', () => {
    expect(analyzeHtml('<body><script src="/assets/a.js"></script></body>')).toEqual([]);
  });

  it('يرفض معالج الحدث المضمّن', () => {
    const violations = analyzeHtml('<button onclick="doThing()">اضغط</button>');

    expect(violations.some((violation) => violation.includes('onclick'))).toBe(true);
  });

  it('يرفض الأصل الخارجي في وسوم تحميل الموارد', () => {
    expect(analyzeHtml('<script src="https://cdn.example/app.js"></script>')).toHaveLength(1);
    expect(analyzeHtml('<link rel="stylesheet" href="//cdn.example/app.css" />')).toHaveLength(1);
    expect(analyzeHtml('<img src="https://tracker.example/p.gif" alt="" />')).toHaveLength(1);
  });

  it('يسمح بـ data:/blob: للصور فقط', () => {
    expect(analyzeHtml('<img src="data:image/png;base64,AAAA" alt="" />')).toEqual([]);
    expect(analyzeHtml('<script src="data:text/javascript,alert(1)"></script>')).toHaveLength(1);
    // `media-src` غير مضبوطة فتتبع default-src 'self' — لا data: للفيديو/الصوت.
    expect(analyzeHtml('<video src="data:video/mp4;base64,AAAA"></video>')).toHaveLength(1);
  });

  it('يرفض مخطط javascript: في أي سمة', () => {
    const violations = analyzeHtml('<a href="javascript:alert(1)">رابط</a>');

    expect(violations.some((violation) => violation.includes('javascript:'))).toBe(true);
  });

  it('يرفض base خارج الأصل ويقبل المسار النسبي', () => {
    expect(analyzeHtml('<base href="https://evil.example/" />')).toHaveLength(1);
    expect(analyzeHtml('<base href="/app/" />')).toEqual([]);
  });

  it('يتعامل مع srcset كقائمة عناوين', () => {
    expect(analyzeHtml('<img srcset="/a.png 1x, https://cdn.example/b.png 2x" alt="" />')).toHaveLength(1);
  });
});

describe('analyzeScript', () => {
  it('يقبل حزمة نظيفة', () => {
    expect(analyzeScript('const x = { eval: 1 }; obj.evaluate(2);')).toEqual([]);
  });

  it('يرفض eval و new Function و Function(...)', () => {
    expect(analyzeScript('eval("1")')).toHaveLength(1);
    expect(analyzeScript('new Function("return 1")')).toHaveLength(1);
    expect(analyzeScript('Function("return 1")()')).toHaveLength(1);
  });

  it('يرفض WebAssembly لأن script-src بلا wasm-unsafe-eval', () => {
    expect(analyzeScript('WebAssembly.instantiate(bytes)')).toHaveLength(1);
  });
});
