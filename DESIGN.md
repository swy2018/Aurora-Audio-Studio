---
name: Aurora Audio Studio
description: A calm local creative studio for Windows and Mac, with a workflow-first Aurora 2.0 website.
colors:
  paper: "#f6fbf8"
  surface: "#fff"
  ink: "#17241f"
  muted: "#52675d"
  line: "#cfe0d8"
  mint: "#eaf6f1"
  accent: "#165f4d"
  hover: "#0f513f"
  focus: "#0f6a52"
typography:
  display:
    fontFamily: "Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI, sans-serif"
    fontSize: "clamp(36px, 4.5vw, 68px)"
    fontWeight: 750
    lineHeight: 1.15
    letterSpacing: "-0.025em"
  headline:
    fontFamily: "Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI, sans-serif"
    fontSize: "clamp(29px, 3.1vw, 44px)"
    fontWeight: 700
    lineHeight: 1.15
    letterSpacing: "-0.025em"
  title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI, sans-serif"
    fontSize: "28px"
    fontWeight: 700
    lineHeight: 1.15
    letterSpacing: "-0.025em"
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.65
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI, sans-serif"
    fontSize: "13px"
    lineHeight: 1.65
rounded:
  field: "8px"
  icon: "10px"
  surface: "12px"
spacing:
  compact: "12px"
  grid: "24px"
  panel: "32px"
  section: "100px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.surface}"
    rounded: "{rounded.surface}"
    padding: "14px 28px"
  button-primary-hover:
    backgroundColor: "{colors.hover}"
    textColor: "{colors.surface}"
  workflow-tab:
    textColor: "{colors.ink}"
    padding: "25px 12px"
  workflow-tab-selected:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.surface}"
    padding: "25px 12px"
  channel-select:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "10px 34px 10px 12px"
  workflow-copy:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.surface}"
    padding: "32px"
---

# Design System: Aurora Audio Studio

## Overview

**Creative North Star: "The Local Creator's Desk"**

Aurora uses calm white working surfaces, pale mint surroundings, forest text, and deep-green actions. Windows and Mac share that identity while retaining their own desktop typography, controls, and platform behavior. The approved dark rounded A-wave icon remains the application mark.

This refresh records the implemented website in docs/index.html, docs/styles.css, and docs/app.js. Its selected direction is workflow-first, candidate 5, seed 3bf050c7, as recorded in the page's direction contract. The website presents Aurora 2.0 with equal Windows and Mac visibility. Actual Mac 1.9.9 captures, labeled with their capture version, provide product evidence. Version branding does not imply that 2.0 final has shipped; release truth remains in PRODUCT.md and the release data.

The frontmatter records website tokens, not replacement native-app tokens. The native rules below preserve application-specific behavior. The rendered references are .impeccable/review/desktop.png, mobile.png, and hero-repro.png; these visual records do not assert model inference success or release verification.

**Key Characteristics:**

- White and mint structure with forest text and clear green actions.
- Equal platform visibility and a shared, calm studio identity.
- Real product screenshots at their original proportions.
- Flat surfaces, readable task information, and restrained geometry.
- Visible keyboard focus, localized copy, and responsive reflow.

## Colors

The website palette uses pale mint paper and white surfaces to organize information; dark forest text and deep green distinguish reading from action.

### Primary

- **Deep Action Green** (accent): primary downloads, selected workflow tabs, links, and the hero headline.
- **Deep Hover Green** (hover): pointer feedback on links and primary buttons.
- **Focus Green** (focus): keyboard outlines, independent of hover.

### Neutral

- **Studio Paper** (paper): page background.
- **Clean Surface** (surface): header, controls, workflow copy, download articles, and model disclosure.
- **Mint Field** (mint): unselected tab hover and table headings.
- **Forest Ink** (ink): main text.
- **Sage Copy** (muted): descriptions, navigation-adjacent information, and captions.
- **Soft Divider** (line): borders, tab divisions, and table rows.

