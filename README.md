# Pro Wrestling Revolution

## Version 0.6.5: unified Skills title bar and significantly faster Worker rendering

The Skills title plaque and sound/help/minimize/close controls now share one continuous metallic gradient and cyan baseline. The previously displaced Help question mark is fully vector drawn and stays centered at either bar height. The game's normal top bar remains unchanged outside Skills.

The true source of much of the worker-tab lag was background artwork being stretched and resampled repeatedly through WinForms' transparent controls. The background is now prepared once at the actual page size and reused with one-to-one image copies, retaining the existing appearance. A visible Windows form test, rather than the old offscreen-only cache test, now measures repeat section rendering. In the synthetic CI fixture, repeating General improved from around 2.2 s to 0.53 s, Skills from 1.20 s to 0.45 s, and Contracts from 0.45 s to 0.13 s. Rendering is significantly improved but not instant, and actual-world performance may vary.

For accurate diagnostics, PWR logs local worker profile MDB query and tab-paint durations into `System/Logs/PWR-performance.log`. The log is bounded to 1 MB, never contains worker names, database record IDs or field values, and is not uploaded. Do not edit your MDB to diagnose lag. No universe files or skin backgrounds are overwritten.

## Version 0.6.4: v0.6.2 Worker Editor restoration and fast tabs

The **Worker Editor General page and all generic profile sections** have been restored to their complete v0.6.2 input controls, formatting, labels, dropdown choices, and underlying edit/save behavior. The v0.6.3-only worker field conversion rules have been removed because the native MDB is being redesigned. The modern full-width **Skills workspace is retained** as a separate screen; it is not part of the reverted General page.

The previous tab switch deleted and recreated the entire WinForms control tree on each selection. Tabs are now built once per worker and cached. Returning to a tab restores its controls and staged values without reconstructing the page. Loading a different worker invalidates and disposes the cache. The shared Save actions are reattached when changing sections so they remain functional, and Back from Skills restores the v0.6.2 Worker Editor.

The Skills footer no longer includes Auto Balance; Preset, Reset and Save remain. Category-header pictograms are removed without changing the four-panel layout. Numeric spinners have vertical spacing and compact fonts that stay inside their row bounds. Sound/help/minimize/close icons are centered in the taller Skills top bar. The rest of the original blue/cyan styling is retained.

Windows smoke tests profile a populated 40-column contract and repeated navigation through ten worker sections. On the test runner, the first Contracts switch took 17 ms and the cached visit took 1 ms. Those measurements are from an in-memory test fixture rather than your actual universe or hardware and are not guaranteed on all installations. Native MDB table definitions have not changed.

The standard updater installs **only the EXE**; no saves, databases, skins, artwork, or settings are overwritten. Real Access MDB write tests require Microsoft ACE OLE DB, which is unavailable on the GitHub-hosted runner.

## Version 0.6.3: full Worker Skills Editor and MDB field corrections

Version 0.6.3 includes the reference-matched, full-width Worker Skills Editor and revised Worker Editor input rules. Check for Updates installs the standalone Windows EXE; it does not replace existing database or skin files.

The Worker Editor no longer shows a fictional Picture Type selection based on the unrelated `BiographyType` column. Birth, debut and death dates, height and weight measurements, physical conditions and face/heel gimmick names are now typed directly into the worker profile. Date and numeric inputs use explicit validation against the current MDB column type, while other categorical values keep real dropdown choices.

The eight Worker Roles are Wrestler, Part-Time Wrestler, Manager, On Screen Personality, Play by Play Commentator, Colour Commentator, Referee and Producer. Competing Division offers Men's Division, Women's Division and Both Divisions, independently of gender identity. Languages offer exactly None, Passable, Basic and Fluent. The legacy `LoyaltyWorkerUID` database column is retained for compatibility, but its display and selectable IDs now refer to companies in `tblFed`, not workers. Worker saving validates the selected company's existence.

