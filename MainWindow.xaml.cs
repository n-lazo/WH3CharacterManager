using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WH3CharacterManager.Models;
using WH3CharacterManager.Services;

namespace WH3CharacterManager;

public partial class MainWindow : Window
{
    private readonly ICharacterScannerService _scannerService;
    private readonly ObservableCollection<CharacterFile> _characterFiles;
    private ICollectionView? _charactersView;
    private bool _isScanning;
    private string _searchText = string.Empty;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(ICharacterScannerService? scannerService)
    {
        InitializeComponent();
        _scannerService = scannerService ?? new CharacterScannerService();
        _characterFiles = new ObservableCollection<CharacterFile>();
        LvCharacters.ItemsSource = _characterFiles;

        _charactersView = CollectionViewSource.GetDefaultView(_characterFiles);
        _charactersView.Filter = FilterCharacter;

        // Intentar obtener la ruta por defecto de los personajes de Warhammer 3
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string defaultPath = Path.Combine(appDataPath, "The Creative Assembly", "Warhammer3", "saved_characters");
        TxtFolderPath.Text = defaultPath;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(TxtFolderPath.Text.Trim()))
        {
            await ScanFolderAsync();
        }
        else
        {
            UpdateEmptyState();
        }
    }

    private async void BtnScanFolder_Click(object sender, RoutedEventArgs e)
    {
        await ScanFolderAsync();
    }

    private async Task ScanFolderAsync()
    {
        if (_isScanning)
            return;

        string folderPath = TxtFolderPath.Text.Trim();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show($"La carpeta '{folderPath}' no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateEmptyState();
            return;
        }

        try
        {
            SetScanningState(true);
            StatusText.Text = "Escaneando personajes y analizando atributos...";
            StatusDot.Fill = (Brush)FindResource("CyanNeonBrush");

            IReadOnlyList<CharacterFile> characters = await _scannerService.ScanDirectoryAsync(folderPath);

            _characterFiles.Clear();
            foreach (CharacterFile charFile in characters)
            {
                _characterFiles.Add(charFile);
            }

            ApplyFilter();
            UpdateCounters();
            UpdateEmptyState();

            StatusText.Text = $"Se encontraron {_characterFiles.Count} personajes.";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde éxito
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al escanear la carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error al escanear la carpeta.";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Rojo error
            UpdateEmptyState();
        }
        finally
        {
            SetScanningState(false);
        }
    }

    private void SetScanningState(bool scanning)
    {
        _isScanning = scanning;
        BtnScanFolder.IsEnabled = !scanning;
        BtnBrowseFolder.IsEnabled = !scanning;
        BtnDuplicateCharacter.IsEnabled = !scanning && LvCharacters.SelectedItem is CharacterFile;
        BtnExportCharacter.IsEnabled = !scanning && LvCharacters.SelectedItem is CharacterFile;
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecciona la carpeta de personajes guardados de Warhammer 3",
            InitialDirectory = Directory.Exists(TxtFolderPath.Text.Trim()) ? TxtFolderPath.Text.Trim() : string.Empty
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtFolderPath.Text = dialog.FolderName;
            _ = ScanFolderAsync();
        }
    }

    private void BtnOpenFolderInExplorer_Click(object sender, RoutedEventArgs e)
    {
        string folderPath = TxtFolderPath.Text.Trim();
        if (Directory.Exists(folderPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el explorador: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show("La carpeta no existe o no ha sido configurada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TxtSearchFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = TxtSearchFilter.Text.Trim();
        BtnClearSearch.Visibility = string.IsNullOrEmpty(_searchText) ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtSearchFilter.Text = string.Empty;
    }

    private bool FilterCharacter(object item)
    {
        if (item is not CharacterFile character)
            return false;

        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        return character.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               character.LoreNameDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               character.SavedNameDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               character.FileName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               character.HeroClassDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               character.RaceDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
               character.TraitDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        _charactersView?.Refresh();
        UpdateCounters();
        UpdateEmptyState();
    }

    private void UpdateCounters()
    {
        int total = _characterFiles.Count;
        TxtCharacterCountBadge.Text = $"{total} {(total == 1 ? "Personaje" : "Personajes")}";

        if (_charactersView != null && !string.IsNullOrWhiteSpace(_searchText))
        {
            int filtered = _charactersView.Cast<object>().Count();
            TxtFilterStatus.Text = $"{filtered} de {total}";
        }
        else
        {
            TxtFilterStatus.Text = string.Empty;
        }
    }

    private void UpdateEmptyState()
    {
        bool hasVisibleItems = _charactersView != null && _charactersView.Cast<object>().Any();
        EmptyStateOverlay.Visibility = hasVisibleItems ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LvCharacters_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LvCharacters.SelectedItem is CharacterFile selected)
        {
            BtnDuplicateCharacter.IsEnabled = true;
            BtnExportCharacter.IsEnabled = true;

            TxtSelectedName.Text = selected.DisplayName;

            if (!string.IsNullOrWhiteSpace(selected.Metadata?.SavedName))
            {
                TxtSelectedSavedTag.Text = $"Etiqueta de guardado: {selected.Metadata.SavedName}";
                TxtSelectedSavedTag.Visibility = Visibility.Visible;
            }
            else
            {
                TxtSelectedSavedTag.Visibility = Visibility.Collapsed;
            }

            TxtSelectedClass.Text = selected.HeroClassDisplay;
            TxtSelectedRace.Text = selected.RaceDisplay;
            TxtSelectedLevel.Text = selected.LevelDisplay;
            TxtSelectedTrait.Text = selected.TraitDisplay;

            if (selected.Metadata?.Skills.Count > 0)
            {
                TxtSelectedSkills.Text = string.Join(", ", selected.Metadata.Skills);
            }
            else
            {
                TxtSelectedSkills.Text = "Habilidades base de campaña";
            }

            TxtSelectedFileName.Text = selected.FileName + ".twc";

            CardSelectedInfo.Visibility = Visibility.Visible;
            CardNoSelection.Visibility = Visibility.Collapsed;
        }
        else
        {
            BtnDuplicateCharacter.IsEnabled = false;
            BtnExportCharacter.IsEnabled = false;

            CardSelectedInfo.Visibility = Visibility.Collapsed;
            CardNoSelection.Visibility = Visibility.Visible;
        }
    }

    private async void BtnDuplicateCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (LvCharacters.SelectedItem is not CharacterFile selectedCharacter)
        {
            MessageBox.Show("Por favor, selecciona un personaje para duplicar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var duplicateWindow = new DuplicateWindow(selectedCharacter)
        {
            Owner = this
        };

        duplicateWindow.ShowDialog();

        if (duplicateWindow.CharactersGenerated > 0)
        {
            await ScanFolderAsync();
        }
    }

    private void LvCharacters_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LvCharacters.SelectedItem is CharacterFile)
        {
            BtnDuplicateCharacter_Click(sender, e);
        }
    }

    private void BtnExportCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (LvCharacters.SelectedItem is not CharacterFile selectedCharacter)
        {
            MessageBox.Show("Por favor, selecciona un personaje para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            FileName = selectedCharacter.DisplayName + ".twc",
            Filter = "Warhammer Character Files (*.twc)|*.twc",
            Title = "Exportar personaje"
        };

        if (saveDialog.ShowDialog(this) == true)
        {
            try
            {
                File.Copy(selectedCharacter.FilePath, saveDialog.FileName, overwrite: true);
                MessageBox.Show("Personaje exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}