using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WH3CharacterManager.Models;

public class RaceFilterItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private int _count;

    public string RaceKey { get; init; } = "ALL";
    public string DisplayName { get; init; } = "Todas las razas";
    public string? IconPath { get; init; }
    public bool HasIcon => !string.IsNullOrEmpty(IconPath) && System.IO.File.Exists(IconPath);

    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
