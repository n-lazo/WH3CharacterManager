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
}
