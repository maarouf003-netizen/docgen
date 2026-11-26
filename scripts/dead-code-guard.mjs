#!/usr/bin/env node
// P4-POST-BATCH (2026-11-26): يمنع بقاء حقول `private` (readonly أو متغيّرة) معلنة
// ومُسنَدة بلا أي استخدام (دين نقل الملكية: حقل خدمة/معالج/اختبار بقي بعد نقل آخر مستخدميه).
//
// المنهج: لكل عائلة `partial class` (namespace + class name) نجمع نصوص ملفاتها،
// ثم نعدّ كل حقل `private` ونبحث عن اسمه خارج سطر التصريح وإسناد المُنشئ/الكتابة
// المباشرة. صفر قراءة = حقل ميت يفشل البوابة.
//
// حدود مقصودة:
// - `protected`/`internal` مستثناة (قد يستهلكها وارث/اختبار).
// - الحقول متعددة الأسطر ذات المهيّئات الكبيرة لا تُحلَّل (لا تُنتج إنذارًا كاذبًا).
// - التعليقات تُجرَّد قبل العدّ كي لا يعتبر ذكرُ الحقل في تعليق «استخدامًا».
//
// التشغيل: node scripts/dead-code-guard.mjs [--self-test]

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '..');

const SCAN_ROOTS = [
  'backend/src',
  'backend/tests',
];

const FIELD_RE =
  /^\s*private\s+(?:readonly\s+)?(?:static\s+)?(?:const\s+)?[\w<>[\],.?\s]+\s+([_a-zA-Z]\w*)\s*(?:=(?!>)[^;]*;|;)\s*$/;
const CLASS_RE =
  /\b(?:public|internal|private|protected)\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*class\s+(\w+)/g;

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (['bin', 'obj', 'node_modules'].includes(entry.name)) continue;
      walk(full, out);
    } else if (entry.name.endsWith('.cs')) {
      out.push(full);
    }
  }
  return out;
}

/** يجرّد تعليقات C# مع الحفاظ على أسطر مستخدمة للعزل. */
export function stripComments(text) {
  return text
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/[^\r\n]*/g, '');
}

/**
 * يحلّل خريطة {filePath: text} ويعيد قائمة الحقول الميتة.
 * مُصدَّر لاختبار ذاتي بلا لمس القرص.
 */
export function findDeadFields(files) {
  const families = new Map();
  for (const [file, raw] of Object.entries(files)) {
    const text = stripComments(raw);
    const ns = (text.match(/^namespace\s+([\w.]+)/m) || [, ''])[1];
    let match;
    CLASS_RE.lastIndex = 0;
    while ((match = CLASS_RE.exec(text))) {
      const key = `${ns}::${match[1]}`;
      if (!families.has(key)) families.set(key, { name: match[1], files: new Map() });
      families.get(key).files.set(file, text);
    }
  }

  const dead = [];
  for (const family of families.values()) {
    const declarations = [];
    const linesByFile = new Map();
    for (const [file, text] of family.files) {
      const lines = text.split(/\r?\n/);
      linesByFile.set(file, lines);
      for (let index = 0; index < lines.length; index++) {
        const match = lines[index].match(FIELD_RE);
        if (match) {
          declarations.push({ name: match[1], file, line: index + 1, source: lines[index].trim() });
        }
      }
    }
    for (const declaration of declarations) {
      const escaped = declaration.name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const anyUse = new RegExp(`(?<![\\w.])${escaped}\\b`);
      const assignmentLhs = new RegExp(`(?<![\\w.])(?:this\\.)?${escaped}\\s*=\\s*(?!=)`, 'g');
      let used = false;
      for (const lines of linesByFile.values()) {
        for (const line of lines) {
          const declarationMatch = line.match(FIELD_RE);
          if (declarationMatch && declarationMatch[1] === declaration.name) continue;
          if (!anyUse.test(line)) continue;
          // إزالة إسناد المُنشئ/الكتابة المباشرة: `_x = value` ليست قراءة.
          const remaining = line.replace(assignmentLhs, '');
          if (anyUse.test(remaining)) {
            used = true;
            break;
          }
        }
        if (used) break;
      }
      if (!used) dead.push({ ...declaration, className: family.name });
    }
  }
  return dead.sort((a, b) => `${a.file}:${a.line}`.localeCompare(`${b.file}:${b.line}`));
}

