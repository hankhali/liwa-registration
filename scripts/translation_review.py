"""Writes translations/REVIEW.md: every machine-drafted Arabic string, next to the English, for native review.

Sources: course and program names (src/Liwa.Infrastructure/Seeding/Demo/*.csv) and every UI string
(web/public/i18n/ar.json against en.json). Re-run after changing any of them:

    python scripts/translation_review.py
"""
import csv
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEMO = os.path.join(ROOT, 'src', 'Liwa.Infrastructure', 'Seeding', 'Demo')
I18N = os.path.join(ROOT, 'web', 'public', 'i18n')
OUT = os.path.join(ROOT, 'translations', 'REVIEW.md')


def rows(name):
    with open(os.path.join(DEMO, name), encoding='utf-8') as f:
        return list(csv.DictReader(f))


def flat(d, prefix=''):
    for k, v in d.items():
        if isinstance(v, dict):
            yield from flat(v, f'{prefix}{k}.')
        else:
            yield f'{prefix}{k}', v


def cell(s):
    return (s or '').replace('|', '\\|').replace('\n', ' ')


with open(os.path.join(I18N, 'en.json'), encoding='utf-8') as f:
    en = dict(flat(json.load(f)))
with open(os.path.join(I18N, 'ar.json'), encoding='utf-8') as f:
    ar = dict(flat(json.load(f)))

programs = rows('programs.csv')
courses = rows('courses.csv')

lines = [
    '# Arabic translations for native review',
    '',
    'Every Arabic string in the app was drafted by machine and needs review by a native speaker before students',
    'see it. Correct the Arabic in place (the source file is named in each section), tick the box, and re-run',
    '`python scripts/translation_review.py` to refresh this list. Course codes stay in Latin letters; '
    'placeholders such as `{{ courses }}` must be kept exactly.',
    '',
    f'- Programs: {len(programs)} · Courses: {len(courses)} · UI strings: {len(ar)}',
    '- Official university, college and program names should be checked against https://lu.ac.ae/ar/.',
    '',
    '## Programs (`src/Liwa.Infrastructure/Seeding/Demo/programs.csv`, `name_ar`)',
    '',
    '| OK | Code | English | Arabic (draft) |',
    '|---|---|---|---|',
]
lines += [f"| [ ] | {p['program_code']} | {cell(p['name'])} | {cell(p.get('name_ar'))} |" for p in programs]
lines += ['', '## Course titles (`courses.csv`, `title_ar`)', '', '| OK | Code | English | Arabic (draft) |', '|---|---|---|---|']
lines += [f"| [ ] | {c['course_code']} | {cell(c['title'])} | {cell(c.get('title_ar'))} |" for c in courses]
lines += ['', '## App text (`web/public/i18n/ar.json`)', '', '| OK | Key | English | Arabic (draft) |', '|---|---|---|---|']
lines += [f"| [ ] | `{k}` | {cell(en.get(k, ''))} | {cell(v)} |" for k, v in sorted(ar.items())]
missing = sorted(set(en) - set(ar))
if missing:
    lines += ['', '## Missing in Arabic', ''] + [f'- `{k}`' for k in missing]

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(lines) + '\n')
print(f'wrote {OUT}: {len(programs)} programs, {len(courses)} courses, {len(ar)} UI strings, {len(missing)} missing')
