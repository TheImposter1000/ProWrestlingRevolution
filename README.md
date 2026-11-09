# Pro Wrestling Revolution

A native Windows professional wrestling management game project. This release focuses on matching the structure and footprint of the reference title screen.

## Download

Get the latest ZIP from [GitHub Releases](https://github.com/TheImposter1000/ProWrestlingRevolution/releases), extract the ZIP to a writable folder, and launch ProWrestlingRevolution.exe. No Python or separate .NET installation.

## Exact title-screen layout

The logical client area is **1366 by 768**. The interface uses an original, image-based title design and a narrow image-sidebar with clickable regions. To match the apparent Windows display-scaled size of older desktop applications more closely, the executable now uses **DpiUnawareGdiScaled** mode. Windows scales the physical pixels according to your system display settings. As a result, a screen capture may have a larger physical footprint than 1366 by 768 at 125% scaling. We have not claimed the two applications will appear identically sized in every browser zoom or monitor configuration.

The title screen is composed of separate files:
 
| Folder and image | Asset size | Function |
| --- | --- | --- |
| Backgrounds/Title_Screen.jpg | 1314 x 649 | Main central artwork |
| LowerBars/Title_Screen.jpg | 1314 x 87 | Bottom artwork |
| SideBars/Title_Screen_Alt.jpg | 57 x 736 | Main menu toolbar |
| SideBars/Title_Screen.jpg | 57 x 736 | Inner-screen toolbar with Back |
| TopBars/Title_Screen_On.jpg | 1370 x 32 | Upper title bar, sound enabled icon |
| TopBars/Title_Screen_Off.jpg | 1370 x 32 | Upper title bar, sound muted icon |

Images are drawn without stretching; the few extra pixels in the reference 1314/1370 files are cropped to the client's available width where needed. Hit regions keep the expected TEW-style navigation order.

## Complete image skin directory standard

Each skin is an independently replaceable folder. The two original bundled image packs, **Obsidian** and **Steel**, are organized as:

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
            [same eight directories]

A third skin can be added by placing an image pack with those folders under Skins. The game discovers packs by checking the title-screen components. No skin.ini or theme-color configuration is required for the visual title screen. The icon hotspot coordinates stay the same, so user-supplied artwork must put its icons at the expected positions.

Obsidian and Steel include original generated graphic files in all eight categories, although many gameplay-specific images will only be populated as those screens are developed.

If an older game folder contains v0.3 images directly inside a skin root, those image files are automatically copied to the correct categories when the game starts, without removing the originals. Custom artwork already present in category folders is never overwritten.

## Application and databases

The application starts at its image-based menu: New Game, Load Game, Database Selector, Database Editor, Skins, with Achievements and Credits below.

Microsoft Access MDB universes live separately from career saves:

    ProWrestlingRevolution.exe
    Databases/
        Default/
            PWR.mdb
            SaveGames/
                MyCareer/
                    PWRSave.mdb
                    Backups/initial.mdb
    Pictures/
    Skins/
    System/

The application can import your own MDB and create independent career and backup copies, but this version does not yet implement a fully connected gameplay simulation based on the MDB schema. The read-only database preview needs a compatible 64-bit ACE provider. Microsoft's Access application or its driver is not bundled.

## What passed

The GitHub Windows pipeline compiles a self-contained executable, generates all original raster graphics, verifies that the correct images and categorized directories exist, checks image dimensions and MDB file handling, and publishes a complete ZIP.

No TEW images, CornellVerse data, or protected TEW database files are redistributed. The original TEW game and its developer are not affiliated with this project.
