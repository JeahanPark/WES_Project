const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const CLASS_DIR = 'C:\\GitFork\\WES_Project\\document\\auto\\catalog\\Class';
const OUT_FILE = 'C:\\GitFork\\WES_Project\\document\\auto\\diagrams\\class\\WES-Class-Overview.canvas';

function uid() { return crypto.randomBytes(8).toString('hex'); }

function parseFrontmatter(md) {
  const m = md.match(/^---\n([\s\S]*?)\n---/);
  if (!m) return {};
  const fm = {};
  for (const line of m[1].split('\n')) {
    const km = line.match(/^(\w+):\s*(.*)$/);
    if (!km) continue;
    let v = km[2].trim();
    if (v.startsWith('"') && v.endsWith('"')) v = v.slice(1, -1);
    v = v.replace(/^\[\[(.+)\]\]$/, '$1');
    if (v === 'null' || v === '') v = null;
    fm[km[1]] = v;
  }
  return fm;
}

// Load classes
const classes = [];
const byName = {};
for (const file of fs.readdirSync(CLASS_DIR)) {
  if (!file.endsWith('.md')) continue;
  const fm = parseFrontmatter(fs.readFileSync(path.join(CLASS_DIR, file), 'utf-8'));
  if (!fm.name) continue;
  const cls = { name: fm.name, category: fm.category, parent: fm.parent, file: `auto/catalog/Class/${file}` };
  classes.push(cls);
  byName[fm.name] = cls;
}

const externalParents = new Set();
for (const c of classes) {
  if (c.parent && !byName[c.parent]) externalParents.add(c.parent);
}

const CATEGORIES = ['Manager', 'Controller', 'Worker', 'Component'];
const catColor = { Manager: '2', Controller: '1', Worker: '4', Component: '5' };
const catLabel = { Manager: 'Manager', Controller: 'Controller', Worker: 'Worker', Component: 'Component' };
const grouped = {};
CATEGORIES.forEach(c => grouped[c] = []);
for (const c of classes) {
  if (grouped[c.category]) grouped[c.category].push(c);
}
CATEGORIES.forEach(c => grouped[c].sort((a, b) => a.name.localeCompare(b.name)));

const CARD_W = 360, CARD_H = 100;
const COL_GAP = 40, ROW_GAP = 45;
const GROUP_PAD_X = 40, GROUP_PAD_TOP = 60, GROUP_PAD_BOTTOM = 40;
const SECTION_GAP = 100;
const CARDS_PER_COL = 2;

const nodes = [];
const edges = [];
const nodeIdByName = {};

// 1. External bases — top row, plain colored text cards
const extList = Array.from(externalParents).sort();
const extRowY = 0;
let extX = 0;
const extColor = '6'; // purple
const extGap = 30;

extList.forEach((name) => {
  const id = uid();
  nodeIdByName['__ext__' + name] = id;
  nodes.push({
    id, type: 'text',
    x: extX, y: extRowY,
    width: CARD_W, height: CARD_H,
    text: `### ${name}\n*(외부 베이스)*`,
    color: extColor
  });
  extX += CARD_W + extGap;
});

const baseRowBottom = extRowY + CARD_H + SECTION_GAP;

// 2. Category groups in 2x2 grid
const COLS = 2;
const groupSizes = CATEGORIES.map(cat => {
  const cards = grouped[cat];
  const cols = CARDS_PER_COL;
  const rows = Math.ceil(cards.length / cols);
  const w = cols * CARD_W + (cols - 1) * COL_GAP + 2 * GROUP_PAD_X;
  const h = rows * CARD_H + (rows - 1) * ROW_GAP + GROUP_PAD_TOP + GROUP_PAD_BOTTOM;
  return { w, h, rows };
});

// Compute row heights and column widths for 2x2 grid
const rowMaxH = [
  Math.max(groupSizes[0].h, groupSizes[1].h),
  Math.max(groupSizes[2].h, groupSizes[3].h),
];
const colMaxW = [
  Math.max(groupSizes[0].w, groupSizes[2].w),
  Math.max(groupSizes[1].w, groupSizes[3].w),
];

const groupRects = [];
CATEGORIES.forEach((cat, idx) => {
  const col = idx % COLS;
  const row = Math.floor(idx / COLS);
  const x = col === 0 ? 0 : colMaxW[0] + SECTION_GAP;
  const y = baseRowBottom + (row === 0 ? 0 : rowMaxH[0] + SECTION_GAP);
  groupRects.push({ x, y, w: groupSizes[idx].w, h: groupSizes[idx].h });
});

CATEGORIES.forEach((cat, idx) => {
  const rect = groupRects[idx];
  const groupId = uid();
  nodes.push({
    id: groupId, type: 'group',
    x: rect.x, y: rect.y,
    width: rect.w, height: rect.h,
    label: catLabel[cat],
    color: catColor[cat]
  });

  const cards = grouped[cat];
  cards.forEach((cls, i) => {
    const c = i % CARDS_PER_COL;
    const r = Math.floor(i / CARDS_PER_COL);
    const x = rect.x + GROUP_PAD_X + c * (CARD_W + COL_GAP);
    const y = rect.y + GROUP_PAD_TOP + r * (CARD_H + ROW_GAP);
    const id = uid();
    nodeIdByName[cls.name] = id;
    nodes.push({
      id, type: 'file',
      x, y,
      width: CARD_W, height: CARD_H,
      file: cls.file,
      color: catColor[cat]
    });
  });
});

// 3. Edges: child → parent. ONLY in-catalog relationships.
// External base relations (to MonoBehaviour / NetworkBehaviour / IManager) are
// intentionally omitted — they create dense crossing lines without informational
// value, since they're visible on each class card's frontmatter.
for (const cls of classes) {
  if (!cls.parent) continue;
  const toId = nodeIdByName[cls.parent];   // only in-catalog
  if (!toId) continue;
  const fromId = nodeIdByName[cls.name];
  if (!fromId) continue;
  edges.push({
    id: uid(),
    fromNode: fromId, fromSide: 'top',
    toNode: toId, toSide: 'bottom'
  });
}

const canvas = { nodes, edges };
fs.writeFileSync(OUT_FILE, JSON.stringify(canvas, null, 2), 'utf-8');
console.log(`wrote ${OUT_FILE} — ${nodes.length} nodes, ${edges.length} edges`);
