using WH3CharacterManager.Models;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class CharacterScannerServiceTests : IDisposable
{
    private readonly string _testDir;

    public CharacterScannerServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "WH3ScanTest_" + Guid.NewGuid().ToString("N"));
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
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ScanDirectoryAsync_OnlyReturnsTwcFiles_OrderedByModifiedDesc()
    {
        string twc1 = Path.Combine(_testDir, "hero1.twc");
        string twc2 = Path.Combine(_testDir, "hero2.twc");
        string txt = Path.Combine(_testDir, "notes.txt");
        string bak = Path.Combine(_testDir, "hero1.twc.bak");

        await File.WriteAllTextAsync(twc1, "content1");
        await File.WriteAllTextAsync(twc2, "content2");
        await File.WriteAllTextAsync(txt, "text");
        await File.WriteAllTextAsync(bak, "backup");

        File.SetLastWriteTime(twc1, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTime(twc2, DateTime.UtcNow);

        var scanner = new CharacterScannerService();
        IReadOnlyList<CharacterFile> results = await scanner.ScanDirectoryAsync(_testDir);

        Assert.Equal(2, results.Count);
        Assert.Equal("hero2", results[0].FileName);
        Assert.Equal("hero1", results[1].FileName);
    }

    [Fact]
    public async Task ScanDirectoryAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        var scanner = new CharacterScannerService();
        IReadOnlyList<CharacterFile> results = await scanner.ScanDirectoryAsync(Path.Combine(_testDir, "does_not_exist"));

        Assert.Empty(results);
    }
}
