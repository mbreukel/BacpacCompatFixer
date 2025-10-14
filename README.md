# BacpacCompatFixer

Removes XTP and AlwaysOn features from model.xml to restore import compatibility.

## Background / Motivation
When importing a .bacpac file into SQL Server or Azure SQL, you may encounter errors like:

```
Error SQL72014: Core Microsoft SqlClient Data Provider: Message 35221, Level 16, State 1, Line 1 The operation could not be processed. The manager for AlwaysOn availability group replicas is disabled on this SQL Server instance. Enable AlwaysOn availability groups using SQL Server Configuration Manager. Then restart the SQL Server service and retry the current operation.
Error SQL72045: Script execution error. The executed script:
ALTER DATABASE [$(DatabaseName)]
    ADD FILE (NAME = [XTP_6406623E], FILENAME = N'$(DefaultDataPath)$(DefaultFilePrefix)_XTP_6406623E.mdf') TO FILEGROUP [XTP];
```

These errors occur because the .bacpac contains references to features that are not supported or enabled on the target SQL Server instance:
- **AlwaysOn** (Availability Groups)
- **XTP** (In-Memory OLTP)

This tool removes all elements and attributes related to AlwaysOn and XTP from model.xml and updates the checksum in origin.xml, making the .bacpac importable on a wider range of SQL Server instances.

## Features
- Removes all elements and attributes containing "AlwaysOn" or "XTP" from model.xml in a .bacpac archive
- Updates the checksum in origin.xml to match the cleaned model.xml
- Optionally creates a backup of the original .bacpac if changes are made
- Fast in-place update: only model.xml and origin.xml are extracted, modified, and replaced
- Compatible with .NET 9
- Open source, MIT License
- **Available as both Console and Blazor Web applications**

## Project Structure

This solution consists of three projects:

- **BacpacCompatFixer.Core** - Class library containing the core functionality
- **BacpacCompatFixer.Console** - Console application for command-line usage
- **BacpacCompatFixer.Blazor** - Blazor Web application with a user-friendly interface

## Requirements / Prerequisites
- .NET 9 SDK or newer
- Windows, Linux, or macOS

## Installation / Build Instructions
1. Clone this repository:
   ```
   git clone https://github.com/yourusername/BacpacCompatFixer.git
   cd BacpacCompatFixer
   ```
2. Build the project:
   ```
   dotnet build
   ```

## Usage

### Console Application
```
cd src/BacpacCompatFixer.Console
dotnet run -- <PathToBacpac> [--no-backup] [--backup-dir <Directory>]
```
- `<PathToBacpac>`: Path to the .bacpac file to process
- `--no-backup`: (optional) Do not create a backup, even if changes are made
- `--backup-dir <Directory>`: (optional) Directory for backup file (default: same as .bacpac)

### Blazor Web Application
```
cd src/BacpacCompatFixer.Blazor
dotnet run
```
Then open your browser to the displayed URL (typically https://localhost:5001) and navigate to the BacpacFixer page.

## Examples

### Console Application
```
cd src/BacpacCompatFixer.Console
dotnet run -- "C:\\temp\\arstest.bacpac"
dotnet run -- "C:\\temp\\arstest.bacpac" --backup-dir "D:\\safeBackups"
dotnet run -- "C:\\temp\\arstest.bacpac" --no-backup
```

### Blazor Web Application
1. Start the application: `dotnet run` from the `src/BacpacCompatFixer.Blazor` directory
2. Navigate to the BacpacFixer page in your web browser
3. Enter the path to your .bacpac file
4. Optionally configure backup settings
5. Click "Process .bacpac" to fix the file

## License
MIT

## Author / Maintainer
Michael Breukel

## Acknowledgements
Assisted by GitHub Copilot

## Keywords / Tags
bacpac, SQL72014, SQL72045, AlwaysOn, XTP, In-Memory OLTP, import error, model.xml, origin.xml, checksum, compatibility, SQL Server, Azure SQL, fix, repair, remove, script execution error, open source, .NET 9, Copilot, Blazor

## Contributing
Contributions are welcome! Please open an issue or submit a pull request.

## Contact / Support
For questions or support, please open an issue on GitHub.