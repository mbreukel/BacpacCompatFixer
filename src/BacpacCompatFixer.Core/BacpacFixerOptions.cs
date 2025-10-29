namespace BacpacCompatFixer.Core;

/// <summary>
/// Options for the BacpacFixer operation.
/// </summary>
public class BacpacFixerOptions
{
    public string SourceBacpac { get; set; } = string.Empty;
    public string? BackupDir { get; set; }
    public bool NoBackup { get; set; }
}
