"""Checks an installation end to end (brief §13). Drops nothing: it only reads, and signs in.

Run it after a clean install (database dropped and recreated, then the API started once so migrations and the demo
seed run), or after an upgrade:

    python scripts/verify_install.py --api http://localhost:5080 [--web http://localhost:4200] [--web-dir dist/web]

Checks:
  1. the API is up and its database is reachable;
  2. every migration in the code is applied, in order (needs psql; settings from .env);
  3. the seed loaded: 3 programs, 16 students, the Spring 2027 timetable;
  4. every demo account signs in;
  5. the planner runs for all 16 demo students (a plan, credit limits and a graduation projection);
  6. the advisor and admin pages' data loads;
  7. optionally, the web app is served (--web) or the production build exists (--web-dir).
Uses only the Python standard library. Exit code 0 = everything passed.
"""
import argparse
import glob
import json
import os
import shutil
import subprocess
import sys
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PASSWORD_DEFAULT = 'Liwa#2027'
STUDENTS = ['student.new', 'student.evening', 'student.repeat', 'student.transfer', 'student.probation', 'student.senior', 'student.hold',
            'student.honors', 'student.parttime', 'student.intern', 'student.switch', 'student.mid1', 'student.mid2', 'student.first', 'student.media', 'student.hanieh']
STAFF = ['advisor.it', 'advisor.business', 'advisor.media', 'admin']

results = []


def check(name, ok, detail=''):
    results.append(ok)
    print(f"  {'PASS' if ok else 'FAIL'}  {name}{(' — ' + detail) if detail else ''}")
    return ok


