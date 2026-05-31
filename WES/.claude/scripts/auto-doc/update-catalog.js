const fs = require('fs');
const path = require('path');

const SCRIPTS_ROOT = 'C:\\GitFork\\WES_Project\\WES\\Assets\\Scripts';
const VAULT_CLASS_DIR = 'C:\\GitFork\\WES_Project\\document\\auto\\catalog\\Class';
const VAULT_SIGNAL_DIR = 'C:\\GitFork\\WES_Project\\document\\auto\\catalog\\Signal';

const TARGETS = ['Manager', 'Controller', 'Worker', 'Component'];

function extractAllClasses(content) {
  // All class declarations in file (not just first)
  const re = /(?:public|internal|private|protected)?\s*(?:abstract\s+|partial\s+|sealed\s+|static\s+)*class\s+(\w+)(?:\s*<[^>]+>)?(?:\s*:\s*([\w\s.<>,]+?))?\s*(?:where[^{]*)?\s*\{/g;
  const out = [];
  let m;
  while ((m = re.exec(content)) !== null) {
    let parent = null;
    if (m[2]) {
      parent = m[2].split(',')[0].trim().replace(/<[^>]*>/g, '').trim();
    }
    out.push({ name: m[1], parent });
  }
  return out;
}

function extractRole(content, className) {
  const lines = content.split('\n');
  const classLineIdx = lines.findIndex(ln => new RegExp(`class\\s+${className}\\b`).test(ln));
  if (classLineIdx < 0) return '';
  // walk backwards collecting comments
  const comments = [];
  for (let j = classLineIdx - 1; j >= 0; j--) {
    const ln = lines[j].trim();
    if (ln.startsWith('///')) {
      comments.unshift(ln.replace(/^\/\/\/\s?/, ''));
    } else if (ln.startsWith('//')) {
      comments.unshift(ln.replace(/^\/\/\s?/, ''));
    } else if (ln === '' || ln.startsWith('[')) {
      continue;
    } else {
      break;
    }
  }
  if (comments.length === 0) return '';
  const text = comments.join(' ');
  const sm = text.match(/<summary>(.*?)<\/summary>/i);
  if (sm) return sm[1].trim();
  return text.replace(/<[^>]+>/g, '').trim();
}

function extractEvents(content, ownerClass) {
  // public [static] event TYPE NAME;
  const signals = [];
  const re = /public\s+(static\s+)?event\s+([\w.<>,\s?]+?)\s+(\w+)\s*;/g;
  let m;
  while ((m = re.exec(content)) !== null) {
    signals.push({
      kind: 'Event',
      name: m[3],
      qualified: `${ownerClass}.${m[3]}`,
      owner: ownerClass,
      isStatic: !!m[1],
      signature: m[2].trim().replace(/\s+/g, ' '),
    });
  }
  return signals;
}

function yamlEscape(s) {
  if (s == null) return '';
  return String(s).replace(/"/g, '\\"');
}

function buildClassMd(cls) {
  const parentField = cls.parent ? `"[[${cls.parent}]]"` : 'null';
  const roleEsc = yamlEscape(cls.role || '');
  return `---
name: ${cls.name}
category: ${cls.category}
parent: ${parentField}
file_path: ${cls.file_path}
role: "${roleEsc}"
status: Active
signals: []
---

# ${cls.name}

${cls.role || '(설명 미정 — 후속 작업으로 채워짐)'}

## 관련

- 부모: ${cls.parent ? `[[${cls.parent}]]` : '(없음)'}
`;
}

function buildSignalMd(sig) {
  const sigEsc = yamlEscape(sig.signature);
  return `---
name: ${sig.qualified}
kind: Event
owner: "[[${sig.owner}]]"
signature: "${sigEsc}"
direction: Local
authority: ${sig.isStatic ? 'Static' : 'Instance'}
frequency: (미정)
subscribers: []
status: Active
---

# ${sig.qualified}

(자동 생성 — 발사 조건 및 구독자 시드 후 후속 작업으로 채워짐)

## 시그니처

\`\`\`csharp
public ${sig.isStatic ? 'static ' : ''}event ${sig.signature} ${sig.name};
\`\`\`

## 관련

- 발사 주체: [[${sig.owner}]]
`;
}

const classResults = [];
const signalResults = [];

for (const category of TARGETS) {
  const dir = path.join(SCRIPTS_ROOT, category);
  if (!fs.existsSync(dir)) continue;
  for (const file of fs.readdirSync(dir)) {
    if (!file.endsWith('.cs')) continue;
    const fp = path.join(dir, file);
    const content = fs.readFileSync(fp, 'utf-8');
    const classes = extractAllClasses(content);
    if (classes.length === 0) {
      console.log(`SKIP ${category}/${file} — no class`);
      continue;
    }
    const rel = path.relative('C:\\GitFork\\WES_Project', fp).replace(/\\/g, '/');
    for (const cls of classes) {
      const role = extractRole(content, cls.name);
      classResults.push({ ...cls, category, file_path: rel, role });
    }
    // Events attributed to the LAST class in the file (typically the "main" class in Unity multi-class .cs files)
    const mainClass = classes[classes.length - 1].name;
    const events = extractEvents(content, mainClass);
    signalResults.push(...events);
  }
}

console.log(`Classes: ${classResults.length}, Events: ${signalResults.length}`);

let classCreated = 0, classSkipped = 0;
for (const cls of classResults) {
  const out = path.join(VAULT_CLASS_DIR, `${cls.name}.md`);
  if (fs.existsSync(out)) { classSkipped++; continue; }
  fs.writeFileSync(out, buildClassMd(cls), 'utf-8');
  classCreated++;
}

let sigCreated = 0, sigSkipped = 0;
for (const sig of signalResults) {
  const out = path.join(VAULT_SIGNAL_DIR, `${sig.qualified}.md`);
  if (fs.existsSync(out)) { sigSkipped++; continue; }
  fs.writeFileSync(out, buildSignalMd(sig), 'utf-8');
  sigCreated++;
}

console.log(`Class .md: created=${classCreated}, skipped(existing)=${classSkipped}`);
console.log(`Signal .md: created=${sigCreated}, skipped(existing)=${sigSkipped}`);
