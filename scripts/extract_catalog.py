"""Extracts the "Course Descriptions" section of the official catalog into a JSON file the demo-data generator reads.

    python -m pip install --target .local/pylib pdfplumber      (once; a normal package restore)
    python -I scripts/extract_catalog.py

Input:  docs/reference/University_Catalog_2025_2026.pdf  (https://lu.ac.ae/downloadPdfs/University_Catalog_2025_2026.pdf)
Output: docs/reference/catalog-2025-2026-courses.json    {code: {title, prerequisites, description, pdf_page}}

The descriptions are laid out in two columns, so each page is cropped into a left and a right half and read top to
bottom. Entries are "Course Code: X / Course Title (or Name): / Prerequisites: / Course Description:". Only the
first entry per code is kept. Descriptions written in Arabic come out of the PDF with their letters scrambled, so
non-Latin descriptions are dropped (the courses still come from the curriculum tables in generate-demo-data.py).
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, '.local', 'pylib'))
import pdfplumber  # noqa: E402

PDF = os.path.join(ROOT, 'docs', 'reference', 'University_Catalog_2025_2026.pdf')
OUT = os.path.join(ROOT, 'docs', 'reference', 'catalog-2025-2026-courses.json')
FIRST, LAST = 307, 400  # PDF pages of the Course Descriptions section

CODE = re.compile(r'Course Code\s*:?\s*([A-Z]{2,4})\s?(\d{3})(?![0-9])')


def latin(text):
    letters = [ch for ch in text if ch.isalpha()]
    return bool(letters) and sum(ch.isascii() for ch in letters) / len(letters) > 0.9


def clean(text):
    text = re.sub(r'Page \d+ of \d+', '', text)
    return re.sub(r'\s+', ' ', text).strip(' .')


def main():
    entries, current = {}, None
    with pdfplumber.open(PDF) as doc:
        for n in range(FIRST, min(LAST, len(doc.pages)) + 1):
            page = doc.pages[n - 1]
            for box in ((0, 0, page.width / 2, page.height), (page.width / 2, 0, page.width, page.height)):
                for line in (page.crop(box).extract_text() or '').splitlines():
                    m = CODE.search(line)
                    if m:
                        code = m.group(1) + m.group(2)
                        current = None if code in entries else {'title': '', 'prerequisites': '', 'description': [], 'pdf_page': n, 'state': ''}
                        if current is not None:
                            entries[code] = current
                        continue
                    if current is None:
                        continue
                    s = line.strip()
                    if re.match(r'^Course (Title|Name)', s):
                        current['title'], current['state'] = re.sub(r'^Course (Title|Name)\s*:?\s*', '', s), 'title'
                    elif re.match(r'^Pre-?requisite', s):
                        current['prerequisites'], current['state'] = re.sub(r'^Pre-?requisites?\s*:?\s*', '', s), 'prereq'
                    elif s.startswith('Course Description'):
                        current['state'] = 'desc'
                    elif s and current['state'] == 'title':
                        current['title'] += ' ' + s
                    elif s and current['state'] == 'prereq':
                        current['prerequisites'] += ' ' + s
                    elif s and current['state'] == 'desc':
                        current['description'].append(s)
    out = {}
    for code, e in sorted(entries.items()):
        desc = clean(' '.join(e['description']))
        out[code] = {
            'title': clean(e['title']) if latin(e['title']) else '',
            'prerequisites': clean(e['prerequisites']),
            'description': desc if latin(desc) else '',
            'pdf_page': e['pdf_page'],
        }
    with open(OUT, 'w', encoding='utf-8') as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
        f.write('\n')
    print(f'{len(out)} courses -> {OUT}')


if __name__ == '__main__':
    main()
