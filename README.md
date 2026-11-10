# Pro Wrestling Revolution

An original Windows desktop wrestling management project. Version 0.5.3 (Reference Layout) redesigns the navigation without replacing your title screen artwork.

## Windows application

Download the portable ZIP from https://github.com/TheImposter1000/ProWrestlingRevolution/releases. Extract it to a writable directory and run ProWrestlingRevolution.exe. No Python or separate .NET installation is required.

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
