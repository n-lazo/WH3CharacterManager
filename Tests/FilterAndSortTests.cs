using System.ComponentModel;
using WH3CharacterManager.Models;
using Xunit;

namespace WH3CharacterManager.Tests;

public class FilterAndSortTests
{
    [Fact]
    public void RaceFilterItem_NotifiesPropertyChanged_WhenIsSelectedChanges()
    {
        var item = new RaceFilterItem
        {
            RaceKey = "wef",
            DisplayName = "Elfos Silvanos",
            Count = 5
        };

        string? changedProp = null;
        item.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        item.IsSelected = true;

        Assert.Equal(nameof(RaceFilterItem.IsSelected), changedProp);
        Assert.True(item.IsSelected);
    }

    [Fact]
    public void RaceFilterItem_NotifiesPropertyChanged_WhenCountChanges()
    {
        var item = new RaceFilterItem
        {
            RaceKey = "ALL",
            DisplayName = "Todas las razas",
            Count = 10
        };

        string? changedProp = null;
        item.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        item.Count = 15;

        Assert.Equal(nameof(RaceFilterItem.Count), changedProp);
        Assert.Equal(15, item.Count);
    }

    [Fact]
    public void CharacterFile_NumericLevel_OrdersCorrectlyInsteadOfAlphabetically()
    {
        var char1 = new CharacterFile
        {
            FileName = "char1",
            FilePath = "c:\\char1.twc",
            Metadata = new CharacterMetadata { Level = 2 }
        };

        var char2 = new CharacterFile
        {
            FileName = "char2",
            FilePath = "c:\\char2.twc",
            Metadata = new CharacterMetadata { Level = 10 }
        };

        var char3 = new CharacterFile
        {
            FileName = "char3",
            FilePath = "c:\\char3.twc",
            Metadata = new CharacterMetadata { Level = 1 }
        };

        var list = new List<CharacterFile> { char1, char2, char3 };

        // Ordering by numeric Level descending
        var sorted = list.OrderByDescending(c => c.Level).ToList();

        Assert.Equal(10, sorted[0].Level);
        Assert.Equal(2, sorted[1].Level);
        Assert.Equal(1, sorted[2].Level);
    }

    [Fact]
    public void CharacterFile_RoleClassification_IdentifiesLordsAndHeroesCorrectly()
    {
        var lord = new CharacterFile
        {
            FileName = "general_01",
            FilePath = "c:\\general_01.twc",
            Metadata = new CharacterMetadata { Role = "Lord / Comandante" }
        };

        var hero = new CharacterFile
        {
            FileName = "mage_01",
            FilePath = "c:\\mage_01.twc",
            Metadata = new CharacterMetadata { Role = "Héroe" }
        };

        bool isLord1 = lord.RoleDisplay.Contains("Lord", StringComparison.OrdinalIgnoreCase) ||
                       lord.RoleDisplay.Contains("Comandante", StringComparison.OrdinalIgnoreCase);
        bool isLord2 = hero.RoleDisplay.Contains("Lord", StringComparison.OrdinalIgnoreCase) ||
                       hero.RoleDisplay.Contains("Comandante", StringComparison.OrdinalIgnoreCase);

        Assert.True(isLord1);
        Assert.False(isLord2);
    }

    [Fact]
    public void MainWindow_Initialization_DoesNotThrow()
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new App();
                    app.InitializeComponent();
                }
                var window = new MainWindow();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(thrown);
    }
}
