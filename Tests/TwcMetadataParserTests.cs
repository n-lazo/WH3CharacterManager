using System.Buffers.Binary;
using System.Text;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class TwcMetadataParserTests
{
    [Fact]
    public void Parse_EmptyBytes_ReturnsSafeDefaults()
    {
        byte[] empty = [];
        var metadata = TwcMetadataParser.Parse(empty);

        Assert.NotNull(metadata);
        Assert.Equal(string.Empty, metadata.SavedName);
        Assert.Equal("Héroe / Comandante", metadata.HeroClass);
        Assert.Equal("Desconocido", metadata.Race);
        Assert.Equal("Héroe", metadata.Role);
        Assert.Equal("Sin rasgo innato", metadata.Trait);
        Assert.Equal(1, metadata.Level);
        Assert.Empty(metadata.Skills);
    }

    [Fact]
    public void Parse_WithInitiativeMarkerAndUtf16Name_ExtractsSavedName()
    {
        using var ms = new MemoryStream();
        byte[] marker = Encoding.ASCII.GetBytes("SAVED_INITIATIVE_SET_INFO");
        ms.Write(marker);
        ms.Write(new byte[] { 0x01, 0x02, 0x03, 0x04 }); // padding

        string name = "HawkShisho";
        byte[] nameUtf16 = Encoding.Unicode.GetBytes(name);
        byte[] lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, name.Length);

        ms.Write(lenBytes);
        ms.Write(nameUtf16);

        var metadata = TwcMetadataParser.Parse(ms.ToArray());
        Assert.Equal("HawkShisho", metadata.SavedName);
    }

    [Fact]
    public void Parse_WithSubtypeAndCulture_ResolvesClassAndRace()
    {
        string content = "filler_text wh2_twa02_wef_glade_captain more_filler";
        byte[] bytes = Encoding.ASCII.GetBytes(content);

        var metadata = TwcMetadataParser.Parse(bytes);

        Assert.Equal("Glade Captain (Capitana del Claro)", metadata.HeroClass);
        Assert.Equal("Elfos Silvanos", metadata.Race);
        Assert.Equal("Héroe", metadata.Role);
    }

    [Fact]
    public void Parse_WithLordSubtype_ResolvesLordRole()
    {
        string content = "filler wh_main_emp_general_lord and_more";
        byte[] bytes = Encoding.ASCII.GetBytes(content);

        var metadata = TwcMetadataParser.Parse(bytes);

        Assert.Equal("Lord / Comandante", metadata.Role);
        Assert.Equal("El Imperio", metadata.Race);
    }

    [Fact]
    public void Parse_WithInnateTraitAndSkills_ExtractsTraitAndCalculatesLevel()
    {
        string content = "wh2_twa02_wef_glade_captain wh2_main_skill_innate_wef_talon_of_kurnous wh2_main_skill_wef_arrow_of_kurnous wh2_main_skill_wef_evasion";
        byte[] bytes = Encoding.ASCII.GetBytes(content);

        var metadata = TwcMetadataParser.Parse(bytes);

        Assert.Equal("Talon Of Kurnous", metadata.Trait);
        Assert.Equal(2, metadata.Skills.Count);
        // Level is 1 + skills.Count = 1 + 2 = 3
        Assert.Equal(3, metadata.Level);
    }
}
