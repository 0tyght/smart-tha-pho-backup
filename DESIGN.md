---
name: Smart Tha Pho Backup Monitor
description: Observed native Windows backup-server status window; not a web-app design system.
colors:
  system-window: "Window"
  ready-green: "#14642d"
  error-dark-red: "#8b0000"
  message-dark-green: "#142d23"
  pending-brown: "#55410a"
  note-gray: "#414141"
typography:
  title:
    fontFamily: "Tahoma"
    fontSize: "17pt"
    fontWeight: 700
  body:
    fontFamily: "Tahoma"
    fontSize: "10pt"
    fontWeight: 400
spacing:
  window-inset: "24px"
  button-top: "8px"
  action-gap: "10px"
  link-gap: "10px"
components:
  native-button:
    typography: "{typography.body}"
    width: "145px"
    height: "38px"
  state-ready:
    textColor: "{colors.ready-green}"
    typography: "{typography.body}"
  state-error:
    textColor: "{colors.error-dark-red}"
    typography: "{typography.body}"
  state-pending:
    textColor: "{colors.pending-brown}"
    typography: "{typography.body}"
  status-message:
    textColor: "{colors.message-dark-green}"
    typography: "{typography.body}"
  online-url:
    typography: "{typography.body}"
  native-link:
    typography: "{typography.body}"
---

# Design System: Smart Tha Pho Backup Monitor

## Overview

The confirmed direction is a simple native Windows status window. This document captures the implemented WinForms monitor only; it does not define or change the design of the four existing web applications.

Standard Windows controls carry appearance, focus, disabled states, and window chrome. No invented metaphor, custom theme, generated artwork, or shipping raster asset is part of this surface.

**Key Characteristics:**

- Native controls and DPI scaling.
- Left-aligned Thai status text with explicit state labels.
- A read-only online URL and four adjacent actions.

Evidence: `scripts/backup/BackupServerMonitor.cs`, `PRODUCT.md`, and `docs/backup-monitor-surface.md` in `C:/xampp/htdocs/smart-tha-pho-backup`; checked against `monitor-error-min.png`, `monitor-starting-min.png`, and `monitor-stopped.png` in `C:/xampp/htdocs/PRMS-TSM/tmp/backup-install/qa`.

## Colors

### Primary

Ready green marks the written ready state. Dark green is the normal operation-message ink, not a filled action-button treatment.

### Secondary

Dark red marks an explicit error in the message or service rows. Brown marks checking, stopped, and shared-service states; it is not reserved exclusively for warnings.

### Neutral

The form uses `SystemColors.Window`, represented by the system color `Window` in the frontmatter. It is not a hard-coded white theme. The footer uses note gray. Uncustomized labels, buttons, link labels, and the read-only text box retain Windows defaults.

**The Written State Rule.** Color accompanies readable status text; it does not replace the state label.

## Typography

Tahoma is the only explicitly selected family. The title uses the bold title token; the form and its controls inherit the regular body token. These are point sizes, not pixel sizes. No custom line-height, tracking, uppercase style, or separate display face is defined.

## Layout

The centered, resizable form starts with a client area of (780 × 510) and a minimum outer window size of (740 × 540), using `AutoScaleMode.Dpi`. These are source design-unit sizes, not CSS breakpoints.

A single-column `TableLayoutPanel` fills the form with the window-inset token. Its eight rows are (40, 42, 110, 32, 40, 56, remaining space, 42) design units: title, current message, service states, URL label, URL field, actions, utility links, and footer note.

The three-row status table has a fixed label column (170) and a remaining-width value column; each row receives one third of the table height. Labels align vertically in the middle. The URL label aligns bottom-left. The operation message ellipsizes and exposes its full value through a tooltip.

Actions flow left-to-right without wrapping. Each button uses margins of (left 0, top 8, right 10, bottom 0). Links flow top-to-bottom; the local link has margins (0, 5, 0, 10), and the Pages link (0, 0, 0, 10). No mobile layout is implemented.

## Elevation & Depth

No custom shadow or elevation token exists. Windows supplies control borders, window chrome, and interaction feedback. The content area remains flat.

## Shapes

Native rectangular controls retain their platform-drawn borders and corners. The source defines no custom radius, card shape, clipping, or decorative container.

## Components

### Buttons

Four equal native buttons provide start, stop, open website, and copy link. Start is disabled while a controller operation is busy. Stop remains enabled, including during startup cancellation. Open requires a nonempty direct URL and online-ready state; copy requires only a nonempty URL. Hover, keyboard focus, pressed, and disabled rendering remain platform-owned.

### Status rows and message

Ready, error, stopped, shared, and checking states have written labels. An error row directs the user to logs. Readiness probes can replace stale API/database or tunnel status with explicit dark-red failure text. The message uses the normal message token except in an error phase.

### URL field

A dock-filled, read-only native `TextBox` displays the direct URL and has a Thai accessible name. It can remain populated when online access fails; website opening is separately disabled.

### Utility links

Native `LinkLabel` controls expose the local website, backup GitHub Pages link, and runtime log folder. The local link remains available when the public tunnel is unavailable.

### Dialogs and footer

Native message boxes report action failure and confirm closing/stopping. A two-line gray footer explains the local data copy, disabled messaging, and preservation of shared MySQL. No custom dialog theme is defined.

## Do's and Don'ts

### Do:

- Do preserve the approved native Windows form and platform control behavior.
- Do pair state colors with explicit readable labels.
- Do keep stopping available during startup and expose failures through status text and logs.

### Don't:

- Don't apply these tokens or layout rules to the existing web applications.
- Don't introduce a decorative theme, metaphor, or raster artwork into this monitor.
- Don't hard-code platform-default control colors, corners, or focus treatments as custom styling.