Skills now has a dedicated, responsive four-panel workspace covering all 36 native `tblWorkerSkills` columns. Every rating has a synchronized slider and numeric input with a 0 to 100 range; unset values remain unset when allowed by the MDB. Modifications stay in the worker's in-memory DataRows until Save. Reset restores the values loaded when this worker was opened. Existing Worker Editor transactional saving, unsaved-change prompts and inline Save errors remain. The Skills screen does not repeat the worker's name, portrait or biography and does not show unrelated Create Moveset/Suggest Name/Make Biography controls.

The visual design follows the approved 1672 x 941 screenshot, including its 56px PWR title plaque, bright cyan glass-bordered panels, faceted headers, custom Ring/Microphone/Physical/Clipboard icons, illuminated sliders, metallic spinner inputs, and full Preset / Auto Balance / Reset / Save action strip. Its reference screenshot values are used only in the in-memory visual smoke fixture. The Skills title overlay restores the usual launcher header when the editor exits.

Preset offers defined, confirm-before-applying in-memory templates (Balanced at 50, or an In-Ring, Entertainment, Physical, or Commentary specialty). Auto Balance shifts only populated values toward an average of 50 without changing unset skills. Both are editor utilities, not official TEW simulation algorithms, and neither writes until Save is pressed. Reset restores loaded values. No replacement database or new skill fields are introduced. Automated Windows checks validate field mappings, the number and grouping of Skills controls, rendering, header toggling, and reset/zero/unset behavior. Live worker MDB writes still require the Microsoft ACE OLE DB provider. On GitHub runners without ACE, integration tests are skipped; run those checks against a copy of a universe on an ACE-enabled Windows PC.

## Version 0.6.2: Worker Editor stability and visual update

The Worker Editor dropdown crash caused by disposing a WinForms `ContextMenuStrip` during its `Closed` event has been fixed. Field menus now reuse a live popup with the PWR dark-navy, cyan-outlined theme, and selected choices are applied after the popup interaction finishes. No worker data or existing database field choices have been replaced.

The Worker Editor background has been restored with asynchronous, cached decoding so opening additional worker profiles does not reread or rescale the same image. Built-in skins generate an original `Backgrounds/Worker_Editor.jpg` only if it does not already exist. Custom skins may supply their own artwork at the same path; otherwise PWR falls back to the bundled Obsidian Worker Editor background.

The Picture Type field is now grouped with the portrait, related labels align more closely with the visual reference, and an unused portrait bitmap copy has been removed. Generic startup exceptions no longer report an unrelated protected-folder warning; actual filesystem and permission errors still show it.

Automated Windows checks verify background generation and caching, themed menu reuse, layout structure, and the existing editor smoke tests. As before, transaction-based worker saving still requires manual validation against a real Access MDB when Microsoft ACE OLE DB is available.

## Version 0.6.1: streamlined Worker Editor workflow

When **Workers** is selected in the Database Editor, **Add New Item** opens the same full-screen Worker Editor used for existing profiles. The profile is initially unsaved and creates a worker and its standard one-to-one detail rows only when Save succeeds.

Save commits the worker's changes as one transaction and immediately returns to the Database Editor list. It no longer shows a success dialog or creates a new backup file. Save failures display inline without losing the unfinished profile. The extra Back to Database button is removed; existing Back navigation is retained.

Dropdown controls now load typed choices from the installed `System/PRWDefault.mdb` and the selected universe, instead of acting as decorative arrows. Yes/No choices and current-universe worker loyalty references are handled appropriately, with custom entry for fields that allow it. The values available depend on the fields and actual entries present in these MDB files; PWR does not invent a TEW enum table.

Per-operation backup creation is also removed from Add, Copy, Delete, Import and Mass Edit. Transactions, protected keys and concurrency checks remain. Initial career recovery files are unchanged. **Before testing destructive database operations, keep your own separate copy of any important universe.**

## Version 0.6.0: functional database and worker editing

The Worker Editor supports typed field changes, role and active-area toggles, biography editing, name suggestions and assignment of a default moveset reference. Save writes modified native worker records in a transaction after creating an MDB backup, checks for external edits, and keeps protected IDs unchanged. Full moveset definitions still require their own data model.

