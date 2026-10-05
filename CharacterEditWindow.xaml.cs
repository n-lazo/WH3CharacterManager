using System.IO;
using System.Windows;
using WH3CharacterManager.Models;
using WH3CharacterManager.Services;

namespace WH3CharacterManager;

public partial class CharacterEditWindow : Window
{
    private readonly CharacterFile _character;
    public bool WasSaved { get; private set; }

    public CharacterEditWindow(CharacterFile character)
    {
        InitializeComponent();
        _character = character ?? throw new ArgumentNullException(nameof(character));

        LblCharacterHeaderName.Text = character.DisplayName;
        LblCharacterSubInfo.Text = $"{character.HeroClassDisplay} • {character.RaceDisplay} • {character.LevelDisplay}";

        if (character.HasPortraitImage)
        {
            try
            {
                ImgCharacterPortrait.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(character.PortraitImagePath!));
            }
            catch
            {
                // Fallback
            }
        }

        if (character.HasRaceIcon)
        {
            try
            {
                ImgRaceIcon.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(character.RaceIconPath!));
                ImgRaceIcon.Visibility = Visibility.Visible;
            }
            catch
            {
                ImgRaceIcon.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ImgRaceIcon.Visibility = Visibility.Collapsed;
        }

        // Cargar nombres actuales
        string forename = string.Empty;
        string surname = string.Empty;

        if (!string.IsNullOrWhiteSpace(character.Metadata?.CustomName))
        {
            var parts = character.Metadata.CustomName.Split(' ', 2);
            forename = parts[0];
            if (parts.Length > 1) surname = parts[1];
        }
        else if (!string.IsNullOrWhiteSpace(character.Metadata?.LoreName))
        {
            forename = character.Metadata.LoreName;
        }

        TxtForename.Text = forename;
        TxtSurname.Text = surname;
        TxtSavedTag.Text = character.Metadata?.SavedName ?? string.Empty;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string forename = TxtForename.Text.Trim();
        string surname = TxtSurname.Text.Trim();
        string savedTag = TxtSavedTag.Text.Trim();

        if (string.IsNullOrWhiteSpace(savedTag) && string.IsNullOrWhiteSpace(forename))
        {
            MessageBox.Show("Debes ingresar al menos una etiqueta de guardado o un nombre de personaje.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(_character.FilePath))
        {
            MessageBox.Show("El archivo .twc ya no existe en disco.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // 1. Respaldo opcional
            if (ChkCreateBackup.IsChecked == true)
            {
                string backupPath = _character.FilePath + ".bak";
                File.Copy(_character.FilePath, backupPath, overwrite: true);
            }

            // 2. Leer bytes originales
            byte[] bytes = await File.ReadAllBytesAsync(_character.FilePath);

            // 3. Modificar binario con los nuevos nombres
            if (_character.Metadata != null)
            {
                byte[] updatedBytes = CharacterBinaryModifier.UpdateCharacterNamesInBytes(
                    bytes,
                    _character.Metadata,
                    forename,
                    surname,
                    savedTag);

                await File.WriteAllBytesAsync(_character.FilePath, updatedBytes);

                // 4. Re-analizar metadatos del archivo actualizado
                var newMetadata = TwcMetadataParser.Parse(updatedBytes);
                newMetadata.PortraitImagePath = _character.Metadata.PortraitImagePath;
                newMetadata.RaceIconPath = _character.Metadata.RaceIconPath;
                _character.Metadata = newMetadata;
            }

            WasSaved = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar los cambios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
