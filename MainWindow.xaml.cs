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
    private readonly ObservableCollection<RaceFilterItem> _raceFilters;
    public ObservableCollection<RaceFilterItem> RaceFilters => _raceFilters;
    private ICollectionView? _charactersView;
    private bool _isScanning;
    private string _searchText = string.Empty;
    private string _selectedRaceFilter = "ALL";
    private enum RoleFilterMode { All, LordsOnly, HeroesOnly }
    private RoleFilterMode _selectedRoleFilter = RoleFilterMode.All;
    private string _currentSortProperty = "DisplayName";
    private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;
    private bool _isSyncingSort;
    private bool _isSyncingSelection;
    private bool _isInitialized;
    private DispatcherTimer? _toastTimer;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(ICharacterScannerService? scannerService)
    {
        _scannerService = scannerService ?? new CharacterScannerService();
        _characterFiles = new ObservableCollection<CharacterFile>();
        _raceFilters = new ObservableCollection<RaceFilterItem>();

        InitializeComponent();

        LvCharacters.ItemsSource = _characterFiles;
        LbGallery.ItemsSource = _characterFiles;
        IcRaceFilterChips.ItemsSource = _raceFilters;

        _charactersView = CollectionViewSource.GetDefaultView(_characterFiles);
        _charactersView.Filter = FilterCharacter;

        // Intentar obtener la ruta por defecto de los personajes de Warhammer 3
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string defaultPath = Path.Combine(appDataPath, "The Creative Assembly", "Warhammer3", "saved_characters");
        TxtFolderPath.Text = defaultPath;

        _isInitialized = true;
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
        else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (GetSelectedCharacter() != null)
            {
                BtnEditCharacter_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
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

            UpdateRaceChips();
            ApplySort(_currentSortProperty, _currentSortDirection);
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
        BtnEditCharacter.IsEnabled = !scanning && hasSelection;
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

    #region Filtering & Sorting

    private void TxtSearchFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        _searchText = TxtSearchFilter.Text.Trim();
        BtnClearSearch.Visibility = string.IsNullOrEmpty(_searchText) ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtSearchFilter.Text = string.Empty;
    }

    private void CbRoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || CbRoleFilter == null) return;
        _selectedRoleFilter = CbRoleFilter.SelectedIndex switch
        {
            1 => RoleFilterMode.LordsOnly,
            2 => RoleFilterMode.HeroesOnly,
            _ => RoleFilterMode.All
        };
        ApplyFilter();
    }

    private bool FilterCharacter(object item)
    {
        if (item is not CharacterFile character)
            return false;

        // 1. Filtro por texto de búsqueda
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            bool matches = character.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.CustomNameDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.LoreNameDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.SavedNameDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.FileName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.HeroClassDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.RaceDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                           character.TraitDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

            if (!matches)
                return false;
        }

        // 2. Filtro por Raza (Crests Chips)
        if (!string.IsNullOrEmpty(_selectedRaceFilter) && _selectedRaceFilter != "ALL")
        {
            if (!string.Equals(character.RaceDisplay, _selectedRaceFilter, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 3. Filtro por Rol (Lords vs Héroes)
        if (_selectedRoleFilter == RoleFilterMode.LordsOnly)
        {
            if (!character.RoleDisplay.Contains("Lord", StringComparison.OrdinalIgnoreCase) &&
                !character.RoleDisplay.Contains("Comandante", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        else if (_selectedRoleFilter == RoleFilterMode.HeroesOnly)
        {
            if (character.RoleDisplay.Contains("Lord", StringComparison.OrdinalIgnoreCase) ||
                character.RoleDisplay.Contains("Comandante", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void ApplyFilter()
    {
        if (!_isInitialized) return;
        _charactersView?.Refresh();
        UpdateCounters();
        UpdateEmptyState();
    }

    private void UpdateCounters()
    {
        if (!_isInitialized || TxtCharacterCountBadge == null) return;
        int total = _characterFiles.Count;
        TxtCharacterCountBadge.Text = $"{total} {(total == 1 ? "Personaje" : "Personajes")}";

        bool hasFilter = !string.IsNullOrWhiteSpace(_searchText) ||
                         (!string.IsNullOrEmpty(_selectedRaceFilter) && _selectedRaceFilter != "ALL") ||
                         _selectedRoleFilter != RoleFilterMode.All;

        if (_charactersView != null && hasFilter)
        {
            int filtered = _charactersView.Cast<object>().Count();
            if (TxtFilterStatus != null)
                TxtFilterStatus.Text = $"{filtered} de {total}";
        }
        else
        {
            if (TxtFilterStatus != null)
                TxtFilterStatus.Text = string.Empty;
        }
    }

    private void UpdateEmptyState()
    {
        if (!_isInitialized || EmptyStateOverlay == null) return;
        bool hasVisibleItems = _charactersView != null && _charactersView.Cast<object>().Any();
        EmptyStateOverlay.Visibility = hasVisibleItems ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateRaceChips()
    {
        string previousSelected = _selectedRaceFilter;
        _raceFilters.Clear();

        int total = _characterFiles.Count;
        var allChip = new RaceFilterItem
        {
            RaceKey = "ALL",
            DisplayName = "Todas las razas",
            IconPath = null,
            Count = total,
            IsSelected = string.IsNullOrEmpty(previousSelected) || previousSelected == "ALL"
        };
        _raceFilters.Add(allChip);

        var groups = _characterFiles
            .GroupBy(c => c.RaceDisplay)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

        bool foundSelected = allChip.IsSelected;

        foreach (var group in groups)
        {
            string raceName = group.Key;
            string? icon = group.FirstOrDefault(c => c.HasRaceIcon)?.RaceIconPath;
            bool isSel = string.Equals(previousSelected, raceName, StringComparison.OrdinalIgnoreCase);
            if (isSel) foundSelected = true;

            _raceFilters.Add(new RaceFilterItem
            {
                RaceKey = raceName,
                DisplayName = raceName,
                IconPath = icon,
                Count = group.Count(),
                IsSelected = isSel
            });
        }

        if (!foundSelected)
        {
            _selectedRaceFilter = "ALL";
            allChip.IsSelected = true;
        }
    }

    private void RaceChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RaceFilterItem item)
        {
            SelectRaceFilter(item);
        }
    }

    private void SelectRaceFilter(RaceFilterItem item)
    {
        if (item.RaceKey == "ALL" || (item.IsSelected && _selectedRaceFilter == item.RaceKey))
        {
            _selectedRaceFilter = "ALL";
        }
        else
        {
            _selectedRaceFilter = item.RaceKey;
        }

        foreach (var chip in _raceFilters)
        {
            chip.IsSelected = chip.RaceKey.Equals(_selectedRaceFilter, StringComparison.OrdinalIgnoreCase);
        }

        ApplyFilter();
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
        {
            string? prop = null;
            if (header.Column == ColHeaderPersonaje) prop = "DisplayName";
            else if (header.Column == ColHeaderClase) prop = "HeroClassDisplay";
            else if (header.Column == ColHeaderRaza) prop = "RaceDisplay";
            else if (header.Column == ColHeaderNivel) prop = "Level";
            else if (header.Column == ColHeaderRasgo) prop = "TraitDisplay";
            else if (header.Column == ColHeaderTamano) prop = "FileSizeBytes";
            else if (header.Column == ColHeaderModificado) prop = "LastModified";

            if (string.IsNullOrEmpty(prop))
                return;

            ListSortDirection newDir;
            if (_currentSortProperty == prop)
            {
                newDir = _currentSortDirection == ListSortDirection.Ascending 
                    ? ListSortDirection.Descending 
                    : ListSortDirection.Ascending;
            }
            else
            {
                newDir = (prop is "Level" or "LastModified" or "FileSizeBytes")
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            ApplySort(prop, newDir);
        }
    }

    private void CbSortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isSyncingSort || CbSortSelector == null || CbSortSelector.SelectedItem is not ComboBoxItem selectedItem)
            return;

        string? prop = selectedItem.Tag as string;
        if (!string.IsNullOrEmpty(prop))
        {
            ListSortDirection dir = _currentSortDirection;
            if (prop != _currentSortProperty && (prop is "Level" or "LastModified" or "FileSizeBytes"))
                dir = ListSortDirection.Descending;

            ApplySort(prop, dir);
        }
    }

    private void BtnSortDirection_Click(object sender, RoutedEventArgs e)
    {
        var newDir = _currentSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        ApplySort(_currentSortProperty, newDir);
    }

    private void ApplySort(string propertyName, ListSortDirection direction)
    {
        if (!_isInitialized) return;
        _currentSortProperty = propertyName;
        _currentSortDirection = direction;

        if (_charactersView != null)
        {
            _charactersView.SortDescriptions.Clear();
            _charactersView.SortDescriptions.Add(new SortDescription(propertyName, direction));
            if (propertyName != "DisplayName")
            {
                _charactersView.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
            }
        }

        if (SortDirectionIcon != null && BtnSortDirection != null)
        {
            SortDirectionIcon.Data = (Geometry)FindResource(
                direction == ListSortDirection.Ascending ? "IconSortAscGeometry" : "IconSortDescGeometry");
            BtnSortDirection.ToolTip = direction == ListSortDirection.Ascending 
                ? "Orden actual: Ascendente (Clic para Descendente)" 
                : "Orden actual: Descendente (Clic para Ascendente)";
        }

        SyncSortComboBox(propertyName);
        UpdateColumnHeaderIndicators(propertyName, direction);
    }

    private void SyncSortComboBox(string propertyName)
    {
        if (CbSortSelector == null) return;
        _isSyncingSort = true;
        try
        {
            foreach (var item in CbSortSelector.Items)
            {
                if (item is ComboBoxItem cbi && (string?)cbi.Tag == propertyName)
                {
                    CbSortSelector.SelectedItem = cbi;
                    break;
                }
            }
        }
        finally
        {
            _isSyncingSort = false;
        }
    }

    private void UpdateColumnHeaderIndicators(string propertyName, ListSortDirection direction)
    {
        string arrow = direction == ListSortDirection.Ascending ? " ▲" : " ▼";
        if (ColHeaderPersonaje != null) ColHeaderPersonaje.Header = "PERSONAJE" + (propertyName == "DisplayName" ? arrow : "");
        if (ColHeaderClase != null) ColHeaderClase.Header = "CLASE / RANGO" + (propertyName == "HeroClassDisplay" ? arrow : "");
        if (ColHeaderRaza != null) ColHeaderRaza.Header = "FACCIÓN / RAZA" + (propertyName == "RaceDisplay" ? arrow : "");
        if (ColHeaderNivel != null) ColHeaderNivel.Header = "NIVEL" + (propertyName == "Level" ? arrow : "");
        if (ColHeaderRasgo != null) ColHeaderRasgo.Header = "RASGO INNATO" + (propertyName == "TraitDisplay" ? arrow : "");
        if (ColHeaderTamano != null) ColHeaderTamano.Header = "TAMAÑO" + (propertyName == "FileSizeBytes" ? arrow : "");
        if (ColHeaderModificado != null) ColHeaderModificado.Header = "MODIFICADO" + (propertyName == "LastModified" ? arrow : "");
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
            BtnEditCharacter.IsEnabled = !_isScanning;
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
                    var raceBitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(selected.RaceIconPath!));
                    ImgSelectedRace.Source = raceBitmap;
                    ImgSelectedRace.Visibility = Visibility.Visible;
                    if (ImgSelectedBannerWatermark != null)
                    {
                        ImgSelectedBannerWatermark.Source = raceBitmap;
                        ImgSelectedBannerWatermark.Visibility = Visibility.Visible;
                    }
                }
                catch
                {
                    ImgSelectedRace.Visibility = Visibility.Collapsed;
                    if (ImgSelectedBannerWatermark != null)
                        ImgSelectedBannerWatermark.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ImgSelectedRace.Visibility = Visibility.Collapsed;
                if (ImgSelectedBannerWatermark != null)
                    ImgSelectedBannerWatermark.Visibility = Visibility.Collapsed;
            }

            // Estilo visual Lord (Oro Imperial) vs Héroe (Cian Neón)
            bool isLord = selected.IsLord;
            if (BorderSelectedPortraitFrame != null)
            {
                BorderSelectedPortraitFrame.BorderBrush = (Brush)FindResource(isLord ? "GoldPrimaryBrush" : "CyanNeonBrush");
            }
            if (BorderSelectedRoleBadge != null && TxtSelectedRoleBadge != null && IconSelectedRoleCrown != null)
            {
                BorderSelectedRoleBadge.Background = (Brush)FindResource(isLord ? "GoldDarkBrush" : "BlueDarkBrush");
                BorderSelectedRoleBadge.BorderBrush = (Brush)FindResource(isLord ? "GoldPrimaryBrush" : "BluePrimaryBrush");
                TxtSelectedRoleBadge.Text = selected.RoleShortDisplay;
                TxtSelectedRoleBadge.Foreground = (Brush)FindResource(isLord ? "GoldLightBrush" : "CyanLightBrush");
                IconSelectedRoleCrown.Visibility = isLord ? Visibility.Visible : Visibility.Collapsed;
            }

            TxtSelectedClass.Text = selected.HeroClassDisplay;
            TxtSelectedRace.Text = selected.RaceDisplay;
            TxtSelectedLevel.Text = selected.LevelDisplay;
            TxtSelectedTrait.Text = selected.TraitDisplay;

            // Mostrar habilidades con badges o mensaje por defecto
            if (selected.Metadata?.Skills.Count > 0)
            {
                if (TxtSkillsHeader != null)
                    TxtSkillsHeader.Text = $"HABILIDADES APRENDIDAS ({selected.Metadata.Skills.Count})";
                IcSelectedSkills.ItemsSource = selected.Metadata.Skills;
                IcSelectedSkills.Visibility = Visibility.Visible;
                TxtNoSkills.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (TxtSkillsHeader != null)
                    TxtSkillsHeader.Text = "HABILIDADES APRENDIDAS";
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
            BtnEditCharacter.IsEnabled = false;
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

        if (ModalBackdrop != null) ModalBackdrop.Visibility = Visibility.Visible;
        try
        {
            duplicateWindow.ShowDialog();
        }
        finally
        {
            if (ModalBackdrop != null) ModalBackdrop.Visibility = Visibility.Collapsed;
        }

        if (duplicateWindow.CharactersGenerated > 0)
        {
            await ScanFolderAsync();
            ShowToast($"✓ Se generaron {duplicateWindow.CharactersGenerated} copias exitosamente.");
        }
    }

    private async void BtnEditCharacter_Click(object sender, RoutedEventArgs e)
    {
        CharacterFile? selectedCharacter = GetSelectedCharacter();
        if (selectedCharacter == null)
        {
            MessageBox.Show("Por favor, selecciona un personaje para editar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editWindow = new CharacterEditWindow(selectedCharacter)
        {
            Owner = this
        };

        bool? dialogResult;
        if (ModalBackdrop != null) ModalBackdrop.Visibility = Visibility.Visible;
        try
        {
            dialogResult = editWindow.ShowDialog();
        }
        finally
        {
            if (ModalBackdrop != null) ModalBackdrop.Visibility = Visibility.Collapsed;
        }

        if (dialogResult == true || editWindow.WasSaved)
        {
            string selectedFilePath = selectedCharacter.FilePath;
            await ScanFolderAsync();

            // Volver a seleccionar el personaje editado en la lista/galería
            var updated = _characterFiles.FirstOrDefault(c => c.FilePath.Equals(selectedFilePath, StringComparison.OrdinalIgnoreCase));
            if (updated != null)
            {
                LvCharacters.SelectedItem = updated;
                LbGallery.SelectedItem = updated;
                LvCharacters.ScrollIntoView(updated);
            }

            ShowToast($"✓ Personaje '{updated?.DisplayName ?? selectedCharacter.DisplayName}' actualizado correctamente.");
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

    private void CtxEdit_Click(object sender, RoutedEventArgs e)
    {
        BtnEditCharacter_Click(sender, e);
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