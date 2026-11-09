// Captures every screen of a milestone in English and Arabic, at desktop and phone widths,
// runs an axe accessibility check on each, and writes a contact sheet.
//
//   node e2e/screenshots.mjs --base http://localhost:4200 --milestone M1
//
// Output: docs/screenshots/<milestone>/{*.png, index.html, a11y-report.json}
// Exit code 1 if any serious or critical accessibility violation is found.
import AxeBuilder from '@axe-core/playwright';
import { chromium } from '@playwright/test';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, a, i, all) => (a.startsWith('--') ? [...acc, [a.slice(2), all[i + 1]]] : acc), []),
);
const base = (args.base ?? 'http://localhost:4200').replace(/\/$/, '');
const milestone = args.milestone ?? 'M1';
const password = args.password ?? 'Liwa#2027';

const here = dirname(fileURLToPath(import.meta.url));
const outDir = resolve(here, '..', '..', 'docs', 'screenshots', milestone);
rmSync(outDir, { recursive: true, force: true });
mkdirSync(outDir, { recursive: true });

const viewports = {
  desktop: { width: 1366, height: 900 },
  phone: { width: 390, height: 844 },
};
const langs = ['en', 'ar'];

/** Each screen: how to get there, and who is signed in. */
const screens = [
  { id: 'login', title: 'Sign in', user: null, path: '/login' },
  {
    id: 'login-error',
    title: 'Sign in — wrong password',
    user: null,
    path: '/login',
    act: async (page) => {
      await openUsernameForm(page);
      await page.fill('input[formcontrolname="username"]', 'student.new');
      await page.fill('input[formcontrolname="password"]', 'not-the-password');
      await page.click('button[type="submit"]');
      await page.waitForSelector('[role="alert"]');
    },
  },
  {
    id: 'login-username',
    title: 'Sign in — username form opened (only differs from "Sign in" when Liwa sign-in is on)',
    user: null,
    path: '/login',
    act: openUsernameForm,
  },
  {
    id: 'login-sso-not-registered',
    title: 'Sign in — Liwa account not found in Smart Registration',
    user: null,
    path: '/login?sso=not_registered',
    waitFor: '[role="alert"]',
  },
  { id: 'student-home', title: 'Student home', user: 'student.new', path: '/student' },
  { id: 'student-home-probation', title: 'Student home — on academic probation', user: 'student.probation', path: '/student' },
  { id: 'advisor-home', title: 'Advisor home', user: 'advisor.it', path: '/advisor' },
  { id: 'admin-home', title: 'Admin home', user: 'admin', path: '/admin', waitFor: 'p.summary' },
  {
    id: 'admin-details',
    title: 'Admin home — "Details for IT" opened',
    user: 'admin',
    path: '/admin',
    waitFor: 'p.summary',
    act: async (page) => {
      await page.click('details.details > summary');
      await page.waitForSelector('dl.status-list', { state: 'visible' });
    },
  },
  { id: 'forbidden', title: 'Not allowed (student opens an admin page)', user: 'student.new', path: '/admin' },
  {
    id: 'user-menu',
    title: 'Account menu open',
    user: 'advisor.it',
    path: '/advisor',
    act: async (page) => {
      await page.click('.user-button');
      await page.waitForSelector('.mat-mdc-menu-panel');
    },
  },
];

/** With "Sign in with your Liwa account" on, the username form sits behind a link; open it first. */
async function openUsernameForm(page) {
  await page.waitForSelector('.local-toggle, input[formcontrolname="username"]');
  if (await page.$('.local-toggle')) await page.click('.local-toggle');
  await page.waitForSelector('input[formcontrolname="username"]');
}

async function signIn(page, username, lang) {
  await page.goto(`${base}/login?lang=${lang}`);
  await openUsernameForm(page);
  await page.fill('input[formcontrolname="username"]', username);
  await page.fill('input[formcontrolname="password"]', password);
  await Promise.all([page.waitForURL((u) => !u.pathname.startsWith('/login')), page.click('button[type="submit"]')]);
}

const browser = await chromium.launch();
const shots = [];
const a11y = [];

