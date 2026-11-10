"""Checks every text/background pair of the theme tokens against WCAG 2.2 AA, in the light and the dark theme.

Reads the colour tokens from web/src/styles.scss (written as light-dark(<light>, <dark>) or a single colour) and
fails (exit code 1) if any pair is below its minimum: 4.5:1 for text, 3:1 for large text and UI parts such as
focus rings and borders that carry meaning (brief §13: contrast check over the theme tokens, all pairs AA).

    python scripts/check_contrast.py
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STYLES = os.path.join(ROOT, 'web', 'src', 'styles.scss')

# (foreground token, background token, minimum ratio, what it is)
PAIRS = [
    ('text', 'page', 4.5, 'body text on the page'),
    ('text', 'surface', 4.5, 'body text on cards'),
    ('text-muted', 'surface', 4.5, 'secondary text on cards'),
    ('text-muted', 'page', 4.5, 'secondary text on the page'),
    ('primary', 'surface', 4.5, 'links and outlined buttons'),
    ('primary', 'page', 4.5, 'links on the page'),
    ('accent', 'surface', 3.0, 'focus ring and accents on cards'),
    ('accent', 'page', 3.0, 'focus ring on the page'),
    ('danger', 'surface', 4.5, 'error text'),
    ('danger', 'clash', 4.5, 'clash text in the timetable'),
    ('success', 'surface', 4.5, 'success text'),
    ('success', 'free', 3.0, 'tick on a free hour'),
    ('warning', 'surface', 4.5, 'warning text'),
    ('text', 'free', 4.5, 'text on free-time cells and notices'),
    ('text', 'clash', 4.5, 'text on clash blocks'),
    ('block-fg', 'block', 4.5, 'class blocks in the timetable'),
    ('gold-fg', 'gold-bg', 4.5, 'critical-path badge and cards'),
    ('chip-draft-fg', 'chip-draft-bg', 4.5, 'status chip: Draft'),
    ('chip-submitted-fg', 'chip-submitted-bg', 4.5, 'status chip: Waiting for review'),
    ('chip-reviewing-fg', 'chip-reviewing-bg', 4.5, 'status chip: Being reviewed'),
    ('chip-approved-fg', 'chip-approved-bg', 4.5, 'status chip: Approved'),
    ('chip-rejected-fg', 'chip-rejected-bg', 4.5, 'status chip: Sent back'),
    ('chip-proposed-fg', 'chip-proposed-bg', 4.5, 'status chip: Changes suggested'),
    ('chip-needs-fg', 'chip-needs-bg', 4.5, 'status chip: Needs another look'),
    ('#ffffff', 'header', 4.5, 'top bar text'),
    ('gold', 'header', 3.0, 'gold rule and focus ring on the indigo top bar'),
    ('on-gold', 'gold', 4.5, 'primary (gold) button label'),
    ('primary', 'block', 4.5, 'active menu item'),
    ('primary', 'gold-bg', 4.5, 'indigo text on the pale gold critical-path background'),
]

# Values set outside the token block.
EXTRA = {}


def tokens():
    text = open(STYLES, encoding='utf-8').read()
    out = {}
    for name, value in re.findall(r'--liwa-([a-z0-9-]+):\s*([^;]+);', text):
        m = re.match(r'light-dark\(\s*(#[0-9a-fA-F]{3,6})\s*,\s*(#[0-9a-fA-F]{3,6})\s*\)', value.strip())
        if m:
            out[name] = (m.group(1), m.group(2))
        elif re.match(r'#[0-9a-fA-F]{3,6}$', value.strip()):
            out[name] = (value.strip(), value.strip())
    out.update(EXTRA)
    return out


def rgb(h):
    h = h.lstrip('#')
    if len(h) == 3:
        h = ''.join(c * 2 for c in h)
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def luminance(h):
    def channel(c):
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (channel(c) for c in rgb(h))
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def ratio(a, b):
    la, lb = sorted((luminance(a), luminance(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def main():
    t = tokens()
    failures = 0
    for theme, idx in (('light', 0), ('dark', 1)):
        print(f'--- {theme} theme')
        for fg, bg, minimum, what in PAIRS:
            f = fg if fg.startswith('#') else t.get(fg, (None, None))[idx]
            b = bg if bg.startswith('#') else t.get(bg, (None, None))[idx]
            if f is None or b is None:
                print(f'  MISSING  {fg} on {bg} ({what})')
                failures += 1
                continue
            r = ratio(f, b)
            ok = r >= minimum
            failures += 0 if ok else 1
            print(f"  {'ok  ' if ok else 'FAIL'}  {r:5.2f}:1 (min {minimum})  {fg} {f} on {bg} {b}  - {what}")
    print('all pairs pass WCAG AA' if failures == 0 else f'{failures} pair(s) fail')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