function collectRepositoryFiles() {
  const files = {};
  for (const root of SCAN_ROOTS) {
    const absolute = path.join(repoRoot, root);
    if (!fs.existsSync(absolute)) continue;
    for (const file of walk(absolute)) {
      files[path.relative(repoRoot, file).replaceAll('\\', '/')] = fs.readFileSync(file, 'utf8');
    }
  }
  return files;
}

function selfTest() {
  const cases = [
    {
      name: 'حقل مُصرَّح ومُسنَد فقط = ميت',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly IDep _dep;\n public A(IDep dep) { _dep = dep; }\n}\n',
      },
      expected: 1,
    },
    {
      name: 'حقل مستخدم في دالة = حي',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly IDep _dep;\n public A(IDep dep) { _dep = dep; }\n public void Run() { _dep.Do(); }\n}\n',
      },
      expected: 0,
    },
    {
      name: 'الإسناد وحده لا يكفي (حقل غير readonly أيضًا)',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private int _count;\n public void Set(int value) { _count = value; }\n}\n',
      },
      expected: 1,
    },
    {
      name: 'ذكر في تعليق لا يُعد استخدامًا',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly IDep _dep;\n // _dep لم يعد مستخدمًا\n public A(IDep dep) { _dep = dep; }\n}\n',
      },
      expected: 1,
    },
    {
      name: 'استخدام من partial آخر = حي',
      files: {
        'a.cs': 'namespace N;\npublic sealed partial class A {\n private readonly IDep _dep;\n public A(IDep dep) { _dep = dep; }\n}\n',
        'a.reads.cs': 'namespace N;\npublic sealed partial class A { public void Read() { _dep.Read(); } }\n',
      },
      expected: 0,
    },
    {
      name: 'قراءة الحقل على يمين إسناد = حي',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly IDep _dep;\n private readonly IDep _other;\n public A(IDep dep) { _dep = dep; _other = _dep; }\n}\n',
      },
      expected: 1, // `_other` ميت، `_dep` حي
    },
    {
      name: 'حقل بمهيّئ فقط وبلا استخدام = ميت',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly List<int> _items = new();\n}\n',
      },
      expected: 1,
    },
    {
      name: 'اسم حقل يشبه اسمًا آخر لا يُعد استخدامًا',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly IDep _dep;\n private readonly IDep _depot;\n public A(IDep dep) { _dep = dep; }\n public void Run() { _depot.Read(); }\n}\n',
      },
      expected: 1, // `_dep` ميت رغم وجود `_depot`
    },
    {
      name: 'nameof يُعد استخدامًا',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private readonly IDep _dep;\n public A(IDep dep) { _dep = dep; }\n public string Name => nameof(_dep);\n}\n',
      },
      expected: 0,
    },
    {
      name: 'خاصية معبّرة ليست حقلًا',
      files: {
        'a.cs': 'namespace N;\npublic sealed class A {\n private bool IsReady => true;\n}\n',
      },
      expected: 0,
    },
  ];

  let failed = 0;
  for (const testCase of cases) {
    const dead = findDeadFields(testCase.files);
    if (dead.length !== testCase.expected) {
      failed++;
      console.error(`SELF-TEST FAIL: ${testCase.name} — expected ${testCase.expected}, got ${dead.length}`);
    }
  }
  if (failed > 0) {
    console.error(`dead-code-guard self-test: ${failed} failure(s).`);
    process.exit(1);
  }
  console.log(`dead-code-guard self-test: ${cases.length}/${cases.length} cases passed.`);
}

if (process.argv.includes('--self-test')) {
  selfTest();
  process.exit(0);
}

const dead = findDeadFields(collectRepositoryFiles());
if (dead.length > 0) {
  console.error(`dead-code-guard: ${dead.length} حقل private بلا استخدام:`);
  for (const field of dead) {
    console.error(`  ${field.file}:${field.line} — ${field.className}.${field.name}`);
  }
  console.error('أزل الحقل (وبارامتر المُنشئ إن صار ميتًا) أو استخدمه؛ لا تُبقِ اعتمادية معلّقة.');
  process.exit(1);
}

console.log('dead-code-guard: نظيف — لا حقل private بلا استخدام.');
