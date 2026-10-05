using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    private bool _isSyncingSelection;
    private DispatcherTimer? _toastTimer;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(ICharacterScannerService? scannerService)
    {
        InitializeComponent();
        _scannerService = scannerService ?? new CharacterScannerService();
        _characterFiles = new ObservableCollection<CharacterFile>();
        
        LvCharacters.ItemsSource = _characterFiles;
        LbGallery.ItemsSource = _characterFiles;

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

    #region Window TitleBar Controls & Keyboard Shortcuts

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (IconMaximizePath != null)
        {
            IconMaximizePath.Data = (Geometry)FindResource(
                WindowState == WindowState.Maximized ? "IconRestoreGeometry" : "IconMaximizeGeometry");
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 || (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control))
        {
            _ = ScanFolderAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TxtSearchFilter.Focus();
            TxtSearchFilter.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && TxtSearchFilter.IsFocused)
        {
            TxtSearchFilter.Text = string.Empty;
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && !TxtSearchFilter.IsFocused && !TxtFolderPath.IsFocused)
        {
            if (GetSelectedCharacter() != null)
            {
                DeleteSelectedCharacter();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter && !TxtSearchFilter.IsFocused && !TxtFolderPath.IsFocused)
        {
            if (GetSelectedCharacter() != null)
            {
                BtnDuplicateCharacter_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }

    #endregion

    #region View Mode Switcher (Table vs Gallery)

    private void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        bool isGallery = RbViewGallery.IsChecked == true;

        if (isGallery)
        {
            LvCharacters.Visibility = Visibility.Collapsed;
            LbGallery.Visibility = Visibility.Visible;
            if (LbGallery.SelectedItem != null)
            {
                LbGallery.ScrollIntoView(LbGallery.SelectedItem);
            }
        }
        else
        {
            LbGallery.Visibility = Visibility.Collapsed;
            LvCharacters.Visibility = Visibility.Visible;
            if (LvCharacters.SelectedItem != null)
            {
                LvCharacters.ScrollIntoView(LvCharacters.SelectedItem);
            }
        }
    }

    #endregion

    #region Scanning & Data

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
            ShowToast($"✓ Escaneo completado: {_characterFiles.Count} personajes encontrados.");
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
        bool hasSelection = GetSelectedCharacter() != null;
        BtnDuplicateCharacter.IsEnabled = !scanning && hasSelection;
        BtnExportCharacter.IsEnabled = !scanning && hasSelection;
        BtnDeleteCharacter.IsEnabled = !scanning && hasSelection;
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

    #endregion

    #region Filtering

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
               character.CustomNameDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
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

    #endregion

    #region Selection & Character Details

    public CharacterFile? GetSelectedCharacter()
    {
        if (RbViewGallery?.IsChecked == true)
            return LbGallery.SelectedItem as CharacterFile;
        return LvCharacters.SelectedItem as CharacterFile;
    }

    private void LvCharacters_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection) return;
        _isSyncingSelection = true;
        try
        {
            var selected = LvCharacters.SelectedItem as CharacterFile;
            if (LbGallery.SelectedItem != selected)
            {
                LbGallery.SelectedItem = selected;
            }
            UpdateCharacterSelection(selected);
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void LbGallery_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection) return;
        _isSyncingSelection = true;
        try
        {
            var selected = LbGallery.SelectedItem as CharacterFile;
            if (LvCharacters.SelectedItem != selected)
            {
                LvCharacters.SelectedItem = selected;
            }
            UpdateCharacterSelection(selected);
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void UpdateCharacterSelection(CharacterFile? selected)
    {
        if (selected != null)
        {
            BtnDuplicateCharacter.IsEnabled = !_isScanning;
            BtnExportCharacter.IsEnabled = !_isScanning;
            BtnDeleteCharacter.IsEnabled = !_isScanning;

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

            if (selected.HasPortraitImage)
            {
                try
                {
                    ImgSelectedPortrait.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(selected.PortraitImagePath!));
                    ImgSelectedPortrait.Visibility = Visibility.Visible;
                }
                catch
                {
                    ImgSelectedPortrait.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ImgSelectedPortrait.Visibility = Visibility.Collapsed;
            }

            if (selected.HasRaceIcon)
            {
                try
                {
                    ImgSelectedRace.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(selected.RaceIconPath!));
                    ImgSelectedRace.Visibility = Visibility.Visible;
                }
                catch
                {
                    ImgSelectedRace.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ImgSelectedRace.Visibility = Visibility.Collapsed;
            }

            TxtSelectedClass.Text = selected.HeroClassDisplay;
            TxtSelectedRace.Text = selected.RaceDisplay;
            TxtSelectedLevel.Text = selected.LevelDisplay;
            TxtSelectedTrait.Text = selected.TraitDisplay;

            // Mostrar habilidades con badges o mensaje por defecto
            if (selected.Metadata?.Skills.Count > 0)
            {
                IcSelectedSkills.ItemsSource = selected.Metadata.Skills;
                IcSelectedSkills.Visibility = Visibility.Visible;
                TxtNoSkills.Visibility = Visibility.Collapsed;
            }
            else
            {
                IcSelectedSkills.ItemsSource = null;
                IcSelectedSkills.Visibility = Visibility.Collapsed;
                TxtNoSkills.Visibility = Visibility.Visible;
            }

            TxtSelectedFileName.Text = selected.FileName + ".twc";

            CardSelectedInfo.Visibility = Visibility.Visible;
            CardNoSelection.Visibility = Visibility.Collapsed;
        }
        else
        {
            BtnDuplicateCharacter.IsEnabled = false;
            BtnExportCharacter.IsEnabled = false;
            BtnDeleteCharacter.IsEnabled = false;

            CardSelectedInfo.Visibility = Visibility.Collapsed;
            CardNoSelection.Visibility = Visibility.Visible;
        }
    }

    private void LvCharacters_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetSelectedCharacter() != null)
        {
            BtnDuplicateCharacter_Click(sender, e);
        }
    }

    private void LbGallery_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetSelectedCharacter() != null)
        {
            BtnDuplicateCharacter_Click(sender, e);
        }
    }

    private void OnItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem lvi)
        {
            lvi.IsSelected = true;
            lvi.Focus();
        }
        else if (sender is ListBoxItem lbi)
        {
            lbi.IsSelected = true;
            lbi.Focus();
        }
    }

    #endregion

    #region Character Actions (Duplicate, Export, Locate, Delete)

    private async void BtnDuplicateCharacter_Click(object sender, RoutedEventArgs e)
    {
        CharacterFile? selectedCharacter = GetSelectedCharacter();
        if (selectedCharacter == null)
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
            ShowToast($"✓ Se generaron {duplicateWindow.CharactersGenerated} copias exitosamente.");
        }
    }

    private void BtnExportCharacter_Click(object sender, RoutedEventArgs e)
    {
        CharacterFile? selectedCharacter = GetSelectedCharacter();
        if (selectedCharacter == null)
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
                ShowToast($"✓ Personaje '{selectedCharacter.DisplayName}' exportado correctamente.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnOpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        CharacterFile? selected = GetSelectedCharacter();
        if (selected != null && File.Exists(selected.FilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{selected.FilePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el explorador: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnDeleteCharacter_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedCharacter();
    }

    private void DeleteSelectedCharacter()
    {
        CharacterFile? selected = GetSelectedCharacter();
        if (selected == null)
            return;

        var result = MessageBox.Show(
            $"¿Estás seguro de que deseas eliminar el personaje '{selected.DisplayName}'?\n\nArchivo: {selected.FileName}.twc\nEsta acción no se puede deshacer.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                if (File.Exists(selected.FilePath))
                {
                    File.Delete(selected.FilePath);
                }

                _characterFiles.Remove(selected);
                ApplyFilter();
                UpdateCharacterSelection(null);
                ShowToast($"✓ Personaje '{selected.DisplayName}' eliminado correctamente.", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar el archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Context Menu Handlers

    private void CtxDuplicate_Click(object sender, RoutedEventArgs e)
    {
        BtnDuplicateCharacter_Click(sender, e);
    }

    private void CtxExport_Click(object sender, RoutedEventArgs e)
    {
        BtnExportCharacter_Click(sender, e);
    }

    private void CtxShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        BtnOpenFileLocation_Click(sender, e);
    }

    private void CtxCopyName_Click(object sender, RoutedEventArgs e)
    {
        CharacterFile? selected = GetSelectedCharacter();
        if (selected != null)
        {
            Clipboard.SetText(selected.DisplayName);
            ShowToast($"✓ Nombre copiado: \"{selected.DisplayName}\"");
        }
    }

    private void CtxDeleteCharacter_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedCharacter();
    }

    #endregion

    #region In-App Toast Notification

    public void ShowToast(string message, bool isSuccess = true)
    {
        TxtToastMessage.Text = message;
        ToastIcon.Data = (Geometry)FindResource(isSuccess ? "IconCheckGeometry" : "IconInfoGeometry");
        ToastIcon.Fill = (Brush)FindResource(isSuccess ? "CyanNeonBrush" : "TextSecondaryBrush");
        ToastNotification.BorderBrush = (Brush)FindResource(isSuccess ? "CyanNeonBrush" : "BorderSubtleBrush");

        ToastNotification.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s2, e2) => ToastNotification.Visibility = Visibility.Collapsed;
            ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        };
        _toastTimer.Start();
    }

    #endregion
}