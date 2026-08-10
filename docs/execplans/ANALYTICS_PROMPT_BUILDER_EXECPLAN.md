# Unified Analytics Prompt Builder

## Purpose

Replace the overlapping Analysis and Ideas tabs with one Analytics tab. The tab must retain the useful behaviours of both through a saved, localized direction selector.

## Progress

- [x] Map the legacy Ideas persisted mode to Analytics without changing enum values.
- [x] Add the persisted analysis-direction setting and clone support.
- [x] Add the localized Analytics tab, direction selector, and system-prompt substitution.
- [x] Update focused tests and function documentation.
- [ ] Build and run focused tests.

## Decisions

- `PromptBuilderCategory.Ideas` remains a legacy enum value for reading historical settings and callers, but is not presented in the UI.
- Analytics is represented by the stable `PromptBuilderCategory.Analysis` value, avoiding a breaking migration for existing users.
- Solution generation is a direction inside Analytics rather than an independent mode, so it can be evaluated with constraints and trade-offs.

## Verification

The window exposes only `ModeAnalytics`; selecting a direction persists it and injects its descriptor into the generated system prompt. A saved legacy Ideas mode opens the Analytics tab.
