# Pro Wrestling Revolution

An original single-player Windows professional wrestling booking simulator. This repository contains the C# Windows desktop application, fictional starter universe, and GitHub Actions pipeline that builds a ready-to-play Windows ZIP.

## Player download

Open **Releases** and download `ProWrestlingRevolution-Windows-x64.zip`. Extract it anywhere writable and double-click `ProWrestlingRevolution.exe`. No Python, Visual Studio, account, license key, or installer is required. All game files stay together, including `Data`, `Saves`, `Pictures`, `Reports`, and `Logs`.

Game careers are saved in the adjacent `Saves/career.db` SQLite file. The original scenario is in `Data/Scenarios/Genesis.json`. Make a copy of your save before starting over.

## Development

The application uses C# / .NET 10 / Windows Forms / Microsoft.Data.Sqlite. It is an original work, not a fork or redistribution of Total Extreme Wrestling or its assets. Every push to `main` runs Windows build checks and prepares the portable ZIP.

The first release is an initial playable prototype, not a feature-complete reproduction of TEW IX.
