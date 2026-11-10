# Brand assets

Colours, fonts and logo files used by the app, where they come from, and how to replace them.

## Colours

The app uses **charcoal `#272727`** and **sage `#b5b59e`**, taken from the official logo, with a deep olive
`#6b6a52` for accents and focus rings on light backgrounds. This replaces the brief's indigo `#3d3d5f` / gold `#f7bf0a`
at the project owner's request (2026-11-09, [docs/assumptions.md](../../../../docs/assumptions.md) D1) so the app
matches Liwa's current identity. The "gold" of the brief survives as the **critical-path** colour (amber badge).
All colours are tokens in `web/src/styles.scss` (light and dark); `python scripts/check_contrast.py` checks every
text/background pair against WCAG AA.

## Fonts

Frutiger (the university's font) is licensed, so the app uses **Open Sans** and **Noto Sans Arabic**, self-hosted from
the npm packages `@fontsource/open-sans` and `@fontsource/noto-sans-arabic` (no font CDN). Material Icons come from the
`material-icons` package.

## Logo files

Taken from the sign-in page of Liwa's Blackboard Learn (`https://lms.lu.ac.ae/ultra/`), because the main website
blocks automated downloads. Converted to transparent PNG; the shield is otherwise unmodified. **Replace them with vector
files from university marketing** when available (same file names).

| File | Use | Size (bytes) | SHA-256 |
|---|---|---|---|
| `logo.png` | Full bilingual lockup on the charcoal sign-in band | 19086 | `2e814aec71c0cb2e8b0edd2c79a99f4bcd23c8549bc4afbdccce12ef97c9276d` |
| `logo-mark.png` | Shield in the top bar | 5229 | `9b87af88b9de07570498154b71ba4901213c86cec8552dcc2f070d14df4636ae` |
| `icon-64.png` | Small icon (shield on charcoal) | 2294 | `7ea2eab1cae3b276b9d62056a7795583651db8aac34118c26a5ed9cf7ffcac2d` |
| `icon-192.png` | Touch icon | 7499 | `0a74eac990eaf61521c03e8fbce883cc7e3ce2152710b2cb0241d5405c4b2dc3` |
| `../../favicon.ico` | Browser tab icon | 5711 | `530ccf33f4ff7dd762a412415708174b02f55044329d2619f734b545dec3c73c` |

The brief's suggested files (`https://lu.ac.ae/wp-content/uploads/2025/05/liwa-university-scaled.png`, the white
wordmark for indigo backgrounds, and `cropped-icon-300x300-C-270x270.png`) could not be fetched automatically; ask
marketing for them, or for vector originals.

## Names

University: **Liwa University** / **جامعة ليوا**. Program titles follow the brief (§3); their Arabic titles are drafts
listed in `translations/REVIEW.md` until checked against https://lu.ac.ae/ar/.
