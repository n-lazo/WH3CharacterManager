using System.Numerics;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class CharacterIdParserTests
{
    [Fact]
    public void Parse_RealWarhammer3Id_Handles16DigitNumberWithoutOverflow()
    {
        // Real Warhammer 3 save file name (40 chars total)
        string originalId = "a6814d3d-ac61-4877-bcfd-9866693022522827";

        CharacterIdInfo info = CharacterIdParser.Parse(originalId);

        Assert.Equal("a6814d3d-ac61-4877-bcfd-", info.BaseId);
        Assert.Equal(BigInteger.Parse("9866693022522827"), info.StartNumber);
        Assert.Equal(16, info.NumberPadding);

        string next1 = info.GenerateId(1);
        string next2 = info.GenerateId(2);

        Assert.Equal("a6814d3d-ac61-4877-bcfd-9866693022522828", next1);
        Assert.Equal("a6814d3d-ac61-4877-bcfd-9866693022522829", next2);
        Assert.Equal(40, next1.Length);
    }

    [Fact]
    public void Parse_NameWithoutNumbers_AppendsUnderscoreAndNumbersFromOne()
    {
        string originalId = "Tyrion";

        CharacterIdInfo info = CharacterIdParser.Parse(originalId);

        Assert.Equal("Tyrion_", info.BaseId);
        Assert.Equal(BigInteger.Zero, info.StartNumber);
        Assert.Equal(1, info.NumberPadding);

        Assert.Equal("Tyrion_1", info.GenerateId(1));
        Assert.Equal("Tyrion_2", info.GenerateId(2));
        Assert.Equal("Tyrion_10", info.GenerateId(10));
    }

    [Fact]
    public void Parse_NameWithLeadingZeroes_PreservesPadding()
    {
        string originalId = "KarlFranz_001";

        CharacterIdInfo info = CharacterIdParser.Parse(originalId);

        Assert.Equal("KarlFranz_", info.BaseId);
        Assert.Equal(new BigInteger(1), info.StartNumber);
        Assert.Equal(3, info.NumberPadding);

        Assert.Equal("KarlFranz_002", info.GenerateId(1));
        Assert.Equal("KarlFranz_010", info.GenerateId(9));
    }

    [Fact]
    public void Parse_PureNumericName_IncrementsAccurately()
    {
        string originalId = "00500";

        CharacterIdInfo info = CharacterIdParser.Parse(originalId);

        Assert.Equal(string.Empty, info.BaseId);
        Assert.Equal(new BigInteger(500), info.StartNumber);
        Assert.Equal(5, info.NumberPadding);

        Assert.Equal("00501", info.GenerateId(1));
        Assert.Equal("00502", info.GenerateId(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_InvalidInput_ThrowsArgumentException(string invalid)
    {
        Assert.Throws<ArgumentException>(() => CharacterIdParser.Parse(invalid));
    }
}
