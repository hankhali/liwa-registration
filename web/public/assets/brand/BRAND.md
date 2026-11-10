# Brand assets

Colours, fonts and logo files used by the app, where they come from, and how to replace them.

## Colours

As the brief (§9) specifies, from lu.ac.ae's stylesheets:

| Token | Value | Used for |
|---|---|---|
| `--liwa-header` / `--liwa-primary` | indigo **`#3d3d5f`** | Top bar, headings, links, secondary buttons (a lighter `#c9c9ee` stands in for text in the dark theme) |
| `--liwa-gold` | gold **`#f7bf0a`** | The one primary button per screen, active menu items, the rule under the top bar |
| `--liwa-on-gold` | `#26263d` | Text on gold (8.7:1) |

The logo pixels are `#3b3a59`, close enough to the stylesheet indigo that the app uses `#3d3d5f` throughout. The pale
amber critical-path badge (`--liwa-gold-bg` / `--liwa-gold-fg`) is a tint of the same gold. All colours are tokens in
`web/src/styles.scss` (light and dark), and the Material palettes in `web/src/styles/_theme-colors.scss` were generated
from indigo (primary) and gold (tertiary). `python scripts/check_contrast.py` checks every text/background pair against
WCAG AA.

From 2026-11-09 to 2026-11-10 the app briefly used a charcoal/sage palette sampled from the Blackboard sign-in page;
the project owner reverted that to the brief's indigo and gold ([docs/assumptions.md](../../../../docs/assumptions.md) D1).

## Fonts

Frutiger (the university's font) is licensed, so the app uses **Open Sans** and **Noto Sans Arabic**, self-hosted from
the npm packages `@fontsource/open-sans` and `@fontsource/noto-sans-arabic` (no font CDN). Material Icons come from the
`material-icons` package.

## Logo files

Downloaded unmodified from lu.ac.ae on 2026-11-10 (plain image fetch, as the brief allows).

| File | Source | Use | Size (bytes) | SHA-256 |
|---|---|---|---|---|
| `liwa-university-scaled.png` | https://lu.ac.ae/wp-content/uploads/2025/05/liwa-university-scaled.png | White wordmark (2560×922). **Only on indigo**: the top bar and the sign-in band | 16808 | `7bd640f57debe2210703b2cc2b2e3035c20a2715f4a521a8ef15b83114955347` |
| `cropped-icon-300x300-C-270x270.png` | https://lu.ac.ae/wp-content/uploads/2025/05/cropped-icon-300x300-C-270x270.png | Indigo shield (270×270) for light backgrounds, favicon and touch icon | 3242 | `eef527ecf04f8579cf68daa1c4c34c34150882557ae15d16eeb67dcba41bfa16` |

The wordmark is English only, so the sign-in band adds the Arabic name (جامعة ليوا) as text underneath. Vector
originals from university marketing would render more sharply. Replace the files under the same names when you have them.

## Names

University: **Liwa University** / **جامعة ليوا**. Program titles follow the brief (§3); their Arabic titles are drafts
listed in `translations/REVIEW.md` until checked against https://lu.ac.ae/ar/.
