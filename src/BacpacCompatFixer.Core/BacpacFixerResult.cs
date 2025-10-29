namespace BacpacCompatFixer.Core;

/// <summary>
/// Result of the BacpacFixer operation.
/// </summary>
public class BacpacFixerResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool Changed { get; set; }
    public string? BackupPath { get; set; }
    public string? ModelHash { get; set; }
}
