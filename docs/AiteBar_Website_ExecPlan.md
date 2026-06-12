# AiteBar Product Website

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` from the repository root. Keep this document self-contained whenever it is revised: a future contributor should be able to continue the work by reading this file and the current working tree only.

## Purpose / Big Picture

AiteBar already has a desktop product and repository, but it does not yet have a polished product website living inside this repository. After this change, the repository will contain a fully ready marketing site in `site` that explains what AiteBar is, who it is for, how it works, why it is different from bookmarks or scattered shortcuts, how to download it, and how to support the author. The site must be deployable as a static website and must include a dedicated donation screen rather than only a small footer link.

The user-visible outcome is simple to verify. Open `site/index.html` in a browser or serve `site` locally, and a complete landing page should appear with strong calls to action, feature storytelling, FAQ, structured product sections, and a visible route to a separate donation page. Open `site/donate/index.html`, and a full donation screen should appear with clear support messaging and actionable links.

## Progress

- [x] (2026-06-12 06:45Z) Reviewed `AGENTS.md`, `PLANS.md`, `README.md`, `docs/technical-reference.md`, `docs/functions.md`, and `docs/USER_MANUAL.md` to gather product facts.
- [x] (2026-06-12 06:48Z) Confirmed the canonical product URL in `AiteBar/AboutWindow.xaml.cs` and the support URL in `AiteBar/MainWindow.xaml.cs`.
- [x] (2026-06-12 06:52Z) Chose a static, dependency-light website architecture in `site` to avoid coupling delivery to missing system `node`/`npm` executables while still shipping a production-ready result.
- [x] (2026-06-12 07:10Z) Created the `site` folder with `index.html`, `donate/index.html`, `assets/styles.css`, `assets/app.js`, SVG assets, and technical SEO files.
- [x] (2026-06-12 07:18Z) Performed local HTTP validation through a temporary static server; `/`, `/donate/`, `/assets/styles.css`, `/robots.txt`, `/site.webmanifest`, and `/sitemap.xml` all returned HTTP 200.
- [x] (2026-06-12 07:22Z) Applied follow-up hardening fixes after review: added missing padding for the hero product card content, narrowed mobile full-width button behavior, and replaced fragile SVG `rgba(...)` attributes with explicit opacity attributes.
- [x] (2026-06-12 07:24Z) Updated this ExecPlan with final implementation notes, validation evidence, and environment limitations.

## Surprises & Discoveries

- Observation: The repository does not currently contain a `site` directory or a JavaScript web app scaffold.
  Evidence: `Get-ChildItem -Force site` returned `Cannot find path`.
- Observation: The machine shell does not expose `node` or `npm` in `PATH`, even though the Codex runtime provides a bundled `node.exe`.
  Evidence: `node -v` and `npm -v` failed in PowerShell, while `load_workspace_dependencies` exposed `C:\Users\ostee\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe`.
- Observation: The app already points users to a canonical product URL and to a real support page.
  Evidence: `AiteBar/AboutWindow.xaml.cs` uses `https://codebdbd.github.io/products/aitebar`; `AiteBar/MainWindow.xaml.cs` contains `https://suvorov.pp.ua/donate/`.
- Observation: Browser automation libraries were present, but bundled Playwright browser binaries were not installed, and direct headless Edge launch for screenshots hit local permission failures.
  Evidence: `await import("playwright")` succeeded, but `chromium.launch()` requested missing browser binaries; direct `msedge.exe --headless --screenshot=...` produced access-denied errors and no files were created.

## Decision Log

- Decision: Build the website as a static site with modular HTML, CSS, SVG, and lightweight JavaScript in `site` instead of scaffolding a framework that requires package installation.
  Rationale: The user asked for a finished site inside this repository. In this environment, system `node` and `npm` are unavailable, and a package-managed framework would add avoidable delivery risk. A static site still supports modern product marketing patterns, advanced SEO, structured data, accessibility, and strong visual design.
  Date/Author: 2026-06-12 / Codex
- Decision: Include a separate donation page at `site/donate/index.html` in addition to donation calls to action on the landing page.
  Rationale: The request explicitly requires a donation screen. A dedicated page satisfies that requirement better than a single footer link or small section.
  Date/Author: 2026-06-12 / Codex
