using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WH3CharacterManager.Models;
using WH3CharacterManager.Services;

namespace WH3CharacterManager;

public partial class MainWindow : Window
{
    private readonly ICharacterScannerService _scannerService;
    private readonly ObservableCollection<CharacterFile> _characterFiles;
    private bool _isScanning;

    public MainWindow(ICharacterScannerService? scannerService = null)
    {
        InitializeComponent();
        _scannerService = scannerService ?? new CharacterScannerService();
        _characterFiles = new ObservableCollection<CharacterFile>();
        LvCharacters.ItemsSource = _characterFiles;

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
            return;
        }

        try
        {
            SetScanningState(true);
            StatusText.Text = "Escaneando personajes...";

            IReadOnlyList<CharacterFile> characters = await _scannerService.ScanDirectoryAsync(folderPath);

            _characterFiles.Clear();
            foreach (CharacterFile charFile in characters)
            {
                _characterFiles.Add(charFile);
            }

            StatusText.Text = $"Se encontraron {_characterFiles.Count} personajes.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al escanear la carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error al escanear la carpeta.";
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
        BtnDuplicateCharacter.IsEnabled = !scanning;
        BtnExportCharacter.IsEnabled = !scanning;
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

        // Refrescar la lista si se generaron nuevos personajes
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
            FileName = selectedCharacter.FileName + ".twc",
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