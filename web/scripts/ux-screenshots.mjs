// Captures every student screen for the UX pass and writes a contact sheet.
//
//   node scripts/ux-screenshots.mjs <out-dir-name>        (from web/; e.g. ux-pass or ux-pass-after)
//
// Uses the installed Google Chrome through Playwright's `channel: 'chrome'`; it never downloads a browser
// (do not run `npx playwright install`). Needs the API (5080) and `ng serve` (4200) running on demo data.
// Output: docs/screenshots/<out-dir-name>/<user>__<lang>__<viewport>__<screen>.png and
// docs/screenshots/ux-pass/index.html (before | after side by side once ux-pass-after exists).
import { chromium } from 'playwright';
import { mkdir, readdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const shotsRoot = path.resolve(here, '../../docs/screenshots');
const outName = process.argv[2] ?? 'ux-pass';
const outDir = path.join(shotsRoot, outName);
const BASE = process.env.LIWA_WEB ?? 'http://localhost:4200';
const PASSWORD = 'Liwa#2027';

export const USERS = (process.env.LIWA_USERS ?? 'student.new,student.senior,student.transfer,student.mid1').split(',');
export const SCREENS = [
  ['1-home', '/student'],
  ['2-availability', '/student/availability'],
  ['3-planner', '/student/planner'],
  ['4-graduation', '/student/graduation'],
  ['5-my-plan', '/student/plan'],
  ['6-messages', '/student/messages'],
];
const VIEWPORTS = {
  desktop: { viewport: { width: 1440, height: 900 } },
  phone: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true },
};
const LANGS = ['en', 'ar'];

async function settle(page) {
  await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
  // Spinners gone, fonts loaded, animations done.
  await page.waitForFunction(() => !document.querySelector('mat-spinner, mat-progress-spinner, .loading'), null, { timeout: 15000 }).catch(() => {});
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(600);
}

async function signIn(page, username) {
  await page.goto(`${BASE}/login?lang=en`);
  await page.locator('input[formcontrolname="username"]').fill(username);
  await page.locator('input[formcontrolname="password"]').fill(PASSWORD);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL((u) => !u.pathname.startsWith('/login'), { timeout: 20000 });
}

async function capture() {
  await mkdir(outDir, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const user of USERS) {
      for (const [vpName, vp] of Object.entries(VIEWPORTS)) {
        const context = await browser.newContext({ ...vp, reducedMotion: 'reduce', colorScheme: 'light' });
        const page = await context.newPage();
        await signIn(page, user);
        for (const lang of LANGS) {
          for (const [screen, url] of SCREENS) {
            await page.goto(`${BASE}${url}?lang=${lang}`);
            await settle(page);
            const file = path.join(outDir, `${user}__${lang}__${vpName}__${screen}.png`);
            await page.screenshot({ path: file, fullPage: true });
            console.log(path.relative(shotsRoot, file));
          }
        }
        await context.close();
      }
    }
  } finally {
    await browser.close();
  }
}

/** docs/screenshots/index.html: one row per capture, "before" and "after" side by side. */
export async function contactSheet() {
  const dirs = ['ux-pass', 'ux-pass-after'].filter((d) => existsSync(path.join(shotsRoot, d)));
  const names = new Set();
  for (const d of dirs) for (const f of await readdir(path.join(shotsRoot, d))) if (f.endsWith('.png')) names.add(f);
  const sorted = [...names].sort();
  const rows = sorted
    .map((f) => {
      const [user, lang, vp, screen] = f.replace('.png', '').split('__');
      const cells = dirs
        .map((d) => {
          const has = existsSync(path.join(shotsRoot, d, f));
          const src = d === 'ux-pass' ? f : `../${d}/${f}`;
          return `<td class="${vp}">${has ? `<a href="${src}"><img loading="lazy" src="${src}" alt="${d} ${f}"></a>` : '<span class="none">not captured</span>'}</td>`;
        })
        .join('');
      return `<tr data-user="${user}" data-lang="${lang}" data-vp="${vp}"><th scope="row"><b>${screen.replace(/^\d-/, '')}</b><br>${user}<br>${lang.toUpperCase()} · ${vp}</th>${cells}</tr>`;
    })
    .join('\n');
  const filters = (key, values) =>
    `<label>${key} <select data-filter="${key}"><option value="">all</option>${values.map((v) => `<option>${v}</option>`).join('')}</select></label>`;
  const uniq = (i) => [...new Set(sorted.map((f) => f.replace('.png', '').split('__')[i]))];
  const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>UX pass screenshots</title>
<style>
  :root { color-scheme: light; font-family: 'Open Sans', system-ui, sans-serif; }
  body { margin: 0 16px 48px; background: #f6f6f9; color: #1c1c2b; }
  h1 { color: #3d3d5f; }
  header { position: sticky; top: 0; background: #f6f6f9; padding: 8px 0; border-bottom: 3px solid #f7bf0a; z-index: 1; }
  label { margin-inline-end: 12px; }
  table { border-collapse: collapse; width: 100%; }
  th, td { border-bottom: 1px solid #dcdce6; padding: 8px; vertical-align: top; text-align: start; }
  thead th { color: #3d3d5f; }
  tbody th { width: 160px; font-weight: 400; font-size: 13px; }
  img { max-width: 100%; border: 1px solid #dcdce6; background: #fff; }
  td.phone img { max-width: 260px; }
  .none { color: #55556a; font-style: italic; }
</style></head>
<body>
<header><h1>UX pass: student screens</h1>
<p>${dirs.map((d) => (d === 'ux-pass' ? '<b>Before</b> (ux-pass/)' : '<b>After</b> (ux-pass-after/)')).join(' · ')}. Click an image to open it full size. Findings: <a href="../../ux-findings.md">docs/ux-findings.md</a>.</p>
${filters('user', uniq(0))}${filters('lang', uniq(1))}${filters('vp', uniq(2))}
</header>
<table><thead><tr><th>Screen</th>${dirs.map((d) => `<th>${d === 'ux-pass' ? 'Before' : 'After'}</th>`).join('')}</tr></thead>
<tbody>
${rows}
</tbody></table>
<script>
  const sel = [...document.querySelectorAll('select[data-filter]')];
  sel.forEach((s) => s.addEventListener('change', () => {
    document.querySelectorAll('tbody tr').forEach((tr) => {
      tr.hidden = sel.some((x) => x.value && tr.dataset[x.dataset.filter] !== x.value);
    });
  }));
</script>
</body></html>
`;
  const sheet = path.join(shotsRoot, 'ux-pass', 'index.html');
  await mkdir(path.dirname(sheet), { recursive: true });
  await writeFile(sheet, html);
  console.log(`contact sheet: ${sheet} (${sorted.length} rows)`);
}

if (process.argv[3] !== '--sheet-only') await capture();
await contactSheet();