- Decision: Base all marketing claims on repository-supported product behavior and avoid fabricated testimonials, pricing claims, or usage metrics.
  Rationale: The repository provides rich factual material about product capabilities, but it does not provide verified customer numbers, review snippets, or conversion metrics. The website must be persuasive without inventing proof.
  Date/Author: 2026-06-12 / Codex
- Decision: Keep the website framework-free even after discovering bundled Node and Playwright support in the Codex runtime.
  Rationale: The runtime helpers are useful for validation, but the deliverable itself benefits from staying deployable as plain static files without a build dependency chain.
  Date/Author: 2026-06-12 / Codex

## Outcomes & Retrospective

The repository now contains a polished static product website in `site` with a full landing page and a dedicated donation screen. The landing page includes strong product-marketing structure: hero, feature story, use cases, trust messaging, FAQ, clear calls to action, and a visible support path. The donation page is a first-class destination rather than a footer afterthought.

Technical SEO is in place through canonical URLs, metadata, Open Graph and Twitter/X preview tags, schema.org JSON-LD, `robots.txt`, `sitemap.xml`, `site.webmanifest`, `.nojekyll`, and `llms.txt`. Local HTTP validation passed for the main routes and technical files.

The only incomplete validation area is full visual browser smoke testing from this environment. Attempted screenshot automation was blocked by missing Playwright browser binaries and permission failures when launching local Edge in headless capture mode. The HTML, CSS, assets, and SEO files were still validated by local serving and direct response inspection, and a future contributor can finish a visual pass by opening `site` in a normal browser outside this restriction.

## Context and Orientation

The repository is primarily a Windows desktop application written in .NET 8 and WPF. The new website work is isolated from the desktop app and should live entirely under a new top-level folder named `site`. That folder should be deployable to a static host such as GitHub Pages, Netlify, Cloudflare Pages, or any ordinary web server without requiring a build step.

The product facts used for the website come from the existing documentation and source code. `README.md` provides the English product summary and headline capabilities. `docs/functions.md` provides a complete feature inventory, including context panels, drag-and-drop button creation, browser profile rotation, built-in utilities, tray integration, update checks, and Quick Note behavior. `docs/USER_MANUAL.md` explains user scenarios such as marketing, development, and design workflows. `AiteBar/AboutWindow.xaml.cs` contains the product website URL and repository URL. `AiteBar/MainWindow.xaml.cs` contains the support-author URL that the app opens from the tray menu.

The website should speak in plain product language rather than internal implementation language. For example, the site can say "browser profiles" because that term appears in user-facing docs, but it should avoid deep WPF or Win32 explanations unless they directly support trust or compatibility messaging.

## Plan of Work

Create the directory tree `site`, `site/assets`, and `site/donate`. Add `site/index.html` as the primary landing page. That page should include a strong hero, outcome-focused copy, a visual product mockup built from HTML/CSS or SVG, clear download and repository calls to action, a problem-to-solution narrative, feature clusters, workflow use cases, trust and privacy positioning, FAQ, and a donation teaser section linking to the dedicated donation page.

Create `site/donate/index.html` as a full support screen. It should explain why support matters, what donations help fund, and provide direct actions to open the real support URL and the GitHub releases page. The page should feel like a first-class destination, not an afterthought.

Add `site/assets/styles.css` for the full visual system. Use CSS custom properties for color, spacing, typography, shadows, and motion. The visual style should feel intentional and modern rather than a generic SaaS template. It should work on both desktop and mobile widths without horizontal scrolling.

Add `site/assets/app.js` for small progressive-enhancement behaviors only, such as mobile navigation toggling, animated metric reveals, a sticky CTA state, or FAQ accordion behavior. The site must remain readable without JavaScript.

Add `site/assets/favicon.svg` and `site/assets/og-image.svg`. The social image can be an SVG illustration sized for link previews. The favicon can also be SVG, paired with a manifest entry.

