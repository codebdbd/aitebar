# Quick Note utility contract

This document describes the expected behavior and UI contract for the Quick Note utility.

## Purpose

Quick Note is a lightweight note window for fast text editing and formatting.
The utility persists note contents between application launches.

## Top formatting toolbar

The top toolbar contains direct formatting buttons only. It must not contain a three-dot menu, dropdown formatting menus, or hidden formatting submenus.

Required toolbar buttons:

1. Decrease font size, shown as `A-`.
2. Increase font size, shown as `A+`.
3. Bulleted list.
4. Numbered list.
5. Bold.
6. Italic.
7. Underline.
8. Strikethrough.
9. Insert or edit link.
10. Code block.
11. Clear formatting.

All buttons use glyphs or compact formatting symbols. Buttons must have tooltips. In light themes all toolbar glyphs and text are black. In dark themes all toolbar glyphs and text use one shared light color.

## Pin function

The note window has a separate pin button in the window controls area.

Expected behavior:

1. When pinned, the note window remains open after losing focus.
2. When unpinned, the note window follows the existing auto-close behavior.
3. The pin button is not part of the formatting toolbar.
4. The pin control is a glyph icon, not a text button.

## Editor context menu

Additional note commands belong in the editor context menu together with standard text commands such as copy, paste, and select all.

The top toolbar must not contain a three-dot menu.

Required custom context menu commands:

1. Choose color.
2. Open file.
3. Clear note.

These commands must be available from the right-click context menu inside the note editor.

## Themes

Themes affect only the note background color and the main note text color.

Theme rules:

1. In every light theme, note text and all toolbar/window glyphs are black.
2. In every dark theme, note text and all toolbar/window glyphs use one shared light color.
3. Theme changes must not affect code block background color.
4. Theme changes must not affect code block text color.
5. Theme changes must not affect code block border color.

## Code block

The code block must be implemented as a separate visual container, visually close to Telegram code blocks.

Required visual structure:

1. A compact header strip at the top of the code block.
2. A small decorative label `code` on the left side of the header.
3. A copy glyph button on the right side of the header.
4. The code content area below the header.
5. The copy glyph never overlaps or hides code text.
6. Rounded corners or clean rectangular corners consistent with the application style.
7. A fixed border or separator that clearly distinguishes the block from normal note text.

Required color behavior:

1. The code block background is the same in all note themes.
2. The code text color is the same in all note themes.
3. The code header background is the same in all note themes.
4. The copy glyph color is the same in all note themes.

Required typography:

1. Code text uses JetBrains Mono.
2. Code text does not use the normal note font.
3. Code text preserves line breaks and indentation.
4. Code text supports programming ligatures when the WPF text stack supports them.

Required copy behavior:

1. The copy control is an icon glyph, not a text button.
2. The copy control is placed on the right side of the header strip.
3. The copy control never overlaps or hides code.
4. Clicking the copy glyph copies only the code content, not surrounding note text.
5. Clicking the copy glyph gives visible feedback that copying happened.

Required editing behavior:

1. Creating a code block must not destroy surrounding note text.
2. Closing the window must not make a code block disappear during the current session.
3. Moving focus away from the note must not convert a code block into plain note text.
4. The code block must remain visually distinct while editing.
5. Clear formatting applied to a code block converts that code block into normal note text.

## Persistence

The utility saves note contents between application launches in a portable package named `QuickNote.aite-note`. The package stores the visual document and embedded images together without file-path dependencies.

The Open file command exports `QuickNote.rtf` beside the package and opens that export through the Windows shell. External edits to the export do not overwrite the package.

Saved note contents must preserve:

1. Normal rich text formatting.
2. Code block visual structure.
3. Links.
4. Lists.
5. Font size changes.
6. Embedded images.

Code blocks are stored in the RTF file as readable fenced code sections starting with ` ```code ` and ending with ` ``` `. When Quick Note loads the file, those sections are restored into the visual code block container with the fixed header and copy glyph.

## Explicit non-goals

The utility must not add:

1. A three-dot menu in the top toolbar.
2. Dropdown formatting controls in the top toolbar.
3. Hidden formatting submenus.
4. Text-labeled copy buttons inside code blocks.
5. Theme-dependent code block colors.
