# Project Migration Guide

## Overview

The BacpacCompatFixer project has been restructured into a multi-project solution with the following components:

### Project Structure

```
BacpacCompatFixer/
├── src/
│   ├── BacpacCompatFixer.Core/          # Class library with core functionality
│   │   ├── BacpacFixerService.cs        # Main service
│   │   ├── BacpacFixerOptions.cs        # Options model
│   │   └── BacpacFixerResult.cs         # Result model
│   ├── BacpacCompatFixer.Console/       # Console application
│   │   └── Program.cs                   # Console UI
│   └── BacpacCompatFixer.Blazor/        # Blazor web application
│       ├── Components/
│       │   ├── Pages/
│       │   │   ├── Home.razor           # Home page
│       │   │   └── BacpacFixer.razor    # Main tool page
│       │   └── Layout/
│       │       └── NavMenu.razor        # Navigation menu
│       └── Program.cs                   # Blazor startup
├── BacpacCompatFixer.sln                # Solution file
└── README.md                            # Documentation
```

## Building the Solution

```bash
# Clone the repository
git clone https://github.com/mbreukel/BacpacCompatFixer.git
cd BacpacCompatFixer

# Build all projects
dotnet build

# Build individual projects
dotnet build src/BacpacCompatFixer.Core
dotnet build src/BacpacCompatFixer.Console
dotnet build src/BacpacCompatFixer.Blazor
```

## Running the Applications

### Console Application

```bash
cd src/BacpacCompatFixer.Console
dotnet run -- "path/to/file.bacpac"
dotnet run -- "path/to/file.bacpac" --backup-dir "backup/path"
dotnet run -- "path/to/file.bacpac" --no-backup
```

### Blazor Web Application

```bash
cd src/BacpacCompatFixer.Blazor
dotnet run
```

Then open your browser to the URL shown in the console (typically http://localhost:5157).

## Publishing Applications

### Console Application

```bash
cd src/BacpacCompatFixer.Console
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
```

### Blazor Application

```bash
cd src/BacpacCompatFixer.Blazor
dotnet publish -c Release
```

## Using the Core Library

You can reference the core library in your own projects:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/BacpacCompatFixer.Core/BacpacCompatFixer.Core.csproj" />
</ItemGroup>
```

Example usage:

```csharp
using BacpacCompatFixer.Core;

var service = new BacpacFixerService();
var options = new BacpacFixerOptions
{
    SourceBacpac = "path/to/file.bacpac",
    NoBackup = false,
    BackupDir = "backup/directory"
};

var result = service.ProcessBacpac(options);
if (result.Success)
{
    Console.WriteLine(result.Message);
    if (result.Changed)
    {
        Console.WriteLine($"Backup: {result.BackupPath}");
        Console.WriteLine($"Hash: {result.ModelHash}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Message}");
}
```

## Migration from Old Version

If you were using the old single-project version:

1. The console application maintains full backward compatibility
2. All command-line arguments work exactly as before
3. Simply use the new path: `src/BacpacCompatFixer.Console/`

## Benefits

- **Modularity**: Core logic separated into reusable library
- **Flexibility**: Choose between CLI and Web UI
- **Maintainability**: Single source of truth for business logic
- **Testability**: Easy to unit test the core library
- **Extensibility**: Easy to add new interfaces (API, GUI, etc.)