Add technical SEO files at the site root: `robots.txt`, `sitemap.xml`, `site.webmanifest`, `.nojekyll`, and `llms.txt`. `robots.txt` should allow indexing and point to the sitemap. `sitemap.xml` should include the landing page and donate page. `llms.txt` should briefly summarize the site for machine-readable discovery. `site.webmanifest` should provide a minimal installable metadata shell for the site.

Finally, serve the directory locally and inspect both pages in a browser. If possible, use a lightweight local server so canonical relative asset loading matches deployment behavior.

## Concrete Steps

Run commands from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect source material:

    rg -n "ProductUrl|DonatePageUrl|RepositoryUrl" AiteBar
    Get-Content -Raw README.md
    Get-Content -Raw docs\functions.md

After implementation, serve the website from the repository root with one of these safe commands:

    C:\Users\ostee\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m http.server 4173 -d site

Then open:

    http://127.0.0.1:4173/
    http://127.0.0.1:4173/donate/

Expected behavior:

    The landing page loads without broken assets, shows the hero, feature sections, FAQ, and footer links.
    The donate page loads as a separate screen with visible support CTAs.
    The browser tab title, description metadata, canonical URL, Open Graph tags, and JSON-LD are present in page source.

Observed validation evidence:

    GET http://127.0.0.1:4173/ -> 200
    GET http://127.0.0.1:4173/donate/ -> 200
    GET http://127.0.0.1:4173/assets/styles.css -> 200
    GET http://127.0.0.1:4173/robots.txt -> 200
    GET http://127.0.0.1:4173/site.webmanifest -> 200
    GET http://127.0.0.1:4173/sitemap.xml -> 200

## Validation and Acceptance

The work is accepted when the following behaviors can be observed:

Opening `site/index.html` or serving `site` locally shows a complete landing page for AiteBar with persuasive copy, clear navigation, responsive layout, working internal links, and calls to action for download, documentation/repository, and donations.

Opening `site/donate/index.html` shows a dedicated donation screen with clear explanation of support, direct outbound support action, and a route back to the product page.

The landing page includes strong SEO basics: descriptive title, meta description, canonical URL, Open Graph tags, Twitter/X preview tags, schema.org JSON-LD for a software application and FAQ content, crawl directives, sitemap, and machine-readable `llms.txt`.

The site remains usable on narrow screens, keyboard focus is visible, color contrast is strong enough for readability, and content is factual with no invented testimonials or unsupported statistics.

Within this environment, acceptance is partially demonstrated through static serving and source inspection. A final human visual pass in a standard browser is still recommended because automated screenshot capture was blocked by local browser-launch restrictions.

## Idempotence and Recovery

The website files are additive and live under `site` and `docs/AiteBar_Website_ExecPlan.md`, so repeating the work should not disturb the desktop application. If a design change goes wrong, deleting or editing only files inside `site` is sufficient to recover. Because there is no build step, broken behavior is easy to isolate by opening the affected HTML file directly or through a simple static server.

## Artifacts and Notes

Important product facts gathered before implementation:

    Canonical product URL: https://codebdbd.github.io/products/aitebar
    Repository URL: https://github.com/codebdbd/aitebar
    Support URL: https://suvorov.pp.ua/donate/
    Supported OS: Windows 10 / Windows 11
    Key product behaviors: hidden edge panel, up to 8 contexts, browser profiles and rotation, drag-and-drop button creation, global hotkeys, Quick Note, import/export of panels, tray control, update checks via GitHub releases

## Interfaces and Dependencies

Use only static web platform primitives: HTML5, CSS, SVG, and a small amount of vanilla JavaScript. Do not require a framework runtime, package manager, or build output for the finished site. The final file set must include at least:

    site/index.html
    site/donate/index.html
    site/assets/styles.css
    site/assets/app.js
    site/assets/favicon.svg
    site/assets/og-image.svg
    site/robots.txt
    site/sitemap.xml
    site/site.webmanifest
    site/llms.txt
    site/.nojekyll

Revision note 2026-06-12: Initial plan created after repository review. The plan chooses a static architecture because that is the most reliable way to deliver a finished marketing site in the current environment while still meeting the SEO and donation-page requirements.

Revision note 2026-06-12: Updated after implementation to record the created file set, local HTTP validation, CSS/SVG hardening changes, and the browser automation limitations encountered during visual smoke testing.