The Database Editor now supports Add New Item, Copy Item(s), Delete Item(s), and Import Items for verified native PWR Workers, Companies, and Match Types. The operations create a backup, preserve declared linked details when copying/importing, and avoid duplicating employment contracts. Deleting records also deletes declared dependent records after explicit confirmation. For external or incompatible MDBs, these native editing operations are not offered; browsing and existing supported tools remain available.

Back and Escape now return from the Worker Editor to the Database Editor before taking the player to the main menu. The four top-right utility buttons are anchored to the actual header width. Check for Updates continues to install only the standalone EXE; the complete ZIP is for first-time installs, not updates.

**Important:** This is still an editor and application-shell milestone. Full gameplay simulation and career loading remain separate tasks. Test edits on a copied MDB until validation with your installed Access provider is complete.

An original Windows desktop wrestling management project. Version 0.5.20 separates standalone EXE updates from the complete game installation.

## Version 0.5.20: standalone EXE updates

Each GitHub release now provides a raw **ProWrestlingRevolution.exe** for
updating an existing game, and **ProWrestlingRevolution-Windows-x64.zip**
as the **complete game** for first-time installation. GitHub automatically
provides the tagged source code. There is no separate update ZIP.

The full installation ZIP deliberately retains its original filename so
older Check for Updates clients can still upgrade: the previous updater
extracts only the EXE from that ZIP. From v0.5.20 onwards, Check for
Updates directly downloads the EXE, checks GitHub's published SHA-256
digest and the executable version, backs up the old EXE and restarts.
It never installs or modifies Databases, Pictures, Skins, System or saves.

**Do not extract the complete ZIP over an existing customized game.**
For installed games, download only the EXE or use Check for Updates.
When developing via Visual Studio under the Repos/bin output folders,
use Git Pull, Clean Solution and Rebuild Solution. The in-game updater
now refuses to replace a developer build with a Release executable,
because the two builds can use different folder-resolution rules.

## Version 0.5.19: alphabetical, relationship-based Database Editor

The Current Section menu is now an alphabetical list of primary wrestling
entities, not a list of every physical Access table. The existing 14-table
native PRW database shows **Companies**, **Match Types** and **Workers**.
Workers remain selected initially when available, so existing worker editing
workflows still start with the wrestler directory.

The worker editor brings together identity, biographies, popularity, skills,
physical condition, alternative identities, attributes and employment records.
Double-clicking a company opens a dedicated linked company record screen with
its contracts and contract biographies. Double-clicking a match type opens a
dedicated linked match screen with its ring setups and allowed finishes.
These tabs use only declared PRW foreign-key relationships; they do not
join records by name, position or guessed identifiers.

The categories and linked record section labels come from
`System/PRWEditor.mdb`, with native names as a safe fallback. Categories
and linked section names display alphabetically; the primary entity tab
remains first. The generator also writes metadata category entries in
alphabetical order. Player records stay in the selected universe MDB.

Before grouping records, the editor validates the ten native core tables
and their columns. Any imported database that does not pass that check
continues to show its original tables separately, alphabetically, with the
generic record browser. This prevents a TEW or other external MDB from
being treated as a PRW universe merely because some table names match.

The company and match profile sections are currently **read-only**.
Validated record editing, including Add, Delete and Copy, remains a
separate feature. Existing MDBs, save games, skins, settings and user
changes to `PRWEditor.mdb` are not modified by this release.

## Version 0.5.18: separate Database Editor metadata MDB

The Database Editor now reads optional category captions, field labels,
display order, and visibility from `System/PRWEditor.mdb`, independent
of `System/PRWDefault.mdb` and player universes. This real Microsoft Jet
MDB contains four metadata tables: `tblEditorCategory`,
`tblEditorField`, `tblEditorGroup`, and `tblEditorSettings`.
It is generated from the game's own 14-table `PrwSchema.cs`, rather
than copying TEW game data. Its layout is informed by inspection of
Access metadata properties in the uploaded TEW9Access.mdb.

All record reads, writes, keys, filtering and mass edits continue to
operate on the original field names of the selected `PWR.mdb`. The
metadata MDB changes captions and display ordering only. Unknown
tables and columns retain their native names. If the metadata MDB
cannot be opened, the editor falls back to those native names.

