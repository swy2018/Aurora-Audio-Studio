---
name: Aurora Audio Studio
description: A calm, local-first Windows audio studio shaped around real creative work.
colors:
  paper: "#f6fbf8"
  surface: "#ffffff"
  surface-mint: "#eaf6f1"
  ink: "#17241f"
  muted: "#5f7169"
  line: "#cfe0d8"
  accent: "#248368"
  accent-deep: "#165f4d"
  accent-soft: "#d9eee6"
  accent-hover: "#0f513f"
  focus: "#0f6a52"
  local-panel: "#173a30"
  local-panel-deep: "#0d2a22"
  local-panel-text: "#edf8f3"
  local-panel-muted: "#c7ded5"
typography:
  display:
    fontFamily: "Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI, sans-serif"
    fontSize: "clamp(48px, 5vw, 68px)"
    fontWeight: 690
    lineHeight: 1.03
    letterSpacing: "-0.045em"
  headline:
    fontFamily: "Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI, sans-serif"
    fontSize: "clamp(36px, 4vw, 58px)"
    fontWeight: 680
    lineHeight: 1.1
    letterSpacing: "-0.04em"
  title:
    fontFamily: "Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI, sans-serif"
    fontSize: "25px"
    fontWeight: 700
    lineHeight: 1.65
    letterSpacing: "-0.025em"
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, Yu Gothic UI, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.65
    letterSpacing: "normal"
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI, Yu Gothic UI, sans-serif"
    fontSize: "14px"
    fontWeight: 600
    lineHeight: 1.65
    letterSpacing: "normal"
rounded:
  sm: "12px"
  md: "16px"
  lg: "24px"
  pill: "999px"
spacing:
  compact: "12px"
  control-x: "22px"
  panel: "38px"
  section: "124px"
components:
  button-primary:
    backgroundColor: "{colors.accent-deep}"
    textColor: "{colors.surface}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "0 22px"
    height: "48px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.surface}"
    rounded: "{rounded.sm}"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.sm}"
    padding: "0 22px"
    height: "48px"
  language-button:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.pill}"
    padding: "0 14px"
    height: "38px"
  workbench-tab-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent-deep}"
    rounded: "{rounded.sm}"
    padding: "19px 18px"
---

# Design System: Aurora Audio Studio

## Overview

**Creative North Star: "The Windows Creator's Desk"**

Aurora feels like a well-kept creative workstation: calm white working space, mint organization fields, deep green actions, and familiar Fluent proportions. The interface gives the workbench and its real output context visual authority; decoration stays quiet so creators can scan, decide, and continue working. Its application mark is a tactile black rounded rectangle with a luminous teal A and compact vertical waveform.

The system is professional without becoming corporate or sterile. Asymmetry, restrained perspective, and the approved A-wave icon give it a specific human-made identity, while durable Windows typography and measured density keep it practical across Chinese, English, Traditional Chinese, and Japanese layouts. Neon AI blue or purple, generic glass surfaces, and grids of interchangeable cards are explicit anti-references.

**Key Characteristics:**

- Calm white and mint working surfaces with rare deep-green emphasis.
- Fluent typography, control sizing, and rounded proportions.
- Real Aurora workbench imagery as evidence, not abstract AI spectacle.
- Asymmetric editorial layouts that remain task-led and scannable.
- Visible focus, reduced-motion support, and responsive reflow.

## Colors

The palette is a quiet local-studio spectrum: warm mint paper and clean white surfaces support dark forest text, while deep green is reserved for actions and active states.

### Primary

- **Workbench Green:** The main accent for workflow numbering and restrained emphasis.
- **Deep Action Green:** The strongest action color, used for primary buttons, active navigation states, and high-confidence interaction.
- **Soft Selection Mint:** The selected and highlighted surface behind active workbench choices.

### Neutral

- **Studio Paper:** The page canvas and sticky-header foundation.
- **Clean Surface:** The foreground surface for controls, proof strips, and substantial containers.
- **Mint Field:** A quiet sectional layer and secondary hover surface.
- **Forest Ink:** The default high-contrast text color.
- **Sage Copy:** Supporting copy, navigation, and metadata.
- **Soft Divider:** Borders and structural separators.
- **Local Console Forest:** The dark ownership panel used to make local file control feel tangible.

### Named Rules

**The Deep-Green Action Rule.** Use deep green for actions and selected states, not as a broad decorative wash.

**The Real Workbench Rule.** Product imagery shows the approved Aurora workbench; abstract AI gradients never substitute for working evidence.

## Typography

**Display Font:** Segoe UI Variable Display (with Segoe UI and Microsoft YaHei UI fallbacks)
**Body Font:** Segoe UI Variable Text (with Segoe UI, Microsoft YaHei UI, and Yu Gothic UI fallbacks)
**Label/Mono Font:** Consolas for local paths only

**Character:** The typography is unmistakably Windows-native but carefully typeset. Tight display tracking gives headlines an editorial edge; readable body spacing keeps dense bilingual explanations calm.

### Hierarchy

