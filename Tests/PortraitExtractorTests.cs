using System.IO;
using WH3CharacterManager.Services;
using Xunit;

namespace WH3CharacterManager.Tests;

public class PortraitExtractorTests
{
    [Fact]
    public void GameAssetService_ExtractsPortrait_Successfully()
    {
        var service = GameAssetService.Instance;
        string porthole = "UI/Portraits/Portholes/no_culture/emp_engineer_campaign_01_0.png";

        string? path = service.GetPortraitImagePath(porthole);
        if (path != null)
        {
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
    }

    [Fact]
    public void GameAssetService_ExtractsRaceIcons_Successfully()
    {
        var service = GameAssetService.Instance;

        string? empPath = service.GetRaceIconPath("emp");
        if (empPath != null)
        {
            Assert.True(File.Exists(empPath));
            Assert.True(new FileInfo(empPath).Length > 0);
        }

        string? wefPath = service.GetRaceIconPath("wef");
        if (wefPath != null)
        {
            Assert.True(File.Exists(wefPath));
            Assert.True(new FileInfo(wefPath).Length > 0);
        }
    }

    [Fact]
    public void GameAssetService_NullInput_ReturnsNull()
    {
        var service = GameAssetService.Instance;
        Assert.Null(service.GetPortraitImagePath(null));
        Assert.Null(service.GetRaceIconPath(null));
        Assert.Null(service.GetRaceIconPath("non_existent_race_xyz"));
    }
}