The portable Windows executable embeds the metadata MDB generated
during the release build. A new installation extracts it to
`System/PRWEditor.mdb` on startup. Existing editable copies are
preserved rather than overwritten. Previous universes, careers,
pictures, settings and skins are unchanged.

For source developers, regenerate the MDB with Python, Java 17 and
Maven before compiling if you want native display captions locally:

    python Tools/EditorMdbGenerator/build_editor_manifest.py
    mvn -f Tools/EditorMdbGenerator/pom.xml compile exec:java

This Java build uses the open source Jackcess library. In standard
published game builds no Java, Python or Maven installation is needed.
Windows ACE OLE DB is still required to read and edit Access records.

## Version 0.5.16: expanded PRW default database

The uploaded `PRW_Default(2).mdb` replaces the built-in base for new
universes. It has 14 tables and 12 declared relationships. The four added
tables are `tblFed`, `tblMatch`, `tblMatchRingSetup`, and
`tblMatchFinishes`, with their original fields preserved. The ten existing
worker and contract tables remain supported.

An existing `System/PRWDefault.mdb` is copied to
`System/TemplateBackups` before the replacement is installed. A local
version marker records the upgrade; later user edits to that current
template are preserved. Existing universes, careers, settings, skins,
pictures, and save files are not modified. New universes copy the
replacement template into their own `Databases` folder.

Worker record browsing remains compatible with older ten-table native
universes. The full data check validates all 14 tables for new universes.
Federation and match-specific gameplay and editing are not yet implemented.

## Version 0.5.14: native MDB settings

The game's settings are stored in `System/PWRSettings.mdb`, in a `tblSettings` table with `SettingKey` and `SettingValue` columns. On first successful creation, existing `System/settings.ini` values, including the selected Skin and Database, are imported exactly; the INI is retained as `System/settings.ini.migrated` for recovery. Existing settings databases are never replaced.

Creating and editing the MDB requires the Windows 64-bit Access Database Engine and ADOX. If creation is unavailable, the existing INI continues to work temporarily, without discarding any preferences, until the required components are installed. On Windows installations with ACE, confirm the new settings MDB exists before removing any backup files.

## Windows application

Download the portable ZIP from https://github.com/TheImposter1000/ProWrestlingRevolution/releases. Extract it to a writable directory and run ProWrestlingRevolution.exe. No Python or separate .NET installation is required.

## Approved full-width title screen

The home screen now uses the approved reference composition: a compact upper-left
identity panel, a four-icon utility cluster at upper right, and seven functional
menu tiles in two rows along the bottom. Check for Updates is in the lower-right
corner. The old 200-pixel sidebar and 40-pixel top bar are hidden on the home screen
and remain in place on interior screens. Menu actions, shortcuts, sound, help,
minimize, close, and the in-app updater still work.

For an **exact pixel match** to the approved screenshot, put the supplied
1672 x 941 reference PNG at this relative path in the selected skin:

    Skins/Obsidian/Graphics/Title_Menu_Exact.png

The complete screenshot will then be drawn at the client size while live click
regions keep the buttons functional. This separate UI image does not replace or
modify Backgrounds/Title_Screen.jpg. If the composite is missing, the game draws
the new chrome natively over the existing title-screen crowd-and-logo art.

This screen's design is independent of the controls used by the other game pages.
The screenshot PNG is a user-provided asset; it must be installed separately
unless included in a local skin pack or a release containing that asset.

## Version 0.5.13: local database creation and runtime paths

The default database is the user's original PRWDefault.mdb, embedded losslessly
in the executable. On first launch, the game extracts it into
`System/PRWDefault.mdb` **beside the running executable**, verifies its SHA-256 before installing it. When the bundled template version
changes, the previous local template is backed up before being replaced.
Later custom changes to the current template remain untouched. The installation remains portable and requires no repository
checkout, network request, or current-working-directory dependency.

