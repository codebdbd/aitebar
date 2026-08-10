# Add Paintings mode to Prompt Builder

## Purpose

Add a separate `Paintings` mode to Prompt Builder. It turns a short idea into an English, art-directed painting prompt and lets the user select a stable artistic direction from a localized catalog.

## Progress

- [x] Added a compatibility-safe `Paintings = 7` category and a persisted painting-style setting.
- [x] Added a localized Paintings tab, Auto style selector, and a catalog of classic art directions and media.
- [x] Added a distinct painting system prompt and retry variation that preserves the idea and style.
- [x] Release build completed without warnings; focused Prompt Builder, settings, and localization tests passed (110/110).
- [x] Expanded the painting catalog with eight art movements and added the separate Animation mode with its persisted style catalog.

## Decisions

- Existing category values remain unchanged; Paintings is appended to prevent migration regressions.
- The catalog stores both a localized UI label and a deterministic English prompt descriptor.
- The default is Auto. Repeat changes composition and atmosphere, not the requested core or selected style.
- Animation is a separate mode rather than a painting style because it requires distinct character-design and sequential-art direction.

## Validation

The UI must show `ModePaintings` and `CmbPaintingStyle`; the service must inject the selected descriptor; settings must preserve both the mode and style; all strings must exist in en, ru, uk, and de.
