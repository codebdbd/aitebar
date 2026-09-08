# Build the AiteBar product website

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It is maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

Create a complete Russian-language product website for AiteBar that explains its distinctive behavior, lets a Windows user download it confidently, and offers an optional donation without presenting paid plans. The finished site must make the product recognizable in the first screen: the edge of a Windows desktop becomes a compact command panel containing personal buttons and built-in tools. A visitor should understand that AiteBar is free, open source, local-first, and available for Windows 10 and 11.

## Progress

- [x] (2026-08-31 00:00Z) Reviewed the project handbook, marketing description, design guide, privacy policy, security policy, feature catalog, user manual, README, and current implementation evidence.
- [x] (2026-08-31 17:40Z) Scaffolded the dedicated `site/` project with the official Sites starter and shadcn support.
- [x] (2026-08-31 17:43Z) Built and handed off the first meaningful local preview at `http://localhost:3000/` with the edge-panel product visual and download action.
- [x] (2026-08-31 17:54Z) Completed the one-page narrative, responsive layout, metadata, favicon, and generated social preview; refined the primary promise to the explicit user-choice statement “Рабочий процесс, который выбираете вы.”
- [x] (2026-08-31 17:55Z) Ran production validation and published version 2 privately through Sites.

## Surprises & Discoveries

- Observation: Several prose documents lag behind current code for Quick Note storage and clipboard defaults.
  Evidence: `AiteBar/QuickNoteService.cs` uses `QuickNote.aite-note`, and `AiteBar/Models.cs` defaults `ClipboardManagerPersistHistory` to false. Website claims must follow code rather than stale prose.

- Observation: The current built-in catalog contains 21 tools, while older documentation mentions smaller totals.
  Evidence: `AiteBar/UtilityButtonCatalog.cs` defines 21 entries in `All`.

## Decision Log

- Decision: Build a focused single-page product site in Russian, with anchored navigation and no pricing section.
  Rationale: The product is permanently free; a paid-tier comparison would contradict the requested positioning. Donation belongs after trust and product value are established.
  Date/Author: 2026-08-31 / Codex

- Decision: Position AiteBar as “the useful edge of your screen,” not as another launcher or generic productivity app.
  Rationale: The edge-triggered, hidden command panel is the product’s most recognizable and differentiating behavior.
  Date/Author: 2026-08-31 / Codex

- Decision: Use a dark Windows-native visual language with professional blue accents and an original UI mockup built from semantic HTML/CSS.
  Rationale: It aligns with `docs/DESIGN.md` and explains the product interface without inventing a decorative illustration.
  Date/Author: 2026-08-31 / Codex

## Outcomes & Retrospective

The complete Russian product site is live at `https://aitebar.comnander.chatgpt.site`. It presents AiteBar as a free local-first Windows workflow hub, explains the edge-panel mechanism, covers contexts, browser profiles, scripts, 21 built-in tools, privacy, FAQ, and optional donation without pricing tiers. The final positioning uses active language—“Рабочий процесс, который выбираете вы”—to make user agency explicit, with matching metadata and social preview. The site is owner-only in Sites until the user explicitly chooses broader access.

## Context and Orientation

The repository root contains the WPF application in `AiteBar/`, tests in `AiteBar.Tests/`, and product documentation in `docs/`. There is no existing web project. The new site lives in `site/` so it is isolated from the .NET solution. The primary source of truth for product behavior is current code, especially `AiteBar/UtilityButtonCatalog.cs`, `AiteBar/HotkeyService.cs`, `AiteBar/Models.cs`, `AiteBar/QuickNoteService.cs`, and the panel implementation. Marketing copy uses `docs/MARKETING_DESCRIPTION.md` as a starting point but corrects stale claims using implementation evidence.

The site is a narrative surface: it guides a visitor from recognition to understanding, trust, download, and optional donation. “Local-first” means normal settings and notes remain on the user’s Windows computer; network access occurs only for explicit functions such as update checks, user-configured AI requests, or optional telemetry.

## Plan of Work

Create `site/` using the official Sites scaffold with shadcn. Replace starter content with a single Russian route. The first viewport includes a compact header, the headline “Рабочий процесс, который выбираете вы,” free/open-source trust labels, a primary download action, a secondary “Посмотреть, как работает” action, and a product-specific panel mockup. The next section explains the three-step interaction: move to the edge, choose an action, return to work.

Continue with use-case lanes rather than a feature dump: web profiles and accounts, scripts and commands, local tools, and writing/AI workflows. Follow with an interactive or compact catalog of the 21 tools, a privacy and trust section, a free/open-source statement, and a donation card. Add concise FAQ items for supported Windows versions, local storage, updates, AI keys, and import/export. Finish with download and repository calls to action.

Use the product palette from `docs/DESIGN.md`: `#1A1A1C` background, `#252526` panels, `#007ACC` accent, soft borders, compact 4–8 pixel radii, and Segoe-style typography. Motion should demonstrate panel reveal and hover behavior but respect reduced-motion preferences. The page must be responsive, keyboard accessible, and usable without JavaScript except for optional disclosure interactions.

## Concrete Steps

From the repository root, initialize and install the site in `site/` using:

    npm create --yes @openai/sites@0.3.0 site -- --yes --add-ons shadcn --install

Inspect the generated scripts, page, layout, styles, shared UI components, and `.openai/hosting.json`. Start the development server from `site/`, implement the first coherent viewport, verify the local URL responds successfully, and open that URL in Codex. Then finish the remaining sections and metadata, run the production build, and publish using the Sites hosting workflow.

Expected local validation includes a successful route response and a production build with no TypeScript or bundling errors.

## Validation and Acceptance

The first screen must answer four questions without scrolling: what AiteBar is, what makes it different, which platform it supports, and how to download it for free. The product visual must show a hidden edge panel rather than a generic dashboard.

The complete page must include product mechanism, audience/use cases, current built-in tools, browser/profile workflow, local-first privacy, free/open-source positioning, optional donation, FAQ, and final download action. It must not contain pricing tiers, trial language, subscriptions, invented testimonials, fabricated user counts, or unsupported performance claims.

At mobile widths, navigation and cards must not overflow, controls must remain touch-sized, and the panel visual must still explain the edge interaction. Keyboard focus must remain visible. Reduced-motion users must not receive continuous decorative movement.

## Idempotence and Recovery

The scaffold command must run only once in the empty `site/` directory. Subsequent work edits existing files and uses the project’s package manager. If installation is interrupted, rerun the package manager install command rather than reinitializing the site. No application source files under `AiteBar/` are modified.

## Artifacts and Notes

The main deliverables are the `site/` source tree, the deployed Sites URL, and this living ExecPlan. Product claims are intentionally conservative: 21 catalog tools, 10 contexts, Windows 10/11, free MIT license, local storage by default, optional donation, and explicit network use for selected features.

## Interfaces and Dependencies

Use the generated React/Sites stack and its installed shadcn primitives. Use Lucide icons from the preinstalled `lucide-react` package. Avoid adding a state library, form framework, analytics package, or backend because the requested site is a static product narrative. External actions use ordinary links to GitHub Releases, the repository, documentation, and the project’s configured donation destination when that URL can be confirmed from repository sources.

Change note: Initial plan created after documentation and implementation audit. It records the free/open-source positioning, required one-page information architecture, and validation path.

Change note (2026-08-31): The primary promise was changed from the interface mechanism to an active statement of user agency, “Рабочий процесс, который выбираете вы,” following direct product-owner feedback. The edge-panel behavior remains the supporting differentiator and visual proof.
