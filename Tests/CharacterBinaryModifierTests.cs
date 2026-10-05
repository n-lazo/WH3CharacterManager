using System.Buffers.Binary;
using System.Text;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class CharacterBinaryModifierTests
{
    [Fact]
    public void ReplaceIdInBytes_SameLength_ReplacesDirectlyWithoutAlteringSize()
    {
        string oldId = "a6814d3d-ac61-4877-bcfd-9866693022522827";
        string newId = "a6814d3d-ac61-4877-bcfd-9866693022522828";

        byte[] header = [0x01, 0x02, 0x03, 0x04];
        byte[] idBytes = Encoding.UTF8.GetBytes(oldId);
        byte[] footer = [0xAA, 0xBB, 0xCC];

        byte[] sample = [.. header, .. idBytes, .. footer];

        byte[] result = CharacterBinaryModifier.ReplaceIdInBytes(sample, oldId, newId);

        Assert.Equal(sample.Length, result.Length);
        Assert.Equal(header, result[..4]);
        Assert.Equal(Encoding.UTF8.GetBytes(newId), result[4..44]);
        Assert.Equal(footer, result[44..]);
    }

    [Fact]
    public void ReplaceIdInBytes_DifferentLength_Updates4BytePrefixIfPresent()
    {
        string oldId = "Tyrion";
        string newId = "Tyrion_01";

        byte[] oldIdBytes = Encoding.UTF8.GetBytes(oldId);
        byte[] newIdBytes = Encoding.UTF8.GetBytes(newId);

        // Preceding 4-byte little-endian length prefix: 6
        byte[] lengthPrefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, oldIdBytes.Length);

        byte[] header = [0x50, 0x51];
        byte[] footer = [0x99, 0x98];

        byte[] sample = [.. header, .. lengthPrefix, .. oldIdBytes, .. footer];

        byte[] result = CharacterBinaryModifier.ReplaceIdInBytes(sample, oldId, newId);

        // Expected new length: header (2) + prefix (4) + newId (9) + footer (2) = 17 bytes
        Assert.Equal(sample.Length + (newIdBytes.Length - oldIdBytes.Length), result.Length);

        // Verify header unchanged
        Assert.Equal(header, result[..2]);

        // Verify prefix updated to 9
        int updatedLength = BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(2, 4));
        Assert.Equal(newIdBytes.Length, updatedLength);

        // Verify new ID content
        Assert.Equal(newIdBytes, result[6..15]);

        // Verify footer unchanged
        Assert.Equal(footer, result[15..]);
    }

    [Fact]
    public void ReplaceIdInBytes_NoMatch_ReturnsExactCopy()
    {
        byte[] sample = [0x10, 0x20, 0x30];
        byte[] result = CharacterBinaryModifier.ReplaceIdInBytes(sample, "Missing", "Replacement");

        Assert.Equal(sample, result);
    }

    [Fact]
    public void ReplaceUtf16StringNearMarker_DifferentLength_UpdatesPrefixAndBuffer()
    {
        using var ms = new MemoryStream();
        byte[] marker = Encoding.UTF8.GetBytes("SAVED_INITIATIVE_SET_INFO");
        ms.Write(marker);
        ms.Write([0x01, 0x02]); // padding

        string oldStr = "HawkShisho";
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, oldStr.Length);
        ms.Write(prefix);
        ms.Write(Encoding.Unicode.GetBytes(oldStr));
        ms.Write([0xFF, 0xEE]); // footer

        byte[] original = ms.ToArray();
        string newStr = "SuperHeroLegend";

        byte[] modified = CharacterBinaryModifier.ReplaceUtf16StringNearMarker(
            original, "SAVED_INITIATIVE_SET_INFO", oldStr, newStr);

        var metadata = TwcMetadataParser.Parse(modified);
        Assert.Equal("SuperHeroLegend", metadata.SavedName);
    }

    [Fact]
    public void UpdateCharacterNamesInBytes_UpdatesForenameSurnameAndSavedTag()
    {
        using var ms = new MemoryStream();
        byte[] marker = Encoding.UTF8.GetBytes("SAVED_INITIATIVE_SET_INFO");
        ms.Write(marker);
        ms.Write([0x04, 0x00, 0x00, 0x00]);

        // Surname: Nitales
        string surname = "Nitales";
        byte[] len1 = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len1, surname.Length);
        ms.Write(len1);
        ms.Write(Encoding.Unicode.GetBytes(surname));

        ms.Write([0x02, 0x00, 0x00, 0x00]);

        // Forename: Jorge
        string forename = "Jorge";
        byte[] len2 = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len2, forename.Length);
        ms.Write(len2);
        ms.Write(Encoding.Unicode.GetBytes(forename));

        ms.Write([0x01, 0x00, 0x00, 0x00]);

        // Save tag: HawkShisho
        string tag = "HawkShisho";
        byte[] len3 = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len3, tag.Length);
        ms.Write(len3);
        ms.Write(Encoding.Unicode.GetBytes(tag));

        byte[] original = ms.ToArray();
        var origMetadata = TwcMetadataParser.Parse(original);

        Assert.Equal("Jorge Nitales", origMetadata.CustomName);
        Assert.Equal("HawkShisho", origMetadata.SavedName);

        byte[] updated = CharacterBinaryModifier.UpdateCharacterNamesInBytes(
            original, origMetadata, "Carlos", "Santana", "MiTagNuevo");

        var newMetadata = TwcMetadataParser.Parse(updated);
        Assert.Equal("Carlos Santana", newMetadata.CustomName);
        Assert.Equal("MiTagNuevo", newMetadata.SavedName);
    }
}
