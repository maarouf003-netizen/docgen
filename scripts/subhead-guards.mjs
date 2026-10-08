/**
 * حراس انحدار خطة رئيس الشعبة (المرحلة 9 — بند 4):
 * يفشل عند أي نمط يعيد إنتاج صنف «التضييق المنسي» الذي لدغنا 3 مرات:
 *  1. نداء `GET /users/lawyers` بلا `mode` صريح (الافتراضي `mine` يستبعد الجدد).
 *  2. فحص دور `head` منفرد في كود الإنتاج خارج قائمة السماح المعللة.
 *  3. سمة `[Authorize(Roles=...)]` تذكر `head` بلا `subhead` خارج السماح.
 *
 * الاستثناءات في قوائم السماح أدناه — أي إضافة لها تتطلب تعليلًا في نفس السطر.
 * يُشغَّل في `CI` (job ‏`guards`) ومحليًا: `node scripts/subhead-guards.mjs`.
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const ROOT = join(import.meta.dirname, '..');

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === 'dist' || entry === 'bin' || entry === 'obj') continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
}

const failures = [];
const allowHeadOnlyFrontend = new Map([
  // ملف: السبب — يُقرأ عند أي تعديل هنا
  ['frontend/src/auth/AuthContext.tsx', 'تعريف راية isHead الدلالية (ليست بوابة)'],
  ['frontend/src/auth/auth-context.ts', 'تعريف نوع السياق'],
  ['frontend/src/App.tsx', 'سباكة RequireRole + مسار audit-logs المغلق عمدًا (قرار 23)'],
  ['frontend/src/pages/AppealDetail.tsx', 'زر «إعادة الإحالة للشعبة» لرئيس القسم حصرًا (§6.5)'],
  ['frontend/src/pages/DelegationRequests.tsx', 'التوجيه/التراجع لرئيس قسم الفرع المناب حصرًا (§7.3)'],
]);
const allowHeadOnlyBackend = new Map([
  ['backend/src/DocGenerator.Api/Controllers/AuditLogsController.cs', 'النطاق الدائري مغلق حتى ownerSectionId (قرار 23)'],
]);

for (const file of walk(ROOT)) {
  const rel = relative(ROOT, file).replaceAll('\\', '/');
  let text;
  try {
    text = readFileSync(file, 'utf8');
  } catch {
    continue;
  }
  const lines = text.split('\n');

  // 1) نداءات قائمة المحامين بلا وضع صريح (عدا الاختبارات التي تحاكي الـAPI).
  if (/^frontend\/src\//.test(rel) && !/\.test\.(ts|tsx)$/.test(rel) && /\.(ts|tsx)$/.test(rel)) {
    lines.forEach((line, i) => {
      if (line.includes('/users/lawyers') && line.includes('api.get') && !line.includes('mode')) {
        // قد يكون الـparams في سطر مجاور — تحقق من نافذة ±3 أسطر قبل الحكم.
        const window = lines.slice(Math.max(0, i - 3), i + 4).join('\n');
        if (!window.includes('mode')) {
          failures.push(`${rel}:${i + 1}: GET /users/lawyers بلا mode صريح (استخدم mine/branch)`);
        }
      }
    });
  }

  // 2) فحص head منفرد في كود الإنتاج الأمامي (السطر الموحّد head||subhead مستثنى).
  if (/^frontend\/src\//.test(rel) && !/\.test\.(ts|tsx)$/.test(rel) && /\.(ts|tsx)$/.test(rel)) {
    lines.forEach((line, i) => {
      if (/role\s*===\s*['"]head['"]/.test(line) && !line.includes('subhead') && !allowHeadOnlyFrontend.has(rel)) {
        failures.push(`${rel}:${i + 1}: فحص head منفرد خارج قائمة السماح — أضف subhead أو علل في allowHeadOnlyFrontend`);
      }
    });
  }

  // 3) سمات التفويض الخلفية.
  if (/^backend\/src\/.*\.cs$/.test(rel)) {
    lines.forEach((line, i) => {
      const m = line.match(/Authorize\((Roles\s*=\s*"([^"]*)"|Policy\s*=\s*"([^"]*)")/);
      if (!m) return;
      const value = m[2] ?? m[3] ?? '';
      const roles = value.split(',').map((r) => r.trim());
      if (roles.includes('head') && !roles.includes('subhead') && !allowHeadOnlyBackend.has(rel)) {
        failures.push(`${rel}:${i + 1}: سمة تذكر head بلا subhead خارج السماح — وسّع أو علل في allowHeadOnlyBackend`);
      }
    });
  }
}

if (failures.length > 0) {
  console.error(`subhead-guards: ${failures.length} مخالفة:\n- ${failures.join('\n- ')}`);
  process.exit(1);
}
console.log('subhead-guards: نظيف — لا تضييق منسي.');