The native Mac shell keeps its observed soft selection mint (#D9EEE6) separately from the website's solid-green selected tabs. Do not replace platform selection colors merely to match the website.

## Typography

### Website

One native sans stack serves all roles: Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI, then sans-serif. The browser selects an installed face; the website is not Windows-exclusive. Headings use balanced wrapping and the frontmatter's shared tight tracking.

- **Display:** hero only; at widths up to 520px the implemented override is 37px.
- **Headline:** section headings; at widths up to 520px the override is 29px.
- **Title:** platform download headings.
- **Body:** ordinary prose, with a maximum line length of 74ch. The hero lead uses clamp(18px, 1.7vw, 24px), capped at 70ch; workflow descriptions use 18px.
- **Label:** release information and workflow metadata use 13px; screenshot captions use 12px; the model table uses 14px.
- Primary buttons use weight 600. Hero platform actions use 21px on wide screens and 16px at widths up to 520px.

### Native Windows localization — retained application rules

The web marketing scale does not apply to native production controls. Desktop body and labels use 14 DIPs, section headings 20, and page titles 28 (24 for Japanese). Japanese uses bundled Noto Sans JP at normal body weight and semibold headings, 22-DIP explanatory leading, and a 232-DIP sidebar. Simplified Chinese uses Microsoft YaHei UI; Traditional Chinese uses Microsoft JhengHei UI; English uses Segoe UI. Code and paths retain Consolas. Preserve Windows text scaling.

Settings are left aligned. Language selection is immediately applied and persisted; the explanatory hint sits directly below the selector. Storage settings retain a separate Save action. A language switch must not reset the active workbench's inputs. Embedded Japanese UI uses the same bundled family with 24/20/17-pixel heading roles and 1.65–1.75 body leading.

### Native Mac implementation

The Avalonia shell uses a light Fluent theme with a 14-point window font default. Its MainWindow selects PingFang SC for Simplified Chinese, PingFang TC for Traditional Chinese, and the platform default for Japanese and other languages. Preserve these actual Mac choices and native WebView content; do not impose the Windows font stack or website heading scale. Sources: work/audio-studio/AuroraAudioStudio.Mac/App.axaml and MainWindow.cs.

## Layout

The website container is min(1392px, 100% - 64px), centered. The header remains in normal document flow; navigation has an 84px minimum height. The centered hero places its headline, lead, equal platform actions, and release label above the workflow surface. Hero padding is 58px above and 32px below (60px above from 1400px).

Six workflow tabs share one horizontal grid. The panel below uses minmax(280px, .8fr) and minmax(0, 1.2fr), a 24px gap, and 24px top margin. From 1400px the ratio becomes .85fr / 1.15fr and workflow-copy padding becomes 36px. Download articles use equal columns with a 24px gap; local-control and update sections use 1fr / 1.2fr with a 52px gap. Major later sections are separated by 100px; downloads start with 90px top padding.

At widths up to 850px, gutters become 18px, navigation wraps, tabs become a three-column grid, and the workflow panel becomes one column. Workflow-copy padding becomes 26px; download articles remain two columns with a 16px gap and 22px padding. Local-control and update sections become one column with a 24px gap. Download top padding becomes 64px; later section separation becomes 68px.

At widths up to 520px, gutters become 14px and download articles stack. Hero platform actions remain equally prominent side by side, each flexing to available width. Workflow tabs retain three columns and a 62px minimum height. The brand's small-name segment hides; navigation links remain available and wrap. The model table preserves a 700px minimum width inside its own keyboard-focusable horizontal scroll region.

Native Windows retains its approved A workbench layout. The Mac shell retains its own navigation, content panels, and native WebView work areas; website composition does not replace desktop application layout.

## Elevation & Depth

The current website has no box shadows, perspective transforms, glass blur, or hover lifts. White surfaces, mint page space, and one-pixel borders provide separation. Screenshots remain flat and legible at every breakpoint. The previous website's floating workbench, dark ownership panel, and ambient shadow vocabulary are no longer current web guidance.

Smooth scrolling is the only explicit website motion; the reduced-motion media query switches it to automatic scrolling. Do not infer animation rules from the historical website.

## Shapes

Website panels, images, primary buttons, tabs' outer enclosure, and disclosures use the surface corner token. The channel selector uses the field token; the brand image uses the icon token. Dividers remain one pixel. The language action is a transparent text button with a left divider, not a pill.

Native Mac controls keep their own geometry: buttons, text fields, and combo boxes use 8-point corners and a 40-point minimum height; buttons use 16 by 8 padding. Standard content panels use 12-point corners and 24-point padding. These values are distinct from the website button's 56px minimum height.

## Components

### Buttons

Primary links are solid Deep Action Green with white semibold text, a 56px minimum height, 18px content gap, and the frontmatter padding. Hover deepens the fill without movement. Hero downloads have a 270px minimum width on wide screens; mobile removes that minimum and uses 13px by 9px padding. Package links occupy their article's full width.

Keyboard focus uses a 3px solid Focus Green outline with a 4px offset. Disabled button styling sets opacity to .55 and the default cursor. No separate website secondary-button variant is currently implemented.

### Workflow Tabs

A shared bordered enclosure groups six task buttons. Default buttons are transparent with Forest Ink text; hover uses Mint Field; selection uses Deep Action Green and white. Tab focus places the outline inside with a -5px offset. ARIA selection and a roving tab stop follow the active workflow; left/right arrows wrap, and Home/End select the first/last task. The panel updates its heading, description, input, engine, output, screenshot, and accessible label together.

### Content Panels and Product Images

Workflow copy uses white with the standard panel padding; platform articles use 30px padding before responsive overrides. Images fill their column with automatic height, a one-pixel divider border, and rounded corners; links open the original capture. Captions identify the actual capture context. Preserve the real Mac application and embedded engine UI, including their own colors and controls.

### Navigation and Language

The white header contains the 40px A-wave image, a 22px bold wordmark, links, and a transparent language control. The wordmark reduces to 19px below 850px. The website switches Chinese and English, including screenshot alternative text, and remembers the choice when browser storage is available. This bilingual website is distinct from the four-language desktop applications. A skip link appears on keyboard focus.

### Download Channel and Status

The native select has a white fill, divider border, field corners, and the frontmatter padding. Stable is selected initially. Changing the channel refreshes both platform packages and their polite live status messages. Loading, unavailable-package, and fetch-failure copy stay visible near the corresponding action. Package versions and checksums come from release data; the page's 2.0 brand is not a hard-coded claim that a final package exists.

### Model Disclosure

A native details/summary control reveals the model table. The enclosing white surface uses 20px by 24px padding, reduced to 16px below 520px. Mint table headers, 12px cell padding, and divider rows keep capability and license information scannable. Preserve explicit unsupported and management-only states.

## Do's and Don'ts

### Do:

- **Do** let white, mint, and dividers carry the website structure.
- **Do** give Windows and Mac equal download prominence.
- **Do** retain real Mac screenshots, original proportions, and truthful capture-version captions.
- **Do** preserve visible focus, keyboard tabs, reduced motion, and localized reflow.
- **Do** keep native Windows and Mac typography and controls separate from web marketing tokens.

### Don't:

- **Don't** introduce neon AI imagery, purple gradients, or generic glass panels.
- **Don't** turn the six workflows into a repetitive card-heavy template.
- **Don't** replace real application evidence with generated mockups or perspective effects.
- **Don't** describe screenshot presence as inference, installation, or release verification.
- **Don't** present the Aurora 2.0 brand as proof that 2.0 final has shipped.
