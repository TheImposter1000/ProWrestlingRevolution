# Pro Wrestling Revolution

An original, single-player Windows wrestling management project. Version 0.3.0 focuses on the visual identity of its image-based desktop main menu, while preserving MDB universe and career management.

## Start the game

Download the latest [Windows x64 release ZIP](https://github.com/TheImposter1000/ProWrestlingRevolution/releases), extract it to a writable folder, and double-click ProWrestlingRevolution.exe. A separate .NET installation, Python, and Visual Studio are not required.

The exact client area is 1366 by 768 pixels, fixed size with no automatic scaling. On displays smaller than that client area, the entire window may not fit.

## Native, image-based main menu

The left toolbar is a single graphical sidebar image with clickable hotspots rather than text buttons. Main-menu views use the alternate sidebar without a Back icon. Inner screens show the Back icon. Hovering highlights the icon using another image loaded from the skin folder. The top bar is also an image with clickable sound-state, Help, Minimize and Close regions.

The graphical assets deliberately use the same component dimensions as the provided layout references but feature original Pro Wrestling Revolution art, not TEW marks or characters.

| Image in each skin folder | Dimensions | Purpose |
| --- | --- | --- |
| Title_Screen(1).jpg | 1314 x 649 | Main title artwork |
| Title_Screen.jpg | 57 x 736 | Inner-screen icon strip with Back |
| Title_Screen_Alt.jpg | 57 x 736 | Main-menu icon strip without Back |
| Title_Screen_On.jpg | 1370 x 32 | Top title bar, speaker icon on |
| Title_Screen_Off.jpg | 1370 x 32 | Top title bar, speaker icon off |
| Title_Screen_Hover_*.jpg | 57 x 736 | Hover images for inner screens |
| Title_Screen_Alt_Hover_*.jpg | 57 x 736 | Hover images for main menu |

The built-in Obsidian and Steel raster packs are generated during the Windows build and included in the ZIP. A custom skin can be created by copying a built-in folder, editing the actual images, and selecting the skin in the main-menu Skins screen. There are no skin.ini files or color configuration files controlling the title screen.

Navigation order: Back (inner screens only), New Game, Load Game, Database Selector, Database Editor, Skins, Achievements and Credits. The user can also press Escape to return home and Alt+N, Alt+L, Alt+D, Alt+E, Alt+S, Alt+A or Alt+C to navigate.

The top bar's speaker icon toggles a visual sound state. Audio playback is not implemented yet.

## MDB universes and careers

The app imports your own valid Microsoft Access .mdb files through Database Selector. Each universe is independent:

    ProWrestlingRevolution.exe
    Databases/
        MyUniverse/
            PWR.mdb
            SaveGames/
                MyCareer/
                    PWRSave.mdb
                    Backups/
                        initial.mdb
    Pictures/
    Skins/
        Obsidian/
            Title_Screen.jpg
            Title_Screen_Alt.jpg
            Title_Screen_On.jpg
            Title_Screen_Off.jpg
            Title_Screen(1).jpg
            ...hover images...
        Steel/
            ...same raster assets...
    System/
        settings.ini

A new career copies the universe MDB, preserving the starting universe. Database table previews require a matching 64-bit ACE OLE DB provider on Windows; copying and importing files do not.

Gameplay is not yet connected to the MDB model. This release is the title-screen and application architecture phase. It does not claim to implement a full TEW-like simulator or edit TEW's original data safely. Original copyrighted TEW assets and databases are not redistributed.

## Building

The GitHub Actions Windows workflow generates the raster assets as genuine JPG files, validates the skin dimensions and universe file-management routines, publishes a self-contained Windows executable, and packages a portable ZIP. It runs on pushes to main. Skin assets are also generated locally on first launch if missing, without overwriting existing custom images.

The source files remain C#/.NET 10, Windows Forms, and System.Data.OleDb. There is no license activation system.
