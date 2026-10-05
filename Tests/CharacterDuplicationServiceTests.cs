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

    [Fact]
    public async Task DuplicateCharacterAsync_WithCustomNamingPatterns_GeneratesDistinctNamesInFiles()
    {
        string originalFileName = "Alith_01";
        string sourceFilePath = Path.Combine(_testDir, originalFileName + ".twc");

        using var ms = new MemoryStream();
        ms.Write(Encoding.UTF8.GetBytes("HEAD_"));
        ms.Write(Encoding.UTF8.GetBytes(originalFileName));
        ms.Write(Encoding.UTF8.GetBytes("_SAVED_INITIATIVE_SET_INFO"));
        ms.Write([0x01, 0x00, 0x00, 0x00]);

        string surname = "Anar";
        byte[] len1 = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(len1, surname.Length);
        ms.Write(len1);
        ms.Write(Encoding.Unicode.GetBytes(surname));

        ms.Write([0x02, 0x00, 0x00, 0x00]);

        string forename = "Alith";
        byte[] len2 = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(len2, forename.Length);
        ms.Write(len2);
        ms.Write(Encoding.Unicode.GetBytes(forename));

        ms.Write([0x03, 0x00, 0x00, 0x00]);

        string tag = "ShadowKing";
        byte[] len3 = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(len3, tag.Length);
        ms.Write(len3);
        ms.Write(Encoding.Unicode.GetBytes(tag));

        byte[] raw = ms.ToArray();
        await File.WriteAllBytesAsync(sourceFilePath, raw);

        var metadata = TwcMetadataParser.Parse(raw);
        var charFile = new CharacterFile
        {
            FileName = originalFileName,
            FilePath = sourceFilePath,
            FileSizeBytes = raw.Length,
            LastModified = DateTime.UtcNow,
            Metadata = metadata
        };

        var service = new CharacterDuplicationService();
        int generated = await service.DuplicateCharacterAsync(
            charFile, _testDir, 2, 
            namePattern: "{name} [{n}]", 
            tagPattern: "{tag}_{n}");

        Assert.Equal(2, generated);

        string copyPath1 = Path.Combine(_testDir, "Alith_02.twc");
        string copyPath2 = Path.Combine(_testDir, "Alith_03.twc");

        Assert.True(File.Exists(copyPath1));
        Assert.True(File.Exists(copyPath2));

        var copy1Meta = TwcMetadataParser.Parse(await File.ReadAllBytesAsync(copyPath1));
        var copy2Meta = TwcMetadataParser.Parse(await File.ReadAllBytesAsync(copyPath2));

        Assert.Equal("Alith [1] Anar", copy1Meta.CustomName);
        Assert.Equal("ShadowKing_1", copy1Meta.SavedName);

        Assert.Equal("Alith [2] Anar", copy2Meta.CustomName);
        Assert.Equal("ShadowKing_2", copy2Meta.SavedName);
    }
}