Database Selector > **Create Database** asks for a name and copies the installed
local base into `Databases/<Name>/PWR.mdb`. This creates an independent universe,
with its own SaveGames folder. Creating a career copies the selected universe to
`Databases/<Name>/SaveGames/<Career>/PWRSave.mdb` and makes an initial backup.
Imports from existing MDBs are still available.

All user data is discovered relative to `AppContext.BaseDirectory`, the
directory containing the executable. The previous special-case upward scan for
a parent folder named `Repos` has been removed. Database files, careers, skins,
pictures, settings and the installed template remain in the application folder;
editing and reading a universe use the local `PWR.mdb` copy.

`PrwSchema.cs` maps all fourteen native tables and their declared
relationships. `PrwWorkerRepository.cs` supports worker identity listings
and full linked record reads, including skills, popularity, physical condition,
biographies, attributes, alternative identities and contracts. The editor
defaults to `tblWorker` and displays linked records on double-click.

This release still does not implement the full gameplay simulation, write/edit
workflows for every category, or row-level referential integrity verification.
The Windows 64-bit Microsoft ACE OLE DB provider is required for accessing MDB
records, but simply copying the built-in template into a new universe does not
require Access to be installed.

## Database Selector and Load Game popups (v0.5.7)

Database Selector and Load Game are now centered modal utility dialogs, not full
pages. Each uses the same reusable shell with a PWR header, two-column layout
(selection on the left, information on the right) and action footer, styled in
dark navy, cyan and red. These tools leave the underlying page intact.

Database Selector lists locally created or imported MDB universes, shows verifiable
file information, and activates the selected database. Create Database, Import
MDB, and Open Folder are available. The selected universe is refreshed when the selector
is called from New Game. The information panel does not fabricate author or
description fields that have not yet been stored in PWR.

Load Game lists real saved careers and their file and backup information.
Rename and Copy are implemented as folder operations. Recovery restores
Backups/initial.mdb after storing the previous active save in Backups.
Delete archives the whole career folder to System/DeletedSaves rather than
destroying it. Check Save inspects the MDB header and table accessibility
without claiming a full database repair. Open Folder works directly. The
Load action is disabled until gameplay save restoration is integrated; the
dialog does not falsely suggest that the simulation can already resume.

No TEW copyrighted skin artwork is used. Existing databases, saved careers,
skins, and custom title-screen graphics are not modified by the release installer.

## Database editor workspace (v0.5.6)

The Database Editor main page is a browsing hub, NOT the detailed editor for an
individual worker or company. It uses the PWR dark navy, cyan and red styling,
without the large "Editing Database" banner. The **active database name is
the heading directly above the record list**. There is no redundant database
dropdown in the left actions column; Change Database is in the bottom toolbar.

The left column has the actual MDB table categories and the action buttons.
The **Mass Edit** field dropdown is immediately underneath the Mass Edit button,
not embedded in the button or placed elsewhere. The centre lists actual record
names with search and sort, and the right column has Notes and Database Summary
tabs. Double-click a record for a full read-only field inspection, with the
option to open the MDB in Access.

Working tools include database switching, MDB import, details, basic schema
accessibility check, External Editing, and text-field Mass Edit. Mass Edit
requires an Access table with exactly one declared primary key, asks for
confirmation, creates a backup under the database's Backups folder, and applies
the update in a transaction. Unsupported tables are rejected rather than
updated by guessed row positions. Add, Copy, Delete, and Import Items remain
disabled until their category-specific editing workflows are implemented.

For large tables, the browser loads the first 10,000 rows and displays the
actual total. The interface uses Microsoft ACE OLE DB; a 64-bit compatible
Microsoft Access Database Engine is required for reading and updating MDBs.

## Minimal Edition title screen

The logical client area is 1366 x 768, with the same Windows display scaling configuration as v0.4.

The main menu has a slim 40-pixel navy metallic top bar with the title, one version label, a red slash emblem, and four original, keyboard-accessible vector controls for sound, help, minimize and close. The 200-pixel brushed-metal sidebar contains seven evenly spaced main-menu rows, plus a Home row on secondary screens. The selected row has a slanted metal right edge, silver reflections and cyan edge lighting. Labels and icon placement follow the reference mockup. Every row draws an original vector icon and text together, with a single cyan hover/active highlight. The old 57-pixel TEW-style graphical icon strip is not used. New Game, Load Game, Database Selector, Database Editor, Skins, Achievements and Credits remain, and Home is available on internal pages. Enter and Space activate a focused item.

