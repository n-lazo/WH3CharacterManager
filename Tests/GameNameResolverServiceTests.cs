using System.Text;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class GameNameResolverServiceTests
{
    [Fact]
    public void Resolve_KnownKeys_ReturnsSpanishLocalizedNames()
    {
        var service = GameNameResolverService.Instance;

        // Keys extracted from game packs
        string? name1 = service.Resolve("names_name_2147359220");
        string? name2 = service.Resolve("names_name_2147354596");
        string? name3 = service.Resolve("names_name_2147355016");

        Assert.Equal("Iarac", name1);
        Assert.Equal("Frochlichmann", name2);
        Assert.Equal("Magno", name3);
    }

    [Fact]
    public void ResolveLoreName_WithSingleNameToken_ReturnsSingleName()
    {
        var service = GameNameResolverService.Instance;
        byte[] bytes = Encoding.ASCII.GetBytes("something names_name_2147359220 other_stuff");

        string loreName = service.ResolveLoreName(bytes);
        Assert.Equal("Iarac", loreName);
    }

    [Fact]
    public void ResolveLoreName_WithForenameAndFamilyName_CombinesProperly()
    {
        var service = GameNameResolverService.Instance;
        // names_name_2147355016 is Magno (forename), names_name_2147354596 is Frochlichmann (family_name)
        byte[] bytes = Encoding.ASCII.GetBytes("family_name names_name_2147354596 other forename names_name_2147355016");

        string loreName = service.ResolveLoreName(bytes);
        Assert.Equal("Magno Frochlichmann", loreName);
    }

    [Fact]
    public void ResolveLoreName_EmptyBytes_ReturnsEmpty()
    {
        var service = GameNameResolverService.Instance;
        string loreName = service.ResolveLoreName(ReadOnlySpan<byte>.Empty);
        Assert.Equal(string.Empty, loreName);
    }
}