- **Display** (690, fluid 48–68px, 1.03): Hero statements only; keep the line length near 10.5em.
- **Headline** (680, fluid 36–58px, 1.1): Major section openings with compact tracking.
- **Title** (700, 25px): Focused panel and local-ownership headings.
- **Body** (400, 16px, 1.65): Default explanations and interface-adjacent prose; prominent leads rise to a fluid 18–21px with a 1.75 line height.
- **Label** (600, 14px): Navigation and compact metadata; release labels use 700 for stronger status emphasis.

### Named Rules

**The Fluent Voice Rule.** Use Segoe Variable roles first and retain the CJK fallbacks; do not introduce a fashionable display face that breaks the Windows studio character.

## Layout

The primary container is capped at 1240px and keeps 22px side gutters on wide screens. Hero and work sections use asymmetric two-column compositions: language or task choices occupy the smaller rail while the real workbench receives the larger field. Large sections use a deliberate 124px vertical rhythm, with thin full-width strips used for factual proof and separation.

At 1050px, the hero becomes a single column and secondary grids tighten. At 760px, side gutters become 16px, primary sections reduce to 86px vertically, complex grids collapse to one column, navigation links hide, and workbench tabs become a horizontally scrollable 150px rail. The app preview loses perspective on mobile so the interface remains readable rather than theatrical.

**The Workbench Leads Rule.** When copy and product UI share a view, copy establishes meaning and the larger workbench image proves it.

## Elevation & Depth

Depth is selective and ambient. Most structure comes from tonal surfaces and one-pixel dividers; substantial workbench imagery receives the strongest diffuse green-tinted shadow, while primary actions and important containers receive smaller lifts. Perspective belongs only to the large desktop workbench presentation and is removed on mobile.

### Shadow Vocabulary

- **Workbench Float** (`0 34px 90px rgba(38, 76, 61, .16), 0 10px 28px rgba(38, 76, 61, .10)`): Approved app frames and workbench screenshots.
- **Primary Action Lift** (`0 12px 26px rgba(22, 95, 77, .22)`): Primary call-to-action buttons.
- **Quiet Container Lift** (`0 18px 50px rgba(38, 76, 61, .08)`): Large informational containers that need gentle separation.
- **Local Panel Lift** (`0 26px 70px rgba(20, 58, 48, .22)`): The dark local-ownership panel.

**The Tonal-First Rule.** Separate ordinary regions with paper, white, mint, and dividers; reserve shadows for actions, substantial containers, and real workbench imagery.

## Shapes

The system uses gently rounded Fluent geometry rather than bubbles or glass capsules. Controls and selected tabs use 12px corners, medium surfaces may use 16px, and large workbench or content containers use 24px. The only fully pill-shaped control is the compact language switcher. Circular geometry is allowed as a restrained background field behind product imagery, not as a general component motif.

## Components

### Buttons

- **Shape:** Gently curved controls with a 12px radius and a 48px minimum height.
- **Primary:** Deep Action Green with white text, a matching border, horizontal 22px padding, and a quiet action shadow.
- **Hover / Focus:** Hover lifts by 2px and deepens the green; keyboard focus uses a 3px solid green outline with a 3px offset.
- **Secondary:** White with a soft divider border; hover changes the surface to Mint Field while preserving dark text.

### Cards / Containers

- **Corner Style:** Large workbench and requirement containers use 24px corners.
- **Background:** Clean Surface for neutral containers; Local Console Forest for the ownership panel.
- **Shadow Strategy:** Follow the selective ambient vocabulary; most sections remain flat.
- **Border:** Use soft one-pixel dividers for lists and structural separation.
- **Internal Padding:** The dark local panel uses 38px; the requirement container uses 52px on wide screens and 30px by 24px on mobile.

### Navigation

The sticky navigation is 72px tall with a lightly translucent Studio Paper background and a soft divider. The approved A-wave icon appears at 38px beside a 680-weight wordmark. Links are compact Sage Copy labels that deepen to green on hover; mobile hides the link row but retains the icon and language control.

### Language Switcher

The bilingual switch is the system's only pill control: a 38px-tall white button with a soft border, compact 13px semibold type, and a mint hover state.

### Workbench Tabs

Workbench choices are full-width text buttons in a vertical rail. Resting tabs are transparent and muted; hover adds a translucent mint field, while the selected tab uses Soft Selection Mint and Deep Action Green. On mobile they become a horizontal, keyboard-operable rail rather than a stacked card grid.

## Do's and Don'ts

### Do:

- **Do** let white, mint, and dividers carry most of the page structure.
- **Do** keep deep green scarce enough that actions and selected states remain obvious.
- **Do** use the approved A-wave icon and real Aurora workbench imagery at their native proportions.
- **Do** preserve visible keyboard focus and the reduced-motion path.
- **Do** collapse layouts cleanly for narrow screens and long localized strings.

### Don't:

- **Don't** introduce neon AI blue, purple gradients, or glowing synthetic-network imagery.
- **Don't** use generic glass panels as the default material language.
- **Don't** turn workflows into a repetitive card-heavy template.
- **Don't** add decorative claims, metrics, endorsements, or UI components without product evidence.
- **Don't** let perspective or animation reduce the readability of the actual workbench.
