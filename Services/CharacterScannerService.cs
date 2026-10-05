using System.IO;
using WH3CharacterManager.Models;

namespace WH3CharacterManager.Services;

public interface ICharacterScannerService
{
    Task<IReadOnlyList<CharacterFile>> ScanDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
}

public class CharacterScannerService : ICharacterScannerService
{
    public async Task<IReadOnlyList<CharacterFile>> ScanDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Array.Empty<CharacterFile>();
        }

        return await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            FileInfo[] files = dirInfo.GetFiles("*.twc", SearchOption.TopDirectoryOnly);

            var list = new List<CharacterFile>(files.Length);
            foreach (FileInfo file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CharacterMetadata? metadata = null;
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(file.FullName);
                    metadata = TwcMetadataParser.Parse(fileBytes);
                }
                catch
                {
                    // Fallback silencioso si el archivo está corrupto o es inválido
                }

                list.Add(new CharacterFile
                {
                    FilePath = file.FullName,
                    FileName = Path.GetFileNameWithoutExtension(file.Name),
                    FileSizeBytes = file.Length,
                    LastModified = file.LastWriteTime,
                    Metadata = metadata
                });
            }

            return (IReadOnlyList<CharacterFile>)list.OrderByDescending(f => f.LastModified).ToList();
        }, cancellationToken);
    }
}
