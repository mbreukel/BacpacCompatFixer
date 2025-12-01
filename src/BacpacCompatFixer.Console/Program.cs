using BacpacCompatFixer.Core;
using System.Text;

/*
    BacpacCompatFixer Console Application
    ======================================

    Removes XTP and AlwaysOn features from model.xml to restore import compatibility.

    Usage:
    dotnet run -- <PathToBacpac> [--no-backup] [--backup-dir <Directory>]

    Example:
    dotnet run -- "C:\\temp\\arstest.bacpac" --backup-dir "D:\\safeBackups"

    Author: Michael Breukel
    License: MIT
*/

class Program
{
    static void Main(string[] args)
    {
        // Set console encoding to UTF-8 for emoji support
        Console.OutputEncoding = Encoding.UTF8;

        // --- Decorative header at startup ---
        PrintHeader();

        var options = ParseArguments(args);
        if (options == null)
        {
            return;
        }

        var service = new BacpacFixerService();
        var result = service.ProcessBacpac(options);

        if (!result.Success)
        {
            Console.WriteLine($"❌ {result.Message}");
            return;
        }

        if (!result.Changed)
        {
            Console.WriteLine($"ℹ️  {result.Message}");
            return;
        }

        Console.WriteLine($"✅ {result.Message}");
        if (!options.NoBackup && result.BackupPath != null)
        {
            Console.WriteLine($"🗂️  Backup: {result.BackupPath}");
        }
        if (result.ModelHash != null)
        {
            Console.WriteLine($"🔒 SHA256 (model.xml): {result.ModelHash}");
        }
    }

    static BacpacFixerOptions? ParseArguments(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return null;
        }
        bool noBackup = false;
        string? backupDir = null;
        string? sourceBacpac = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                if (string.Equals(a, "--no-backup", StringComparison.OrdinalIgnoreCase))
                {
                    noBackup = true;
                }
                else if (string.Equals(a, "--backup-dir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("❌ Missing value for --backup-dir");
                        PrintUsage();
                        return null;
                    }
                    backupDir = args[++i];
                }
                else
                {
                    Console.WriteLine($"❌ Unknown option: {a}");
                    PrintUsage();
                    return null;
                }
            }
            else if (sourceBacpac == null)
            {
                sourceBacpac = a;
            }
        }
        if (string.IsNullOrWhiteSpace(sourceBacpac))
        {
            PrintUsage();
            return null;
        }
        return new BacpacFixerOptions
        {
            SourceBacpac = sourceBacpac,
            BackupDir = backupDir,
            NoBackup = noBackup
        };
    }

    static void PrintUsage()
    {
        Console.WriteLine("❌ Please provide the path to the .bacpac file as the first argument.");
        Console.WriteLine("Usage: dotnet run -- <PathToBacpac> [--no-backup] [--backup-dir <Directory>]");
    }

    static void PrintHeader()
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 BacpacCompatFixer v1.0                       ║");
        Console.WriteLine("║   Removes AlwaysOn/XTP from .bacpac for better compatibility ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ForegroundColor = oldColor;
        Console.WriteLine();
    }
}
