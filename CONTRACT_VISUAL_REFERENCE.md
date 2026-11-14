# Worker Contracts visual and interaction contract

The **General Worker Editor is the master UI template for every worker section**. The Contracts screens must reuse the General screen's actual portrait, biography, field, panel and action rendering rather than independently approximating its colors. Contract layouts may differ because the underlying information differs. The original Contracts screenshots define the data grouping and full-width structure; later user feedback overrides their decoration. The General Worker screenshot defines the shared visual language.

## Visual language

Both Contract screens share the selected skin's **Backgrounds/Worker_Editor.jpg** asset and fall back to Obsidian when it is missing. Draw the image through the existing WorkerBackgroundCache. Main frames, table body and footer are translucent navy glass with thin steel-blue or cyan rims. The artwork remains visible through panels. Never introduce opaque black rectangles behind checkbox rows, fields, or buttons.

Section headers contain only the title: **Basic Details**, **Roster Details**, **Terms, Perks & Bonuses**, and **Roles**. No section icons. No explanatory subtitles or italic descriptions. Footer actions and selection actions use text labels, not icon glyphs. Keep all sections on a fixed-width, four-column layout beneath the identity/biography strip. The top application header remains unchanged.

## Uniform entry controls

**Every schema-backed field** uses the same glass control surface. Text fields (Name, Short Name), numeric values (Wage, Downside, Momentum), dates (Contract Begin), and category/yes-no fields must all support direct keyboard input. When field options are available from the PWR database, clicking a small chevron or pressing Alt+Down/F4 opens a list; typing is never blocked by the existence of a list. All entries use the **General Worker's light silver-white vertical gradient with dark ink**, compact Segoe UI typography and thin steel-blue outline, regardless of whether they accept a date, numeric value, list choice or text. Never render a dark blue TextBox inside a darker blue control frame. Use the shared `WorkerEditorTheme.PaintField` to prevent hollowed-out controls. The biography uses `WorkerEditorTheme.CreateBiography` and `PaintBiographyFrame`: a light editor with dark text, not a navy rectangle.

Validate typed dates as YYYY-MM-DD and require values compatible with their native MDB column types. Employer names must resolve to a real tblFed row. Fields that the native schema does not include may appear in their requested positions, but remain read-only with a clear explanation. Do not invent columns, codes, or unsupported saved values.

## Contracts list

Columns stay ordered Employer, Contract Type, Status, Begins, Expires, Primary Role. Keep the current live filtering, sorting, row selection and active/expired counts. Promotion logos remain supported; remove decorative contract-type or role pictograms. Keep Remove, Open, and Add Contract accessible and text-only. Status dots carry meaning and may remain.

## Contract Editor

The identity strip shows the worker portrait, Employee, Employer, Name, Short Name and Biography in the existing positions. The portrait must use **the exact General Worker 91-by-91 PictureBox, thin border, zoom setting, missing-picture treatment and picture-pack resolution**. Use `WorkerEditorTheme.CreatePortrait` and `LoadWorkerPortrait` in both General and Contracts, not a special Contracts portrait with a different border, caption strip or photograph source. The four cards retain the existing native field order and all eight role checkboxes. Glass panels must share the background image and have a subtle reflection. Do not draw separate black checkbox bars. The bottom frame retains Import Details From Alter Ego, Copy Terms From Another Contract, Reset and Save. Reset must share the exact text-only button design as the other actions, with no native black/white overlay.

## Persistence and tests

Continue to use tblWorker/tblFed to resolve the worker and employer, tblContract for contract data and roles, and tblContractBio for the contract-specific biography. Save and delete operations remain transactional. Never touch unrelated worker data. Keep validation and concurrency safeguards.

Windows CI captures worker-contract-list-preview.png and worker-contract-editor-preview.png using the actual custom input controls and the selected Worker Editor skin artwork. Assert that Name, Short Name, Wage, Downside, Contract Begin and other native choices expose the same editable TextBox interface. Review the previews for black backgrounds, clipped fields, inconsistent chrome or remaining icon clutter before merging. The reference images are acceptance criteria, not assets to bake behind the controls.

## Canonical component reuse and regression check

The shared `WorkerEditorTheme.cs` is the source of truth for the General Worker's colors, silver field gradient, glass card finish, metallic header strip, 91x91 portrait, biography field and action button gradient. `WorkerEditorForm` and `ContractsWorkspaceView` both call it. When new worker sections are introduced, reuse these primitives and only replace the layout and data bindings. No screen-specific redesign of a component already defined in General.

A Windows smoke test checks the Contracts portrait framing, biography surface, native role checkbox styling and directly editable light input fields. The General Worker Editor must remain visually unchanged by shared-component extraction. At the 1366x768 test canvas, a pixel comparison of the original v0.6.8 General preview with the first refactored preview found zero changed pixels. Keep comparing both General and Contracts previews when changing shared rendering.
