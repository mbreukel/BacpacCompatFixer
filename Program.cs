using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics;

/*
    BacpacCompatFixer
    =================

    Removes XTP and AlwaysOn features from model.xml to restore import compatibility.

    Background
    ----------
    When importing a .bacpac file into SQL Server or Azure SQL, you may encounter errors like:

        Error SQL72014: Core Microsoft SqlClient Data Provider: Message 35221, Level 16, State 1, Line 1 The operation could not be processed. The manager for AlwaysOn availability group replicas is disabled on this SQL Server instance. Enable AlwaysOn availability groups using SQL Server Configuration Manager. Then restart the SQL Server service and retry the current operation.
        Error SQL72045: Script execution error. The executed script:
        ALTER DATABASE [$(DatabaseName)]
            ADD FILE (NAME = [XTP_6406623E], FILENAME = N'$(DefaultDataPath)$(DefaultFilePrefix)_XTP_6406623E.mdf') TO FILEGROUP [XTP];

    Reason:
    These errors occur because the .bacpac contains references to features that are not supported or enabled on the target SQL Server instance:
    - AlwaysOn (Availability Groups)
    - XTP (In-Memory OLTP, aka eXtensible Table Partitioning)

    If these features are present in model.xml, the import will fail unless the target SQL Server is configured for them. This tool removes all elements and attributes related to AlwaysOn and XTP from model.xml and updates the checksum in origin.xml, making the .bacpac importable on a wider range of SQL Server instances.

    Features:
    - Removes all elements and attributes containing "AlwaysOn" or "XTP" from model.xml in a .bacpac archive
    - Updates the checksum in origin.xml to match the cleaned model.xml
    - Optionally creates a backup of the original .bacpac if changes are made
    - Fast in-place update: only model.xml and origin.xml are extracted, modified, and replaced
    - Compatible with .NET 9
    - Open source, MIT License

    Usage:
    dotnet run -- <PathToBacpac> [--no-backup] [--backup-dir <Directory>"

    Example:
    dotnet run -- "C:\\temp\\arstest.bacpac" --backup-dir "D:\\safeBackups"

    Author: Michael Breukel
    Maintainer: Michael Breukel
    Acknowledgement: Assisted by GitHub Copilot
    License: MIT

    Keywords: bacpac, SQL72014, SQL72045, AlwaysOn, XTP, In-Memory OLTP, import error, model.xml, origin.xml, checksum, compatibility, SQL Server, Azure SQL, fix, repair, remove, script execution error, open source, .NET 9, Copilot
*/

class Program
{
    /// <summary>
    /// Entry point. Requires the path to a .bacpac as the first argument. Optional second argument: output directory (unused in in-place mode).
    /// Optional flags: --no-backup, --backup-dir <Directory>
    /// </summary>
    /// <param name="args">args[0] = input .bacpac path (positional), args[1] (optional positional) = output directory</param>
    static void Main(string[] args)
    {
        // --- Decorative header at startup ---
        PrintHeader();

        var options = ParseArguments(args);
        if (options == null)
        {
            return;
        }

        if (!File.Exists(options.SourceBacpac))
        {
            Console.WriteLine("❌ .bacpac file not found: " + options.SourceBacpac);
            return;
        }

        if (!TryReadModelAndOrigin(options.SourceBacpac, out var modelText, out var originText))
        {
            return;
        }

        var (newModelText, changed) = CleanXmlText(modelText);
        if (!changed)
        {
            Console.WriteLine("ℹ️  No changes: no 'AlwaysOn' or 'XTP' entries found.");
            return;
        }

        string? backupPath = null;
        if (!options.NoBackup)
        {
            backupPath = CreateBackup(options.SourceBacpac, options.BackupDir);
        }
        else
        {
            Console.WriteLine("ℹ️  Backup skipped (--no-backup)");
        }

        string modelHash = ComputeSHA256ModelXmlText(newModelText);
        string newOriginText = UpdateOriginXmlText(originText, modelHash);

        if (!TryUpdateBacpac(options.SourceBacpac, newModelText, newOriginText))
        {
            return;
        }

        Console.WriteLine("✅ Updated package in place: " + options.SourceBacpac);
        if (!options.NoBackup && backupPath != null)
        {
            Console.WriteLine("🗂️  Backup: " + backupPath);
        }
        Console.WriteLine("🔒 SHA256 (model.xml): " + modelHash);
    }

