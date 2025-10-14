using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace BacpacCompatFixer.Core;

/// <summary>
/// Service for fixing .bacpac files by removing AlwaysOn and XTP features.
/// </summary>
public class BacpacFixerService
{
    /// <summary>
    /// Processes a .bacpac file to remove AlwaysOn and XTP features.
    /// </summary>
    public BacpacFixerResult ProcessBacpac(BacpacFixerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SourceBacpac))
        {
            return new BacpacFixerResult
            {
                Success = false,
                Message = "Source .bacpac path is required."
            };
        }

        if (!File.Exists(options.SourceBacpac))
        {
            return new BacpacFixerResult
            {
                Success = false,
                Message = $".bacpac file not found: {options.SourceBacpac}"
            };
        }

        if (!TryReadModelAndOrigin(options.SourceBacpac, out var modelText, out var originText, out var error))
        {
            return new BacpacFixerResult
            {
                Success = false,
                Message = error
            };
        }

        var (newModelText, changed) = CleanXmlText(modelText);
        if (!changed)
        {
            return new BacpacFixerResult
            {
                Success = true,
                Changed = false,
                Message = "No changes: no 'AlwaysOn' or 'XTP' entries found."
            };
        }

        string? backupPath = null;
        if (!options.NoBackup)
        {
            backupPath = CreateBackup(options.SourceBacpac, options.BackupDir);
        }

        string modelHash = ComputeSHA256ModelXmlText(newModelText);
        string newOriginText = UpdateOriginXmlText(originText, modelHash);

        if (!TryUpdateBacpac(options.SourceBacpac, newModelText, newOriginText, out error))
        {
            return new BacpacFixerResult
            {
                Success = false,
                Message = error
            };
        }

        return new BacpacFixerResult
        {
            Success = true,
            Changed = true,
            Message = $"Updated package in place: {options.SourceBacpac}",
            BackupPath = backupPath,
            ModelHash = modelHash
        };
    }

    private bool TryReadModelAndOrigin(string bacpacPath, out string modelText, out string originText, out string? error)
    {
        modelText = string.Empty;
        originText = string.Empty;
        error = null;

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
                        error = "model.xml or origin.xml is missing in the package.";
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
            error = $"Failed to read entries: {ex.Message}";
            return false;
        }
    }

    private string? CreateBackup(string sourceBacpac, string? backupDir)
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
        return backupPath;
    }

    private bool TryUpdateBacpac(string bacpacPath, string newModelText, string newOriginText, out string? error)
    {
        error = null;
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
            error = $"Failed to update package: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Find an entry by name case-insensitively. Matches full path or file name.
    /// </summary>
    private ZipArchiveEntry? FindEntry(ZipArchive archive, string targetName)
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
    private (string updatedText, bool changed) CleanXmlText(string xmlText)
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

    private string SerializeXml(XDocument doc)
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
    private string UpdateOriginXmlText(string originText, string modelHash)
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
    private string ReadText(byte[] bytes)
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
    private void ReplaceEntryText(ZipArchive archive, string preferredName, string text)
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
    private string ComputeSHA256ModelXmlText(string text)
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
    private string ComputeSHA256File(string filePath)
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
}
