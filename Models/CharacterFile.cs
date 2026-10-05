namespace WH3CharacterManager.Models;

public class CharacterMetadata
{
    public string SavedName { get; init; } = string.Empty;
    public string HeroClass { get; init; } = "Desconocido";
    public string Race { get; init; } = "Desconocido";
    public string Role { get; init; } = "Héroe";
    public string Trait { get; init; } = "Ninguno";
    public string Faction { get; init; } = string.Empty;
    public int Level { get; init; } = 1;
    public IReadOnlyList<string> Skills { get; init; } = Array.Empty<string>();
    public string SubtypeRaw { get; init; } = string.Empty;
}

public class CharacterFile
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime LastModified { get; init; }
    public CharacterMetadata? Metadata { get; set; }

    public string DisplayName => !string.IsNullOrWhiteSpace(Metadata?.SavedName)
        ? Metadata.SavedName
        : FileName;

    public string HeroClassDisplay => Metadata?.HeroClass ?? "Héroe / Comandante";
    public string RaceDisplay => Metadata?.Race ?? "Desconocido";
    public string LevelDisplay => Metadata != null ? $"Nv. {Metadata.Level}" : "-";
    public string TraitDisplay => Metadata?.Trait ?? "-";
    public string RoleDisplay => Metadata?.Role ?? "Héroe";

    public string FormattedFileSize => FormatBytes(FileSizeBytes);
    public string FormattedLastModified => LastModified.ToString("yyyy-MM-dd HH:mm");

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
