# Pro Wrestling Revolution

An original single-player Windows wrestling management project. Phase One rebuilds the startup shell, skin management and Microsoft Access MDB universe layout.

## What works in Phase One

* A native Windows desktop main menu opens automatically at launch.
* Navigation: New Game, Load Game, Database Selector, Database Editor, Skins, Achievements and Credits.
* Two included color skins, Obsidian and Steel. Additional folders with a skin.ini file appear automatically in the skin selector. Optional background and logo PNG or JPEG images can be configured in skin.ini.
* Import a Microsoft Access .mdb universe into Databases/<Universe>/PWR.mdb. Every universe has its own folder; no universal scenario file and no JSON scenario.
* Create a named career that copies the starting MDB into Databases/<Universe>/SaveGames/<Career>/PWRSave.mdb, plus Backups/initial.mdb. The original is never modified by this operation.
* Browse and preview a database's tables via the installed Access OLE DB provider, with an optional local password entered at runtime. Open the MDB in Microsoft Access for direct editing.

## Download and run

Open the [GitHub Releases](https://github.com/TheImposter1000/ProWrestlingRevolution/releases) page and download the Phase One Windows x64 ZIP. Extract the entire ZIP to a writable folder and run ProWrestlingRevolution.exe. No Python, Visual Studio, or .NET installation is needed to open the main menu.

**No commercial TEW databases or assets are included.** To use the database features, import your own compatible .mdb through Database Selector. If you already use Microsoft Access, its 64-bit Access Database Engine / ACE OLE DB provider can let the app inspect tables. An installed 32-bit Access provider cannot be loaded by a 64-bit executable; use matching driver architecture. Database import and save copying do not require the OLE DB provider.

## Folder structure

    ProWrestlingRevolution.exe
    Databases/
        README.txt
        <Universe>/
            PWR.mdb
            SaveGames/
                <Career>/
                    PWRSave.mdb
                    Backups/
                        initial.mdb
    Pictures/
        README.txt
    Skins/
        Obsidian/skin.ini
        Steel/skin.ini
    System/
        README.txt
        settings.ini

System/settings.ini is created locally and is not uploaded to GitHub. The game does not store the MDB password. New skins may add optional background.png and logo.png, referenced by the BackgroundImage and LogoImage keys in the skin.ini file.

## Current scope

This phase builds the application shell and file-management workflow, not the advanced TEW-style gameplay simulation. You can create and inspect .mdb career files, but there is not yet a gameplay screen connected to the imported MDB's schema. The prior v0.1.0 prototype remains available in Git history and its earlier GitHub release; its JSON scenario and SQLite store are not part of this Phase One ZIP. Future gameplay should use an original, documented MDB schema, not assume a TEW proprietary database can act as a playable Pro Wrestling Revolution universe.

## Development and verification

Built using C#, Windows Forms and .NET 10. The MDB table preview uses System.Data.OleDb on Windows. GitHub Actions runs a smoke test for directory layout, skins, MDB-header recognition, universe import, save copy, backups, settings persistence, and unsafe-path rejection, then publishes a self-contained x64 executable and ZIP. The automated smoke fixture is not a valid MDB; actual Access database connectivity needs to be tested on a matching Windows/Access installation.

No activation, player account, or commercial licensing system is present.
