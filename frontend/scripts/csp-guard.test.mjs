import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { analyzeCss, analyzeDist, analyzeHtml, analyzeScript } from './csp-guard.mjs';

/** يبني مجلد `dist` مؤقتًا من خريطة مسار←محتوى، ويعيد جذره. */
function createDist(files) {
  const root = mkdtempSync(join(tmpdir(), 'csp-guard-'));
  for (const [relativePath, content] of Object.entries(files)) {
    const fullPath = join(root, relativePath);
    mkdirSync(dirname(fullPath), { recursive: true });
    writeFileSync(fullPath, content);
  }
  return root;
}

/**
 * اختبارات حارس `CSP` (W0-CSP): تثبّت أن الحارس **يكشف** الأصناف الخمسة التي تُبيّض
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

describe('analyzeCss', () => {
  it('يقبل الأصول المحلية و data:/blob: والمعرّفات', () => {
    const css = `
      @import "./fonts.css";
      @font-face { src: url(/assets/cairo.woff2) format("woff2"); }
      .a { background: url("data:image/png;base64,AAAA"); }
      .b { mask: url(#mask); }
    `;

    expect(analyzeCss(css)).toEqual([]);
  });

  it('يرفض url إلى أصل خارجي', () => {
    expect(analyzeCss('.a { background: url(https://cdn.example/bg.png); }')).toHaveLength(1);
    expect(analyzeCss('.a { background: url("//cdn.example/bg.png"); }')).toHaveLength(1);
  });

  it('يرفض @import خارجيًا مرة واحدة بلا احتساب مزدوج', () => {
    const violations = analyzeCss('@import url("https://fonts.example/css");');

    expect(violations).toHaveLength(1);
    expect(violations[0]).toContain('@import');
  });
});

describe('analyzeDist', () => {
  it('يفشل برسالة توجيهية عند غياب dist', () => {
    const root = mkdtempSync(join(tmpdir(), 'csp-guard-missing-'));
    try {
      const result = analyzeDist(join(root, 'dist'));

      expect(result.violations).toHaveLength(1);
      expect(result.violations[0]).toContain('npm run build');
      expect(result.filesChecked).toBe(0);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it('يقبل مخرجات dist نظيفة ويعدّ المستند والحزم', () => {
    const root = createDist({
      'index.html': `<!doctype html><html><head>
        <script type="module" crossorigin src="/assets/index-a.js"></script>
        <link rel="stylesheet" crossorigin href='/assets/index-a.css' />
      </head><body></body></html>`,
      'assets/index-a.js': 'export {};\n',
      'assets/index-a.css': ':root { color-scheme: light; }\n',
    });
    try {
      const result = analyzeDist(root);

      expect(result.violations).toEqual([]);
      expect(result.filesChecked).toBe(3);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it('يكشف مرجع أصل غير موجود بعلامات تنصيص مفردة أيضًا', () => {
    const root = createDist({
      'index.html': `<script type="module" src='/assets/missing.js'></script>`,
    });
    try {
      const result = analyzeDist(root);

      expect(result.violations).toHaveLength(1);
      expect(result.violations[0]).toContain('/assets/missing.js');
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it('يفحص أوراق CSS المبنية ضد الأصول الخارجية', () => {
    const root = createDist({
      'index.html': `<link rel="stylesheet" href="/assets/index-a.css" />`,
      'assets/index-a.css': '.a { background: url(https://cdn.example/bg.png); }\n',
    });
    try {
      const result = analyzeDist(root);

      expect(result.violations).toHaveLength(1);
      expect(result.violations[0]).toContain('index-a.css');
      expect(result.filesChecked).toBe(2);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });
});