The center displays the selected skin's original Backgrounds/Title_Screen.jpg with no second logo, added slogan, or redundant information footer. The image is fit proportionally inside the 1166 x 728 main canvas (at 1366 x 768 logical client resolution), without altering the original file, cropping its logo or stretching the artwork. The original JPG is never modified. The LowerBars/Title_Screen.jpg file is NOT drawn on the home screen.

The inner management screens remain available. This version does not implement full wrestling simulation gameplay.

## Skins

Every direct folder in Skins appears in the selector, even if it has just one image. Missing images fall back to the built-in Obsidian artwork. The existing eight-category structure is retained:

    Skins/
        Obsidian/
            Backgrounds/
            Buttons/
            Graphics/
            Loading_NoProfile/
            Loading_Profile/
            LowerBars/
            SideBars/
            TopBars/
        Steel/
            [same categories]
        YourCustomSkin/
            Backgrounds/
                Title_Screen.jpg

The LowerBars directory is retained for compatibility with later screens, but not used on the title screen. Custom artwork files are not uploaded to GitHub or overwritten by the game.

IMPORTANT: If you install the complete game in a new folder, place your existing custom skins and MDB data in that installation's data folders rather than replacing them. Visual Studio Debug builds under a Repos checkout use the parent game directory when its Databases, Pictures, Skins and System folders are present. Use the game's Open Skins Folder button to confirm the active path.

## MDB structure

    ProWrestlingRevolution.exe
    Databases/
        MyWorld/
            PWR.mdb
            SaveGames/
                    PWRSave.mdb
                    Backups/
                        initial.mdb
    Pictures/
    Skins/
    System/

MDB import and career copying preserve each starting universe. Read-only table previews need a compatible 64-bit Microsoft ACE provider.

## Building and status

Built with C#, .NET 10, Windows Forms and Microsoft Access OLE DB. GitHub Actions performs Windows smoke checks, publishes a self-contained x64 executable and creates a portable ZIP. This is a UI and application-shell milestone, not the complete wrestling simulator.

No copyrighted TEW artwork, protected CornellVerse data or proprietary databases are bundled.

## Version 0.5.1 validation

The Windows smoke test checks that the sidebar is 200 pixels wide, contains all eight unified rows, and no longer adds a standalone sidebar PictureBox. The 40-pixel metallic top bar has four accessible utility buttons and no legacy raster control strip. The skin system still discovers every custom skin folder. Source icon art is drawn in C# rather than bundled from TEW.

## Version 0.5.3 changes

The seven main-menu entries now form a continuous, evenly spaced navigation group; the former blank gap before Achievements has been removed. The MAIN MENU heading and thin line, New Game's blue blade shape, icons, label positions and top bar align more closely with the reference. No title-screen JPG, logo, tagline or crowd artwork has been edited. Because the original user artwork is intentionally preserved, its embedded logo can still have a different size or composition from the reference mockup. All file paths, Access data handling and skin selection remain unchanged.

## In-app updates (version 0.5.20 and later)

The updater checks GitHub's latest stable release. If a newer version
exists, it downloads the standalone `ProWrestlingRevolution.exe`
release asset, verifies the GitHub SHA-256 digest and the executable's
Windows version, then replaces the installed EXE using a temporary
helper process. A backup of the previous executable is saved in
`.pwr-updates/`. Game data folders and the custom skin are not touched.

The existing installation folder must be writable. Visual Studio builds
are deliberately not updated in place; pull and rebuild source code instead.

**Backward compatibility:** The complete game ZIP keeps the legacy
`ProWrestlingRevolution-Windows-x64.zip` asset name. v0.5.19 and earlier
updaters still download that ZIP and extract only its executable, allowing
them to move to the standalone updater in v0.5.20. v0.5.3 and older
versions have no automatic updater and must be installed once manually.
