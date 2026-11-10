DATABASES
All editable game data is stored beside the running ProWrestlingRevolution.exe,
never in a GitHub repository checkout or the process working directory.

On first launch the executable installs its built-in PRWDefault.mdb template
as System/PRWDefault.mdb. When a newer base ships, the existing template is backed up in
System/TemplateBackups before replacement. This base has 14 native tables.

Use DATABASE SELECTOR > Create Database to make a new universe based on that
local template. Each independent universe has this layout:

Databases/<Universe>/PWR.mdb
Databases/<Universe>/SaveGames/<Career>/PWRSave.mdb
Databases/<Universe>/SaveGames/<Career>/Backups/initial.mdb

Database Selector > Import MDB is still available for existing custom worlds.
When a game career is started the universe is copied, not overwritten.

All universe files, saves, pictures, skins, and user settings belong to the
executable's installation directory. You can move the entire folder together.

Microsoft Access Database Engine (64-bit ACE OLE DB) is required for reading
and editing the MDB data. Copying/creating a universe does not need Access.
