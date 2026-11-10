# Pro Wrestling Revolution

An original Windows desktop wrestling management project. Version 0.5.0 (Minimal Edition) redesigns the navigation without replacing your title screen artwork.

## Windows application

Download the portable ZIP from https://github.com/TheImposter1000/ProWrestlingRevolution/releases. Extract it to a writable directory and run ProWrestlingRevolution.exe. No Python or separate .NET installation is required.

## Minimal Edition title screen

The logical client area is 1366 x 768, with the same Windows display scaling configuration as v0.4.

The main menu has a slim top utility bar with the title and one version label. The top-right sound, help, minimize and close controls remain functional. The 215-pixel sidebar has the existing skin-defined 57-pixel graphical icon strip plus clean text labels, including New Game, Load Game, Database Selector, Database Editor, Skins, Achievements and Credits. The Back icon returns to Home from internal pages.

The center displays the selected skin's original Backgrounds/Title_Screen.jpg with no second logo, added slogan, or redundant information footer. The picture is resized proportionally to fit the available width; the dark bottom edge extends to the bottom of the window. The original JPG is never modified. The LowerBars/Title_Screen.jpg file is NOT drawn on the home screen.

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
                MyCareer/
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
