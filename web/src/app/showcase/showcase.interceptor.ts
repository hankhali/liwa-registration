import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { Observable, delay, of, throwError } from 'rxjs';
import { SHOWCASE } from './showcase';

/** One recorded answer from the real API. */
interface Recorded {
  status: number;
  body: unknown;
}

/** What scripts/record-showcase.mjs saves from a run against the real server. */
export interface ShowcaseData {
  recordedAt: string;
  /** "user|METHOD|/api/path?query|body" → answer ("-" = not signed in, body "" = none). */
  answers: Record<string, Recorded>;
  /** Sign-in answer per practice account (token replaced at run time). */
  logins: Record<string, { user: unknown }>;
}

let data: ShowcaseData | null = null;

/** Loads the recorded answers before the app starts (showcase builds only). */
export async function loadShowcaseData(): Promise<void> {
  if (!SHOWCASE) return;
  const res = await fetch('showcase-data.json');
  data = (await res.json()) as ShowcaseData;
}

export const SHOWCASE_TOKEN_PREFIX = 'showcase.';

/** Which practice account a request is for, from the showcase token the auth interceptor attached. */
function userOf(req: HttpRequest<unknown>): string {
  const auth = req.headers.get('Authorization') ?? '';
  const i = auth.indexOf(SHOWCASE_TOKEN_PREFIX);
  return i >= 0 ? auth.slice(i + SHOWCASE_TOKEN_PREFIX.length) : '-';
}

function bodyText(req: HttpRequest<unknown>): string {
  if (req.body === null || req.body === undefined) return '';
  return typeof req.body === 'string' ? req.body : JSON.stringify(req.body);
}

/** Finds the closest recorded answer: exact, then ignoring the body, then ignoring the query string. */
export function lookup(answers: Record<string, Recorded>, user: string, method: string, url: string, body: string): Recorded | null {
  const exact = answers[`${user}|${method}|${url}|${body}`];
  if (exact) return exact;
  const prefixes = [`${user}|${method}|${url}|`, `${user}|${method}|${url.split('?')[0]}`];
  for (const p of prefixes) {
    const key = Object.keys(answers).find((k) => k.startsWith(p) && (p.endsWith('|') || k.charAt(p.length) === '|' || k.charAt(p.length) === '?'));
    if (key) return answers[key];
  }
  return null;
}

function reply(req: HttpRequest<unknown>, r: Recorded): Observable<HttpEvent<unknown>> {
  if (r.status >= 400) {
    return throwError(() => new HttpErrorResponse({ status: r.status, error: r.body, url: req.url })).pipe(delay(80));
  }
  return of(new HttpResponse({ status: r.status, body: r.body, url: req.url })).pipe(delay(120));
}

/**
 * Showcase builds only: answers every /api request from the recorded data, so the app works with no server.
 * Signing in as a practice account always works; anything that would change data answers "read-only demo".
 */
export const showcaseInterceptor: HttpInterceptorFn = (req, next) => {
  if (!SHOWCASE || !data || !req.url.startsWith('/api/')) return next(req);
  const url = req.urlWithParams;

  if (req.method === 'POST' && url === '/api/auth/login') {
    const username = String((req.body as { username?: string } | null)?.username ?? '').trim().toLowerCase();
    const login = data.logins[username];
    if (!login) return reply(req, { status: 401, body: { title: 'Unknown practice account' } });
    return reply(req, {
      status: 200,
      body: { ...login, accessToken: SHOWCASE_TOKEN_PREFIX + username, expiresAt: new Date(Date.now() + 12 * 3_600_000).toISOString() },
    });
  }

  const found = lookup(data.answers, userOf(req), req.method, url, bodyText(req));
  if (found) return reply(req, found);
  if (req.method === 'GET') return reply(req, { status: 404, body: null });
  // Saving, sending, approving: the online demo shows the screens but doesn't change anything.
  return reply(req, { status: 403, body: { title: 'This online demo is read-only.' } });
};
