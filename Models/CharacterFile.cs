namespace WH3CharacterManager.Models;

public class CharacterFile
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime LastModified { get; init; }

    public string FormattedFileSize => FormatBytes(FileSizeBytes);
    public string FormattedLastModified => LastModified.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
