using System.Text;
using WH3CharacterManager.Models;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class CharacterDuplicationServiceTests : IDisposable
{
    private readonly string _testDir;

    public CharacterDuplicationServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "WH3Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in temp
            }
        }
    }

    [Fact]
    public async Task DuplicateCharacterAsync_GeneratesRequestedCopiesWithCorrectNamesAndContent()
    {
        string originalFileName = "Lord_001";
        string sourceFilePath = Path.Combine(_testDir, originalFileName + ".twc");
        string originalContent = $"DATA_START_{originalFileName}_DATA_END";
        await File.WriteAllBytesAsync(sourceFilePath, Encoding.UTF8.GetBytes(originalContent));

        var charFile = new CharacterFile
        {
            FileName = originalFileName,
            FilePath = sourceFilePath,
            FileSizeBytes = originalContent.Length,
            LastModified = DateTime.UtcNow
        };

        var service = new CharacterDuplicationService();
        var progressList = new List<int>();
        var progress = new Progress<int>(v => progressList.Add(v));

        int generated = await service.DuplicateCharacterAsync(charFile, _testDir, 3, progress);

        Assert.Equal(3, generated);

        // Verify generated files exist
        string file1 = Path.Combine(_testDir, "Lord_002.twc");
        string file2 = Path.Combine(_testDir, "Lord_003.twc");
        string file3 = Path.Combine(_testDir, "Lord_004.twc");

        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));
        Assert.True(File.Exists(file3));

        string content1 = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file1));
        Assert.Equal("DATA_START_Lord_002_DATA_END", content1);
    }

    [Fact]
    public async Task DuplicateCharacterAsync_SupportsCancellation()
    {
        string originalFileName = "Mage_01";
        string sourceFilePath = Path.Combine(_testDir, originalFileName + ".twc");
        await File.WriteAllBytesAsync(sourceFilePath, Encoding.UTF8.GetBytes(originalFileName));

        var charFile = new CharacterFile
        {
            FileName = originalFileName,
            FilePath = sourceFilePath,
            FileSizeBytes = 7,
            LastModified = DateTime.UtcNow
        };

        var service = new CharacterDuplicationService();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.DuplicateCharacterAsync(charFile, _testDir, 10, cancellationToken: cts.Token);
        });
    }
}
