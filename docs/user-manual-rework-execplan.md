# Rework user documentation for release readability

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` in the repository root.

## Purpose / Big Picture

The current `USER_MANUAL.md` contains user instructions, technical implementation notes, and release completeness checks in one long file. After this work, a new user can open `USER_MANUAL.md`, find a quick start immediately, follow task-based instructions, and see reserved places for future screenshots. Technical details and internal verification material remain available in separate reference files.

## Progress

- [x] (2026-05-26 00:00Z) Reviewed `USER_MANUAL.md` structure and confirmed it mixes user guidance, technical reference, and QA checklist content.
- [x] (2026-05-26 00:00Z) Decided on a three-document split: user manual, technical reference, and documentation QA checklist.
- [x] (2026-05-26 00:00Z) Replaced `USER_MANUAL.md` with a shorter task-based manual.
- [x] (2026-05-26 00:00Z) Added `docs/technical-reference.md` for internal paths, settings, implementation behavior, and package details.
- [x] (2026-05-26 00:00Z) Added `docs/documentation-qa-checklist.md` for feature coverage and release documentation checks.
- [x] (2026-05-26 00:00Z) Validated Markdown structure by inspecting headings and repository status.

## Surprises & Discoveries

- Observation: The repository already has unrelated modified and deleted files.
  Evidence: `git status --short` showed changes in `AiteBar/*`, `README.md`, several `docs/*` files, and untracked `USER_MANUAL.md`.

- Observation: The rewritten user manual no longer contains the internal markers that made it read like a technical specification.
  Evidence: Searching `USER_MANUAL.md` for `AppData`, `settings.json`, `Context[1-4]Hotkey`, `cmd.exe`, `powershell.exe`, `SendInput`, `manifest`, `mutex`, `HKCU`, `.NET 8`, `WPF`, `Win32`, `error.log`, and `custom_buttons` returned no matches.

## Decision Log

- Decision: Keep screenshot work as placeholders only.
  Rationale: The user explicitly asked not to touch images yet and to leave room for screenshots.
  Date/Author: 2026-05-26 / Codex

- Decision: Move technical details out of the user manual rather than deleting them entirely.
  Rationale: The source material is useful for maintainers, but it makes the user manual too long and technical for release use.
  Date/Author: 2026-05-26 / Codex

## Outcomes & Retrospective

The documentation is now split by audience. `USER_MANUAL.md` is a shorter user-facing guide with quick start, task instructions, browser-profile comparisons, safety warnings, real scenarios, troubleshooting, FAQ, and screenshot placeholders. `docs/technical-reference.md` keeps implementation and storage details. `docs/documentation-qa-checklist.md` keeps the feature audit and release documentation checklist.

## Context and Orientation

The main file is `USER_MANUAL.md` at the repository root. It is currently a long Russian-language document with sections for installation, UI, feature reference, all settings, troubleshooting, FAQ, a full feature catalog, and completeness checks. The goal is not to change application behavior. The goal is to reorganize documentation so normal users see practical instructions first, while technical readers can still find implementation details in `docs/technical-reference.md` and verification coverage in `docs/documentation-qa-checklist.md`.

## Plan of Work

Rewrite `USER_MANUAL.md` as a concise user guide with a table of contents, quick start, benefits, screenshot placeholders, task-based sections, browser profile comparison, safety warnings, real usage scenarios, troubleshooting, and FAQ. Create `docs/technical-reference.md` with storage paths, settings model notes, action execution details, import/export package behavior, browser-profile behavior, logging, and installer notes. Create `docs/documentation-qa-checklist.md` with the removed feature catalog and completeness checks in a maintainer-facing form.

## Concrete Steps

Run all commands from `d:\01_Codebdbd\01_projects\aitebar`.

Inspect the final heading structure with:

    rg -n "^#{1,4} " USER_MANUAL.md docs/technical-reference.md docs/documentation-qa-checklist.md

Inspect repository status with:

    git status --short

## Validation and Acceptance

This is a documentation-only change. Acceptance is that `USER_MANUAL.md` starts with user-facing content and no longer contains internal implementation sections such as full settings model fields, mutex details, manifest validation internals, or completeness audits. The technical and QA material must remain available in separate files under `docs`.

## Idempotence and Recovery

The changes are additive except for replacing the root `USER_MANUAL.md`. If more detail is needed later, content can be copied from `docs/technical-reference.md` back into a user-facing section after rewriting it as instructions.

## Artifacts and Notes

Heading validation was run with:

    rg -n "^#{1,4} " .\USER_MANUAL.md .\docs\technical-reference.md .\docs\documentation-qa-checklist.md

The new `USER_MANUAL.md` contains 9 screenshot placeholders and is substantially shorter than the previous combined guide.

## Interfaces and Dependencies

No code interfaces or runtime dependencies change. The only artifacts are Markdown files.
