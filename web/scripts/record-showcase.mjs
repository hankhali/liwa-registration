// Records the real API's answers for the online showcase (the read-only build that runs with no server).
//
// Needs the API (Development, demo data) and `ng serve` running, and the showcase build:
//   npx ng build --configuration showcase
//   node scripts/record-showcase.mjs            # → dist/showcase/browser/showcase-data.json
//
// It walks the guided tour and opens every page of the tour's practice accounts. It only reads: nothing is
// saved, sent or approved. Uses the installed Chrome (channel: 'chrome'); nothing is downloaded.
import { chromium } from 'playwright';
import { readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const BASE = process.env.LIWA_BASE ?? 'http://localhost:4200';
const here = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(here, '..', 'dist', 'showcase', 'browser');

const answers = {};
const logins = {};
const tokens = new Map();

const browser = await chromium.launch({ channel: 'chrome' });
const page = await browser.newPage({ viewport: { width: 1366, height: 820 } });

page.on('response', async (res) => {
  const req = res.request();
  const url = new URL(req.url());
  if (!url.pathname.startsWith('/api/')) return;
  const target = url.pathname + url.search;
  let body = null;
  try {
    body = (res.headers()['content-type'] ?? '').includes('json') ? await res.json() : null;
  } catch {
    body = null;
  }
  if (req.method() === 'POST' && url.pathname === '/api/auth/login') {
    if (res.status() === 200 && body?.user) {
      logins[body.user.username] = { user: body.user };
      tokens.set(body.accessToken, body.user.username);
    }
    return;
  }
  const auth = (await req.allHeaders())['authorization'] ?? '';
  const user = tokens.get(auth.replace(/^Bearer\s+/i, '')) ?? '-';
  answers[`${user}|${req.method()}|${target}|${req.postData() ?? ''}`] = { status: res.status(), body };
});

const settle = async (ms = 1800) => {
  await page.waitForLoadState('networkidle').catch(() => undefined);
  await page.waitForTimeout(ms);
};

// 1. The whole walkthrough, step by step.
await page.goto(`${BASE}/login`);
await page.getByRole('button', { name: 'Start the guided tour' }).click();
await page.locator('app-walkthrough-overlay .card').waitFor();
for (let i = 0; i < 40; i++) {
  await page.waitForFunction(() => !document.querySelector('app-walkthrough-overlay .opening'), null, { timeout: 20000 });
  await settle(900);
  const next = page.locator('app-walkthrough-overlay .card button.next');
  if ((await next.count()) === 0) break;
  await next.click();
}
await page.keyboard.press('Escape');

// 2. Every page of each account, so the visitor can look around after the tour.
async function signInAs(username, password) {
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  await page.goto(`${BASE}/login`);
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await settle();
  if (username === 'admin') await page.keyboard.press('Escape');
}

async function visit(paths) {
  for (const p of paths) {
    await page.goto(`${BASE}${p}`);
    await settle();
  }
}

/** Opens each link in a list page (plans in the queue, students in the list) and comes back. */
async function openEach(listPath, selector, max = 8) {
  await page.goto(`${BASE}${listPath}`);
  await settle();
  const hrefs = await page.locator(selector).evaluateAll((as) => as.map((a) => a.getAttribute('href')).filter(Boolean));
  for (const h of [...new Set(hrefs)].slice(0, max)) {
    await page.goto(`${BASE}${h}`);
    await settle();
  }
}

const demo = await (await fetch(`${BASE}/api/auth/demo-accounts`)).json();

for (const s of ['student.hanieh', 'student.transfer']) {
  await signInAs(s, demo.password);
  await visit(['/student', '/student/availability', '/student/planner', '/student/graduation', '/student/plan', '/student/messages']);
}

await signInAs('advisor.it', demo.password);
await visit(['/advisor', '/advisor/messages', '/advisor/versions']);
// Every filter tab of the queue, then every plan in it.
await page.goto(`${BASE}/advisor/plans`);
await settle();
for (const tab of await page.getByRole('tab').all()) {
  await tab.click().catch(() => undefined);
  await settle(900);
}
await openEach('/advisor/plans', 'table.queue tbody a');
await openEach('/advisor/students', 'table a[href^="/advisor/students/"]');

await signInAs('admin', demo.adminPassword);
await visit(['/admin', '/admin/catalog', '/admin/imports', '/admin/users', '/admin/settings', '/admin/ai-usage', '/admin/messages', '/tour']);

await browser.close();

// The preview signs in without passwords: don't ship the practice ones.
const noPassword = '(not needed in the preview)';
for (const a of Object.values(answers)) {
  if (a.body && typeof a.body === 'object' && 'adminPassword' in a.body) Object.assign(a.body, { password: noPassword, adminPassword: noPassword });
}
writeFileSync(path.join(outDir, 'showcase-data.json'), JSON.stringify({ recordedAt: new Date().toISOString(), answers, logins }));
// The showcase never shows the university's logo; don't ship it.
rmSync(path.join(outDir, 'assets', 'brand'), { recursive: true, force: true });

// The host wraps the page in its own <html>/<head>/<body>: keep only what goes inside.
const indexFile = path.join(outDir, 'index.html');
const html = readFileSync(indexFile, 'utf8');
if (html.includes('<!doctype')) {
  const css = html.match(/href="(styles-[^"]+\.css)"/)[1];
  const main = html.match(/src="(main-[^"]+\.js)"/)[1];
  const preload = (html.match(/<link rel="modulepreload" href="[^"]+">/g) ?? []).join('');
  writeFileSync(
    indexFile,
    [
      '<title>Smart Registration Prototype</title>',
      '<meta name="theme-color" content="#3d3d5f">',
      '<link rel="icon" href="data:,">',
      '<base href="./">',
      `<link rel="stylesheet" href="${css}">`,
      '<app-root></app-root>',
      '<noscript>This preview needs JavaScript.</noscript>',
      `${preload}<script src="${main}" type="module"></script>`,
      '',
    ].join('\n'),
  );
}
console.log(`Recorded ${Object.keys(answers).length} answers for ${Object.keys(logins).length} accounts → ${path.join(outDir, 'showcase-data.json')}`);
