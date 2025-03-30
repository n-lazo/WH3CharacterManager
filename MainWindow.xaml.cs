using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WH3CharacterManager;

public partial class MainWindow
{
    private string characterFolderPath;
    private ObservableCollection<CharacterFile> characterFiles;

    public MainWindow()
    {
        InitializeComponent();
        characterFiles = new ObservableCollection<CharacterFile>();
        LvCharacters.ItemsSource = characterFiles;
            
        // Intentar obtener la ruta por defecto de los personajes de Warhammer 3
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        characterFolderPath = Path.Combine(appDataPath, "The Creative Assembly", "Warhammer3", "saved_characters");
        TxtFolderPath.Text = characterFolderPath;
    }

    private void BtnScanFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            characterFolderPath = TxtFolderPath.Text;

            if (!Directory.Exists(characterFolderPath))
            {
                MessageBox.Show($"La carpeta {characterFolderPath} no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            characterFiles.Clear();
            string[] files = Directory.GetFiles(characterFolderPath, "*.twc");
                
            foreach (string filePath in files)
            {
                byte[] fileContent = File.ReadAllBytes(filePath);
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                    
                characterFiles.Add(new CharacterFile
                {
                    FilePath = filePath,
                    FileName = fileName,
                    FileContent = fileContent
                });
            }

            StatusText.Text = $"Se encontraron {characterFiles.Count} personajes.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al escanear la carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        dialog.Description = "Selecciona la carpeta de personajes guardados de Warhammer 3";
        dialog.ShowNewFolderButton = false;
            
        if (Directory.Exists(TxtFolderPath.Text))
        {
            dialog.SelectedPath = TxtFolderPath.Text;
        }
            
        var result = dialog.ShowDialog();
            
        if (result == System.Windows.Forms.DialogResult.OK)
        {
            TxtFolderPath.Text = dialog.SelectedPath;
        }
    }

    private void BtnDuplicateCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (LvCharacters.SelectedItem == null)
        {
            MessageBox.Show("Por favor, selecciona un personaje para duplicar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CharacterFile selectedCharacter = (CharacterFile)LvCharacters.SelectedItem;
            
        DuplicateWindow duplicateWindow = new DuplicateWindow(selectedCharacter);
        duplicateWindow.Owner = this;
        duplicateWindow.ShowDialog();

        // Refrescar la lista después de posibles cambios
        if (duplicateWindow.CharactersGenerated > 0)
        {
            BtnScanFolder_Click(sender, e);
        }
    }

    private void BtnExportCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (LvCharacters.SelectedItem == null)
        {
            MessageBox.Show("Por favor, selecciona un personaje para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CharacterFile selectedCharacter = (CharacterFile)LvCharacters.SelectedItem;
            
        SaveFileDialog saveDialog = new SaveFileDialog
        {
            FileName = selectedCharacter.FileName + ".twc",
            Filter = "Warhammer Character Files (*.twc)|*.twc",
            Title = "Exportar personaje"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(selectedCharacter.FilePath, saveDialog.FileName, true);
                MessageBox.Show("Personaje exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

public class CharacterFile
{
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public byte[] FileContent { get; set; }
}