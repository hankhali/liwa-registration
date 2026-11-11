"""Puts demo plans into every status, for manual review of the screens (brief §13).

    python scripts/demo_plan_states.py [--api http://localhost:5080]

After it runs, sign in as the advisors and students below to see each state:

    Draft             student.first       (saved, not sent)
    Submitted         student.mid2        (waiting for advisor.it)
    AdvisorReviewing  student.new         (advisor.it opened it)
    Approved          student.senior      (approved by advisor.it)
    Rejected          student.mid1        (sent back by advisor.business with a comment)
    ChangesProposed   student.transfer    (advisor.business suggested dropping a class)
    NeedsReview       student.honors      (approved, then a failing grade was posted)

Run it on a fresh demo database (scripts/reset-db.ps1, then start the API). It only uses the public API, so every
step is audited and notified exactly as in real use. Standard library only.
"""
import argparse
import json
import sys
import urllib.error
import urllib.request

PASSWORD = 'Liwa#2027'


def call(api, path, token=None, body=None, method=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(api.rstrip('/') + path, data=data, method=method or ('POST' if body is not None else 'GET'))
    req.add_header('Content-Type', 'application/json')
    if token:
        req.add_header('Authorization', f'Bearer {token}')
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read()
            return r.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw) if raw else None
        except ValueError:
            return e.code, None


def token(api, username):
    status, body = call(api, '/api/auth/login', body={'username': username, 'password': PASSWORD})
    if status != 200:
        sys.exit(f'{username} could not sign in (HTTP {status}). Is this a demo database?')
    return body['accessToken']


def key(w):
    return f"{w['code']}:{','.join(w['courses'])}:{w['value'] or ''}"


def send_recommended(api, username, submit=True):
    t = token(api, username)
    _, view = call(api, '/api/students/me/planning', t, body={})
    ids = [p['sectionId'] for p in view['recommendation']['plan']]
    _, check = call(api, '/api/students/me/plan/check', t, body={'sectionIds': ids})
    status, res = call(api, '/api/students/me/plan', t, body={'sectionIds': ids, 'submit': submit, 'acknowledgedWarnings': [key(w) for w in check['warnings']]})
    if status != 200:
        sys.exit(f'{username}: saving the plan failed (HTTP {status}): {res}')
    return t, ids, res['plan']['id']


def plan_id(api, advisor_token, student_id):
    _, queue = call(api, '/api/advisor/plans', advisor_token)
    return next(q['planId'] for q in queue if q['studentId'] == student_id)


def decide(api, advisor_token, pid, decision, note=None, section_ids=None):
    call(api, f'/api/advisor/plans/{pid}', advisor_token)  # open it (marks it "being reviewed")
    status, res = call(api, f'/api/advisor/plans/{pid}/decision', advisor_token, body={'decision': decision, 'note': note, 'sectionIds': section_ids})
    if status != 200:
        sys.exit(f'{decision} failed for plan {pid} (HTTP {status}): {res}')


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--api', default='http://localhost:5080')
    api = p.parse_args().api

    it = token(api, 'advisor.it')
    business = token(api, 'advisor.business')
    admin = token(api, 'admin')

    send_recommended(api, 'student.first', submit=False)
    print('Draft             student.first')

    send_recommended(api, 'student.mid2')
    print('Submitted         student.mid2')

    send_recommended(api, 'student.new')
    call(api, f"/api/advisor/plans/{plan_id(api, it, '202700101')}", it)
    print('AdvisorReviewing  student.new')

    send_recommended(api, 'student.senior')
    decide(api, it, plan_id(api, it, '202300104'), 'Approve', 'Well done: this finishes your degree.')
    print('Approved          student.senior')

    send_recommended(api, 'student.mid1')
    decide(api, business, plan_id(api, business, '202500108'), 'Reject', 'Please swap BUS301 for an evening class so you can keep working.')
    print('Rejected          student.mid1')

    _, ids, _ = send_recommended(api, 'student.transfer')
    pid = plan_id(api, business, '202500112')
    call(api, f'/api/advisor/plans/{pid}', business)
    proposal = None
    for drop in ids:
        candidate = [x for x in ids if x != drop]
        _, c = call(api, f'/api/advisor/plans/{pid}/check', business, body={'sectionIds': candidate})
        if not c['errors']:
            proposal = candidate
            break
    decide(api, business, pid, 'ProposeChanges', 'One class fewer this semester gives you time for the internship search.', proposal)
    print('ChangesProposed   student.transfer')

    t, ids, _ = send_recommended(api, 'student.honors')
    decide(api, it, plan_id(api, it, '202500103'), 'Approve')
    _, check = call(api, '/api/students/me/plan/check', t, body={'sectionIds': ids})
    waiting = next((w['courses'][1] for w in check['warnings'] if w['code'] == 'WaitingForGrade' and len(w['courses']) > 1), None)
    _, record = call(api, '/api/advisor/students', it)
    if waiting:
        status, res = call(api, '/api/admin/grades', admin, body={'studentId': '202500103', 'courseCode': waiting, 'termCode': '2026FA', 'grade': 'F'})
        print(f"NeedsReview       student.honors   (F posted in {waiting}: {len(res['movedToNeedsReview']) if status == 200 else 0} plan moved)")
    else:
        print('NeedsReview       (skipped: no plan depended on a grade in progress)')
    return 0


if __name__ == '__main__':
    sys.exit(main())
