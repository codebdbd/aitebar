# Unify Quick Note Image Interaction

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It is maintained according to `PLANS.md` in the repository root.

## Purpose / Big Picture

Quick Note images must behave as objects, not as broken fragments of text. A user can insert an image, click it immediately to see that it is selected, press Delete to remove only that image, then continue editing text without reopening the note. Saving and reopening must preserve the image and retain the same interaction.

## Progress

- [x] (2026-08-25 03:20Z) Reviewed the current event routes, persistence helpers, export adapters, and focused tests.
- [x] (2026-08-25 03:34Z) Introduced `QuickNoteImageInteractionController` as the sole owner of image selection, cursor feedback, and safe deletion.
- [x] (2026-08-25 03:34Z) Removed image-specific selection, hit-test, child event registration, and deletion branches from `QuickNoteWindow` partial files.
- [x] (2026-08-25 03:36Z) Updated focused tests to exercise the controller instead of private window methods.
- [x] (2026-08-25 03:37Z) Ran the full Quick Note test set: 129 passed. Release build compiled with zero errors but the environment returned nonzero because NuGet vulnerability metadata is unreachable.
- [ ] Perform the manual window scenario and create the installer if requested.

## Surprises & Discoveries

- Observation: The current implementation has image selection and deletion in both `QuickNoteWindow.xaml.cs` and `QuickNoteWindow.Editor.cs`.
  Evidence: `TxtNote_PreviewMouseLeftButtonDown`, `TxtNote_PreviewMouseMove`, `SelectImage`, and `RegisterImageSelection` are in `QuickNoteWindow.xaml.cs`; deletion paths are in `QuickNoteWindow.Editor.cs`.
- Observation: A `Border` child inside an `InlineUIContainer` did not survive the current XamlPackage round trip.
  Evidence: `Package_RestoresInlineImageWithoutRtfMarkerConversion` failed when the image was wrapped in `Border`.

## Decision Log

- Decision: Keep the durable representation as an `InlineUIContainer` whose child is `Image`.
  Rationale: It is the representation proven by the existing XamlPackage and RTF round-trip tests. Runtime selection decoration must not become persisted document content.
  Date/Author: 2026-08-25 / Codex.
- Decision: Put all runtime image interaction in a dedicated controller rather than in window partial handlers.
  Rationale: One object must own mouse selection, keyboard deletion, cursor state, and transient decoration so routed WPF events cannot be handled twice.
  Date/Author: 2026-08-25 / Codex.

## Outcomes & Retrospective

Work in progress. The prior approach added overlapping routed-event handlers and did not provide a stable ownership boundary. The completed controller must replace those handlers rather than coexist with them.

The controller now owns the image interaction boundary. The window delegates mouse and Delete events to it, and performs autosave only after a reported document mutation. The package representation remains the tested `InlineUIContainer` plus `Image`; no runtime visual wrapper is saved into the note.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` declares `TxtNote`, the WPF `RichTextBox` used by Quick Note. `AiteBar/QuickNoteWindow.xaml.cs` owns window lifecycle and receives the XAML routed input events. `AiteBar/QuickNoteWindow.Editor.cs` owns editor commands and image insertion. `AiteBar/QuickNoteImageHelper.cs` creates images, enforces byte and pixel limits, and encodes their PNG payload for saving. `AiteBar/QuickNoteService.cs` saves the normal `.aite-note` XamlPackage format. `AiteBar/QuickNoteRtfAdapter.cs` and `AiteBar/QuickNoteMarkdownExchange.cs` provide compatible export paths.

An `InlineUIContainer` is a WPF document element that embeds a UI control inside text. It is durable in the existing note format, but WPF text selection is designed for text rather than embedded controls. The controller will therefore hold transient object selection separately and will never store runtime visual state in the document.

## Plan of Work

Create `AiteBar/QuickNoteImageInteractionController.cs`. It will accept the editor `RichTextBox` in its constructor and expose methods for the only image-related input routes: `TrySelectFromMouseInput`, `UpdateCursorFromMouseInput`, `TryDeleteSelected`, `ClearSelection`, and `Dispose`. It will locate a container by walking the WPF event source upward, verify that the container still belongs to the document, draw and remove a transient image effect, and safely remove the selected container from its parent `InlineCollection`. It will return an interaction result so `QuickNoteWindow` performs autosave and footer updates exactly once.

Modify `QuickNoteWindow.xaml.cs` so its existing mouse and key handlers delegate only to the controller. Remove direct image fields, visual-tree lookup helpers, child event registration, and direct image deletion handling from `QuickNoteWindow.xaml.cs` and `QuickNoteWindow.Editor.cs`. `InsertImage` will notify the controller that a new container exists but will not attach an event handler to the container. Loading a document will ask the controller to clear any stale runtime selection. The durable image helper and export adapters remain format-only code.

Add focused tests in `AiteBar.Tests/QuickNoteImageInteractionControllerTests.cs` for selection and deletion of a newly created image without a save/reload, selection after a package reload, clearing selection after a text click, and keeping ordinary text deletion outside the controller. Update or retire reflection tests that target removed `QuickNoteWindow` image methods.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

1. Implement the controller and wire its narrow interface into the window.
2. Run:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~QuickNote"

   Expect all Quick Note tests to pass. If the WPF test host does not finish, run:

       dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll --TestCaseFilter:"FullyQualifiedName~QuickNote"

3. Run:

       dotnet build .\AiteBar.sln -c Release --no-restore

4. Manually verify: insert an image, click it once, observe the selected state, press Delete, type text, save, close, reopen, then repeat click and Delete.

## Validation and Acceptance

Acceptance requires all of the following: a just-inserted image can be clicked once and visibly selected; Delete removes only that image; the editor remains editable immediately afterward; a saved and reloaded image behaves identically; a click on text clears the image selection; keyboard text deletion still works; image round-trip, RTF, Markdown, and all Quick Note tests pass.

## Idempotence and Recovery

The controller changes only runtime state and can be reconstructed whenever the window is created. The persisted `InlineUIContainer` plus PNG payload format is unchanged. If a test exposes an unsupported WPF visual decoration, remove only the decoration and keep the controller ownership boundary; do not wrap the persisted image in a new UI control.

## Artifacts and Notes

The baseline focused test command before the controller refactor passed 129 Quick Note tests. The prior `Border` persistence failure is captured above and must remain covered by package round-trip tests.

## Interfaces and Dependencies

`QuickNoteImageInteractionController` must be an internal WPF-only class. It may depend on `System.Windows.Controls.RichTextBox`, `System.Windows.Documents.InlineUIContainer`, `System.Windows.Input`, `System.Windows.Media`, and `QuickNoteImageHelper`. It must not depend on persistence services, settings services, dialogs, or window lifecycle. The window owns persistence and invokes autosave only after the controller reports that a document mutation occurred.

Revision note (2026-08-25): Created after the image interaction review to replace overlapping window-level event handling with a single controller.

Revision note (2026-08-25): Recorded the completed controller refactor and validation evidence. The remaining manual scenario requires a visible desktop session.