def call(api, path, token=None, body=None, method=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(api.rstrip('/') + path, data=data, method=method or ('POST' if body is not None else 'GET'))
    req.add_header('Content-Type', 'application/json')
    if token:
        req.add_header('Authorization', f'Bearer {token}')
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read()
            return r.status, (json.loads(raw) if raw and r.headers.get_content_type() == 'application/json' else raw)
    except urllib.error.HTTPError as e:
        return e.code, None
    except (urllib.error.URLError, TimeoutError) as e:
        return 0, str(e)


def sign_in(api, username, password):
    status, body = call(api, '/api/auth/login', body={'username': username, 'password': password})
    return body['accessToken'] if status == 200 and isinstance(body, dict) else None


def dotenv():
    values = {}
    path = os.path.join(ROOT, '.env')
    if os.path.exists(path):
        for line in open(path, encoding='utf-8'):
            if '=' in line and not line.lstrip().startswith('#'):
                k, v = line.split('=', 1)
                values[k.strip()] = v.strip()
    return values


def migrations_in_code():
    files = glob.glob(os.path.join(ROOT, 'src', 'Liwa.Infrastructure', 'Persistence', 'Migrations', '*_*.cs'))
    return sorted(os.path.basename(f)[:-3] for f in files if not f.endswith('.Designer.cs') and 'ModelSnapshot' not in f)


def migrations_applied(env):
    psql = shutil.which('psql') or next(iter(sorted(glob.glob(r'C:\Program Files\PostgreSQL\*\bin\psql.exe'), reverse=True)), None)
    if not psql:
        return None
    cmd = [psql, '-h', env.get('POSTGRES_HOST', 'localhost'), '-p', env.get('POSTGRES_PORT', '5432'), '-U', env.get('POSTGRES_USER', 'liwa'),
           '-d', env.get('POSTGRES_DB', 'liwa'), '-w', '-tAc', 'select migration_id from "__EFMigrationsHistory" order by migration_id']
    out = subprocess.run(cmd, capture_output=True, text=True, env={**os.environ, 'PGPASSWORD': env.get('POSTGRES_PASSWORD', 'liwa')})
    return [line.strip() for line in out.stdout.splitlines() if line.strip()] if out.returncode == 0 else None


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--api', default='http://localhost:5080')
    p.add_argument('--web', help='URL where the web app is served (checks index.html and the /api proxy)')
    p.add_argument('--web-dir', help='folder of the production web build to check (e.g. dist/web)')
    p.add_argument('--password', default=os.environ.get('LIWA_DEMO_PASSWORD', PASSWORD_DEFAULT))
    args = p.parse_args()
    api = args.api

    print('1. API and database')
    status, body = call(api, '/api/health/ready')
    if not check('API is up and the database is reachable', status == 200, f'HTTP {status}'):
        print('Stopping: start the API first (scripts/run-dev.ps1, or the service on a server).')
        return 1

    print('2. Migrations')
    code = migrations_in_code()
    applied = migrations_applied(dotenv())
    if applied is None:
        check('migrations applied in order', True, 'skipped: psql not found or no database access (set POSTGRES_* in .env)')
    else:
        check(f'all {len(code)} migrations applied, in order', applied == code,
              '' if applied == code else f'missing {sorted(set(code) - set(applied))}, unexpected {sorted(set(applied) - set(code))}')

    print('3. Demo seed')
    admin = sign_in(api, 'admin', args.password)
    if check('admin signs in', admin is not None):
        status, o = call(api, '/api/admin/overview', admin)
        ok = status == 200 and isinstance(o, dict)
        check('3 programs loaded', ok and o.get('programs') == 3, str(o.get('programs') if ok else status))
        check('16 students loaded', ok and o.get('students', 0) >= 16, str(o.get('students') if ok else status))
        check('current term has a timetable', ok and (o.get('sectionsThisTerm') or 0) > 100, str(o.get('sectionsThisTerm') if ok else status))

    print('4. Every demo account signs in')
    tokens = {}
    for u in STUDENTS + STAFF:
        tokens[u] = sign_in(api, u, args.password)
        check(f'{u} signs in', tokens[u] is not None)

    print('5. The planner runs for all 16 demo students')
    for u in STUDENTS:
        t = tokens.get(u)
        if not t:
            check(f'planner for {u}', False, 'no sign-in')
            continue
        status, v = call(api, '/api/students/me/planning', t, body={})
        r = v.get('recommendation') if status == 200 and isinstance(v, dict) else None
        ok = bool(r) and r.get('limits') is not None and r['projection']['withSummer']['graduationTerm'] is not None
        check(f'planner for {u}', ok, f"{len(r['plan'])} classes, {r['totalCredits']} credits, graduates {r['projection']['withoutSummer']['graduationTerm']}" if ok else f'HTTP {status}')

    print('6. Advisor and admin pages')
    adv = tokens.get('advisor.it')
    for path in ['/api/advisor/overview', '/api/advisor/plans', '/api/advisor/students', '/api/catalog/versions', '/api/notifications']:
        status, _ = call(api, path, adv)
        check(f'advisor {path}', status == 200, f'HTTP {status}')
    for path in ['/api/admin/system', '/api/admin/catalog/courses', '/api/admin/users', '/api/admin/credit-policy', '/api/admin/settings/ai', '/api/admin/ai-usage', '/api/admin/exports/programs']:
        status, _ = call(api, path, admin)
        check(f'admin {path}', status == 200, f'HTTP {status}')
    status, _ = call(api, '/api/admin/users', tokens.get('student.new'))
    check('students are refused admin pages', status == 403, f'HTTP {status}')

    if args.web:
        print('7. Web app')
        status, page = call(args.web, '/')
        check('web app is served', status == 200 and b'<app-root' in (page or b''), f'HTTP {status}')
        status, _ = call(args.web, '/api/health')
        check('web server proxies /api to the API (same origin)', status == 200, f'HTTP {status}')
    if args.web_dir:
        index = os.path.join(args.web_dir if os.path.isabs(args.web_dir) else os.path.join(ROOT, args.web_dir), 'browser', 'index.html')
        if not os.path.exists(index):
            index = os.path.join(os.path.dirname(index), '..', 'index.html')
        check('production web build exists', os.path.exists(index), os.path.normpath(index))

    failed = results.count(False)
    print(f'\n{len(results) - failed} passed, {failed} failed')
    return 0 if failed == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
