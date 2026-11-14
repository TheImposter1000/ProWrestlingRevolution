# Worker Contracts visual and interaction contract

Use the latest user screenshot and feedback as the governing specification. The original two Contract screenshots define the screen structure, four card columns and six listing columns; the later screenshot and explicit feedback override decorative details. Implement a cohesive Worker Editor section, not a separate marketing dashboard.

## Visual language

Both Contract screens share the selected skin's **Backgrounds/Worker_Editor.jpg** asset and fall back to Obsidian when it is missing. Draw the image through the existing WorkerBackgroundCache. Main frames, table body and footer are translucent navy glass with thin steel-blue or cyan rims. The artwork remains visible through panels. Never introduce opaque black rectangles behind checkbox rows, fields, or buttons.

Section headers contain only the title: **Basic Details**, **Roster Details**, **Terms, Perks & Bonuses**, and **Roles**. No section icons. No explanatory subtitles or italic descriptions. Footer actions and selection actions use text labels, not icon glyphs. Keep all sections on a fixed-width, four-column layout beneath the identity/biography strip. The top application header remains unchanged.

## Uniform entry controls

**Every schema-backed field** uses the same glass control surface. Text fields (Name, Short Name), numeric values (Wage, Downside, Momentum), dates (Contract Begin), and category/yes-no fields must all support direct keyboard input. When field options are available from the PWR database, clicking a small chevron or pressing Alt+Down/F4 opens a list; typing is never blocked by the existence of a list. All entries have matching height, framing, typography and navy fill. The Biography area remains a multiline text editor.

Validate typed dates as YYYY-MM-DD and require values compatible with their native MDB column types. Employer names must resolve to a real tblFed row. Fields that the native schema does not include may appear in their requested positions, but remain read-only with a clear explanation. Do not invent columns, codes, or unsupported saved values.

## Contracts list

Columns stay ordered Employer, Contract Type, Status, Begins, Expires, Primary Role. Keep the current live filtering, sorting, row selection and active/expired counts. Promotion logos remain supported; remove decorative contract-type or role pictograms. Keep Remove, Open, and Add Contract accessible and text-only. Status dots carry meaning and may remain.

## Contract Editor

The identity strip shows the worker portrait, Employee, Employer, Name, Short Name and Biography in the existing positions. The four cards retain the existing native field order and all eight role checkboxes. Glass panels must share the background image and have a subtle reflection. Do not draw separate black checkbox bars. The bottom frame retains Import Details From Alter Ego, Copy Terms From Another Contract, Reset and Save. Reset must share the exact text-only button design as the other actions, with no native black/white overlay.

## Persistence and tests

Continue to use tblWorker/tblFed to resolve the worker and employer, tblContract for contract data and roles, and tblContractBio for the contract-specific biography. Save and delete operations remain transactional. Never touch unrelated worker data. Keep validation and concurrency safeguards.

Windows CI captures worker-contract-list-preview.png and worker-contract-editor-preview.png using the actual custom input controls and the selected Worker Editor skin artwork. Assert that Name, Short Name, Wage, Downside, Contract Begin and other native choices expose the same editable TextBox interface. Review the previews for black backgrounds, clipped fields, inconsistent chrome or remaining icon clutter before merging. The reference images are acceptance criteria, not assets to bake behind the controls.
