# Pro Wrestling Revolution

An original Windows desktop wrestling management project. Version 0.5.4 (In-App Updater) redesigns the navigation without replacing your title screen artwork.

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
