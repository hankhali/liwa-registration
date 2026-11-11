// Screenshots of the advisor review page (three columns) and "Suggest changes" (the student's planner), for the
// UX pass. Uses the installed Chrome (channel 'chrome'). Opens student.new's plan, which demo_plan_states.py
// leaves "being reviewed", so opening it changes nothing; nothing is decided or sent.
//   node scripts/advisor-review-shot.mjs <out-dir-name>
import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const out = path.resolve(here, '../../docs/screenshots', process.argv[2] ?? 'ux-pass-after');
const BASE = process.env.LIWA_WEB ?? 'http://localhost:4200';
const API = process.env.LIWA_API ?? 'http://localhost:5080';

const login = await fetch(`${API}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username: 'advisor.it', password: 'Liwa#2027' }) });
const { accessToken } = await login.json();
const queue = await (await fetch(`${API}/api/advisor/plans`, { headers: { Authorization: `Bearer ${accessToken}` } })).json();
const item = queue.find((q) => q.studentId === '202700101');
if (!item) throw new Error('student.new has no plan in the queue; run scripts/demo_plan_states.py first');

await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  for (const lang of ['en', 'ar']) {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' });
    await page.goto(`${BASE}/login?lang=${lang}`);
    await page.locator('input[formcontrolname="username"]').fill('advisor.it');
    await page.locator('input[formcontrolname="password"]').fill('Liwa#2027');
    await page.locator('button[type="submit"]').click();
    await page.waitForURL((u) => !u.pathname.startsWith('/login'));
    await page.goto(`${BASE}/advisor/plans/${item.planId}?lang=${lang}`);
    await page.waitForLoadState('networkidle').catch(() => {});
    await page.waitForTimeout(800);
    await page.screenshot({ path: path.join(out, `advisor.it__${lang}__desktop__review.png`), fullPage: true });
    await page.getByRole('button', { name: lang === 'en' ? /Suggest changes|Propose changes/ : /اقترح|اقتراح/ }).first().click();
    await page.waitForLoadState('networkidle').catch(() => {});
    await page.waitForTimeout(800);
    await page.screenshot({ path: path.join(out, `advisor.it__${lang}__desktop__suggest-changes.png`), fullPage: true });
    await page.close();
  }
} finally {
  await browser.close();
}
console.log('saved to', out);