try {
  for (const screen of screens) {
    for (const lang of langs) {
      for (const [vpName, viewport] of Object.entries(viewports)) {
        const context = await browser.newContext({ viewport, locale: lang === 'ar' ? 'ar-AE' : 'en-AE', deviceScaleFactor: 1 });
        const page = await context.newPage();
        if (screen.user) await signIn(page, screen.user, lang);
        await page.goto(`${base}${screen.path}${screen.path.includes("?") ? "&" : "?"}lang=${lang}`);
        await page.waitForLoadState('networkidle');
        if (screen.waitFor) await page.waitForSelector(screen.waitFor);
        if (screen.act) await screen.act(page);
        await page.evaluate(() => document.fonts.ready);
        await page.waitForTimeout(250); // let Material animations settle

        const file = `${screen.id}.${lang}.${vpName}.png`;
        await page.screenshot({ path: join(outDir, file), fullPage: true });

        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        const dir = await page.evaluate(() => document.documentElement.dir);

        const result = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
        const violations = result.violations.map((v) => ({
          id: v.id,
          impact: v.impact,
          help: v.help,
          nodes: v.nodes.length,
          targets: v.nodes.slice(0, 3).map((n) => n.target.join(' ')),
        }));
        a11y.push({ screen: screen.id, lang, viewport: vpName, dir, horizontalOverflowPx: overflow, violations });
        shots.push({ ...screen, lang, vpName, file, overflow, violations: violations.length });
        console.log(`${file}  dir=${dir}  overflow=${overflow}px  axe=${violations.length}`);
        for (const v of violations) console.log(`    [${v.impact}] ${v.id}: ${v.help} → ${v.targets.join(' | ')}`);
        await context.close();
      }
    }
  }
} finally {
  await browser.close();
}

writeFileSync(join(outDir, 'a11y-report.json'), JSON.stringify({ base, milestone, generatedAt: new Date().toISOString(), results: a11y }, null, 2));

const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
const rows = screens
  .map((s) => {
    const cells = langs
      .flatMap((lang) => Object.keys(viewports).map((vp) => shots.find((x) => x.id === s.id && x.lang === lang && x.vpName === vp)))
      .map(
        (x) => `<figure class="${x.vpName}"><a href="${x.file}"><img src="${x.file}" alt="${esc(s.title)} — ${x.lang.toUpperCase()} ${x.vpName}" loading="lazy"></a>
          <figcaption>${x.lang === 'ar' ? 'العربية' : 'English'} · ${x.vpName}${x.overflow > 0 ? ' · <b class="bad">horizontal scroll</b>' : ''}${x.violations ? ` · <b class="bad">${x.violations} a11y issue(s)</b>` : ' · axe ✓'}</figcaption></figure>`,
      )
      .join('');
    return `<section><h2>${esc(s.title)}</h2><div class="row">${cells}</div></section>`;
  })
  .join('\n');

const total = a11y.reduce((n, r) => n + r.violations.length, 0);
const overflowCount = a11y.filter((r) => r.horizontalOverflowPx > 0).length;
writeFileSync(
  join(outDir, 'index.html'),
  `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${milestone} screenshots · Liwa Smart Registration</title>
<style>
  :root { --indigo:#2d2a6e; --border:#dcdbe4; }
  body { font-family: 'Open Sans', system-ui, sans-serif; margin: 0; padding: 24px 16px; background: #f5f5f8; color: #1d1b26; }
  header { max-width: 1400px; margin: 0 auto 24px; }
  h1 { color: var(--indigo); margin: 0 0 4px; }
  section { max-width: 1400px; margin: 0 auto 32px; background: #fff; border: 1px solid var(--border); border-radius: 8px; padding: 16px; }
  h2 { margin: 0 0 12px; font-size: 1.1rem; }
  .row { display: flex; gap: 12px; overflow-x: auto; align-items: flex-start; }
  figure { margin: 0; flex: none; }
  figure.desktop img { width: 420px; }
  figure.phone img { width: 160px; }
  img { border: 1px solid var(--border); border-radius: 4px; display: block; }
  figcaption { font-size: 12px; margin-top: 4px; color: #55536a; }
  .bad { color: #b3261e; }
</style></head><body>
<header><h1>${milestone} — screens</h1>
<p>${shots.length} screenshots · ${screens.length} screens × English/Arabic × desktop/phone · generated ${new Date().toISOString().slice(0, 16).replace('T', ' ')} UTC from ${esc(base)}</p>
<p>Accessibility (axe-core, WCAG 2.1 A/AA): <b class="${total ? 'bad' : ''}">${total} violation(s)</b> · horizontal scrolling: <b class="${overflowCount ? 'bad' : ''}">${overflowCount} screen(s)</b> · details in <a href="a11y-report.json">a11y-report.json</a></p></header>
${rows}
</body></html>`,
);

const serious = a11y.flatMap((r) => r.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical').map((v) => ({ ...v, at: `${r.screen}.${r.lang}.${r.viewport}` })));
console.log(`\n${shots.length} screenshots, ${total} axe violation(s) (${serious.length} serious/critical), ${overflowCount} with horizontal overflow → ${outDir}`);
for (const v of serious) console.log(`  [${v.impact}] ${v.id} @ ${v.at}: ${v.help} → ${v.targets.join(' | ')}`);
process.exit(serious.length || overflowCount ? 1 : 0);
