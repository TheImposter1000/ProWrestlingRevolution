# Pro Wrestling Revolution

An original Windows desktop wrestling management project. Version 0.5.14 migrates skin and selected-database preferences into a genuine Access MDB, retaining the native universe database workflow.

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
`System/PRWDefault.mdb` **beside the running executable**, verifies its SHA-256
before installing it, and preserves any already-installed template instead of
overwriting it. The installation remains portable and requires no repository
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

`PrwSchema.cs` maps the ten original worker-related tables and their declared
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

IMPORTANT: If you install this release in a fresh folder, copy your own skin and MDB data into that folder. When launching through Visual Studio, the runtime skin folder is normally under bin/Debug/net10.0-windows/ rather than the source directory. Use the game's Open Skins Folder button to confirm the actual path.

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

## In-app updates (version 0.5.4 and later)

Click **Check for Updates** at the bottom of the sidebar. The game uses GitHub's
**latest stable release** and checks it against the version embedded in the
Windows executable. If newer, it asks for your approval, downloads the official
portable ZIP from this repository, verifies the release asset SHA-256 digest,
and checks that the ZIP contains a newer Windows executable. It extracts **only**
the root \`ProWrestlingRevolution.exe\` and starts a temporary copy of the
current executable, which waits for the game to exit, replaces the installed
executable, and restarts Pro Wrestling Revolution.

The updater does **not** unpack or overwrite Databases, SaveGames, Skins,
Pictures, System or user settings. Each successful replacement keeps a copy
of the former executable in \`.pwr-updates/\` for recovery. If a download or
digest check fails, the running game is unaffected. If Windows cannot replace
the executable (for example, because of permissions), the user sees an error.

The game must be installed in a **writable folder**. Automatic updates do not
write into protected Program Files directories or elevate privileges. The
installer deliberately updates **application executable only**; future releases
which require external runtime dependencies must extend this protocol.

**Migration:** v0.5.3 and earlier have no automatic updater. Install v0.5.4
once from GitHub, preserving your existing data folders, to enable automatic
updates for subsequent stable versions. Releases must use non-prerelease tags
such as \`v0.5.5\` for the GitHub latest endpoint.