    class Options
    {
        public string SourceBacpac { get; set; } = string.Empty;
        public string? BackupDir { get; set; }
        public bool NoBackup { get; set; }
    }

    static Options? ParseArguments(string[] args)
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
        return new Options
        {
            SourceBacpac = sourceBacpac,
            BackupDir = backupDir,
            NoBackup = noBackup
        };
    }

    static bool TryReadModelAndOrigin(string bacpacPath, out string modelText, out string originText)
    {
        modelText = string.Empty;
        originText = string.Empty;
        Console.WriteLine("📦 Opening package to read 'model.xml' and 'origin.xml'...");
        try
        {
            using (var fs = File.OpenRead(bacpacPath))
            {
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    var modelEntry = FindEntry(archive, "model.xml");
                    var originEntry = FindEntry(archive, "origin.xml");
                    if (modelEntry == null || originEntry == null)
                    {
                        Console.WriteLine("❌ model.xml or origin.xml is missing in the package.");
                        return false;
                    }
                    using (var ms = new MemoryStream())
                    {
                        using (var s = modelEntry.Open())
                        {
                            s.CopyTo(ms);
                        }
                        modelText = ReadText(ms.ToArray());
                    }
                    using (var ms = new MemoryStream())
                    {
                        using (var s = originEntry.Open())
                        {
                            s.CopyTo(ms);
                        }
                        originText = ReadText(ms.ToArray());
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Failed to read entries: " + ex.Message);
            return false;
        }
    }

    static string? CreateBackup(string sourceBacpac, string? backupDir)
    {
        string backupRoot = string.IsNullOrWhiteSpace(backupDir) ? Path.GetDirectoryName(sourceBacpac)! : backupDir!;
        Directory.CreateDirectory(backupRoot);
        string sourceHash8 = ComputeSHA256File(sourceBacpac).Substring(0, 8);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = Path.Combine(
            backupRoot,
            Path.GetFileNameWithoutExtension(sourceBacpac) + $"_orignal_{timestamp}_{sourceHash8}.bacpac"
        );
        File.Copy(sourceBacpac, backupPath, overwrite: true);
        Console.WriteLine($"🗄️  Backup created: {backupPath}");
        return backupPath;
    }

    static bool TryUpdateBacpac(string bacpacPath, string newModelText, string newOriginText)
    {
        Console.WriteLine("🧵 Updating 'model.xml' and 'origin.xml' inside package...");
        try
        {
            using (var fs = new FileStream(bacpacPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Update))
                {
                    ReplaceEntryText(archive, "model.xml", newModelText);
                    ReplaceEntryText(archive, "origin.xml", newOriginText);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Failed to update package: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Find an entry by name case-insensitively. Matches full path or file name.
    /// </summary>
    static ZipArchiveEntry? FindEntry(ZipArchive archive, string targetName)
    {
        foreach (var e in archive.Entries)
        {
            var full = e.FullName.Replace('\\', '/');
            if (string.Equals(full, targetName, StringComparison.OrdinalIgnoreCase))
                return e;
            if (string.Equals(Path.GetFileName(full), targetName, StringComparison.OrdinalIgnoreCase))
                return e;
        }
        return null;
    }

    /// <summary>
    /// Remove elements/attributes containing AlwaysOn/XTP from XML text and return normalized UTF-8 xml text and change flag.
    /// </summary>
    static (string updatedText, bool changed) CleanXmlText(string xmlText)
    {
        // Parse from string to avoid XmlReader encoding switching exceptions
        var doc = XDocument.Parse(xmlText, LoadOptions.PreserveWhitespace);

        var toRemove = doc.Descendants()
            .Where(e =>
                e.Name.LocalName.Contains("AlwaysOn", StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.Contains("XTP", StringComparison.OrdinalIgnoreCase) ||
                e.Attributes().Any(a =>
                    a.Value.Contains("AlwaysOn", StringComparison.OrdinalIgnoreCase) ||
                    a.Value.Contains("XTP", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (toRemove.Count == 0)
        {
            // Still normalize serialization for consistency
            return (SerializeXml(doc), false);
        }

        foreach (var elem in toRemove)
        {
            elem.Remove();
        }

        return (SerializeXml(doc), true);
    }

    static string SerializeXml(XDocument doc)
    {
        // Ensure declaration matches actual bytes
        doc.Declaration = new XDeclaration("1.0", "utf-8", null);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using (var ms = new MemoryStream())
        {
            using (var xw = XmlWriter.Create(ms, settings))
            {
                doc.Save(xw);
            }
            // Return UTF-8 text without BOM
            var bytes = ms.ToArray();
            return Encoding.UTF8.GetString(bytes);
        }
    }

    /// <summary>
    /// Update origin.xml text with the given model.xml hash.
    /// </summary>
    static string UpdateOriginXmlText(string originText, string modelHash)
    {
        var doc = XDocument.Parse(originText, LoadOptions.PreserveWhitespace);
        XNamespace ns = doc.Root!.GetDefaultNamespace();

        var checksumElem = doc.Descendants(ns + "Checksum")
            .FirstOrDefault(e => e.Attribute("Uri")?.Value == "/model.xml");

        if (checksumElem != null)
        {
            checksumElem.Value = modelHash.ToUpperInvariant();
        }

        return SerializeXml(doc);
    }

    /// <summary>
    /// Read bytes as text, detecting BOM. If none, assume UTF-8.
    /// </summary>
    static string ReadText(byte[] bytes)
    {
        using (var ms = new MemoryStream(bytes))
        using (var sr = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            return sr.ReadToEnd();
        }
    }

    /// <summary>
    /// Replace or create an entry with given UTF-8 (no BOM) text. Preserve original casing if entry exists.
    /// </summary>
    static void ReplaceEntryText(ZipArchive archive, string preferredName, string text)
    {
        var existing = FindEntry(archive, preferredName);
        string nameToUse = existing?.FullName ?? preferredName;
        existing?.Delete();
        var newEntry = archive.CreateEntry(nameToUse, CompressionLevel.Optimal);
        using (var es = newEntry.Open())
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
            es.Write(bytes, 0, bytes.Length);
        }
    }

    /// <summary>
    /// Compute SHA256 over normalized model.xml text (UTF-8 without BOM, Unix newlines).
    /// Returns UPPERCASE hex string.
    /// </summary>
    static string ComputeSHA256ModelXmlText(string text)
    {
        text = text.Replace("\r\n", "\n");
        var bytes = new UTF8Encoding(false).GetBytes(text);
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }
    }

    /// <summary>
    /// Compute SHA256 of any file as raw bytes. Returns UPPERCASE hex string.
    /// </summary>
    static string ComputeSHA256File(string filePath)
    {
        using (var sha256 = SHA256.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            }
        }
    }

    /// <summary>
    /// Update origin.xml <Checksums> entry for /model.xml with the given hash (file variant - unused in in-place mode).
    /// </summary>
    static void UpdateOriginXml(string originXmlPath, string modelHash)
    {
        XDocument doc = XDocument.Load(originXmlPath);
        XNamespace ns = doc.Root!.GetDefaultNamespace();

        var checksumElem = doc.Descendants(ns + "Checksum")
            .FirstOrDefault(e => e.Attribute("Uri")?.Value == "/model.xml");

        if (checksumElem != null)
        {
            checksumElem.Value = modelHash.ToUpperInvariant();
        }

        doc.Save(originXmlPath);
        Console.WriteLine("🔧 origin.xml updated.");
    }

    static void PrintUsage()
    {
        Console.WriteLine("❌ Please provide the path to the .bacpac file as the first argument.");
        Console.WriteLine("Usage: dotnet run -- <PathToBacpac> [--no-backup] [--backup-dir <Directory>]");
    }

    // Header-Methode hinzufügen
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