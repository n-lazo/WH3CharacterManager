using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WH3CharacterManager.Models;
using WH3CharacterManager.Services;

namespace WH3CharacterManager;

public partial class DuplicateWindow : Window
{
    private readonly CharacterFile _originalCharacter;
    private readonly ICharacterDuplicationService _duplicationService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isDuplicating;

    public int CharactersGenerated { get; private set; }

    public DuplicateWindow(CharacterFile character, ICharacterDuplicationService? duplicationService = null)
    {
        InitializeComponent();
        _originalCharacter = character ?? throw new ArgumentNullException(nameof(character));
        _duplicationService = duplicationService ?? new CharacterDuplicationService();
        LblCharacterName.Text = character.FileName + ".twc";
        UpdatePreview();
    }

    private void TxtCopies_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void BtnMinusCopies_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtCopies.Text.Trim(), out int copies) && copies > 1)
        {
            TxtCopies.Text = (copies - 1).ToString();
        }
    }

    private void BtnPlusCopies_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtCopies.Text.Trim(), out int copies))
        {
            TxtCopies.Text = (copies + 1).ToString();
        }
        else
        {
            TxtCopies.Text = "1";
        }
    }

    private void BtnPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string text && text.StartsWith("+"))
        {
            if (int.TryParse(text[1..], out int add))
            {
                int current = int.TryParse(TxtCopies.Text.Trim(), out int val) ? val : 0;
                TxtCopies.Text = Math.Max(1, current + add).ToString();
            }
        }
    }

    private void UpdatePreview()
    {
        if (TxtPreviewFirst == null || TxtPreviewLast == null)
            return;

        if (int.TryParse(TxtCopies.Text.Trim(), out int copies) && copies > 0)
        {
            try
            {
                CharacterIdInfo idInfo = CharacterIdParser.Parse(_originalCharacter.FileName);
                string first = idInfo.GenerateId(1) + ".twc";
                string last = idInfo.GenerateId(copies) + ".twc";

                TxtPreviewFirst.Text = first;
                TxtPreviewLast.Text = last;
                if (BtnDuplicate != null) BtnDuplicate.IsEnabled = true;
                return;
            }
            catch
            {
                // Ignorar error al parsear durante edición
            }
        }

        TxtPreviewFirst.Text = "-";
        TxtPreviewLast.Text = "-";
        if (BtnDuplicate != null) BtnDuplicate.IsEnabled = false;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isDuplicating)
        {
            BtnCancel.IsEnabled = false;
            BtnCancel.Content = "CANCELANDO...";
            _cancellationTokenSource?.Cancel();
            return;
        }

        Close();
    }

    private async void BtnDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtCopies.Text.Trim(), out int copies) || copies <= 0)
        {
            MessageBox.Show("Por favor, introduzca un número válido de copias.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string? outputFolderPath;
        if (RbSameFolder.IsChecked == true)
        {
            outputFolderPath = Path.GetDirectoryName(_originalCharacter.FilePath);
        }
        else
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Selecciona la carpeta de destino para las copias",
                InitialDirectory = Path.GetDirectoryName(_originalCharacter.FilePath) ?? string.Empty
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            outputFolderPath = dialog.FolderName;
        }

        if (string.IsNullOrWhiteSpace(outputFolderPath) || !Directory.Exists(outputFolderPath))
        {
            MessageBox.Show("La carpeta de destino no es válida o no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _isDuplicating = true;
        _cancellationTokenSource = new CancellationTokenSource();

        TxtCopies.IsEnabled = false;
        BtnMinusCopies.IsEnabled = false;
        BtnPlusCopies.IsEnabled = false;
        RbSameFolder.IsEnabled = false;
        RbCustomFolder.IsEnabled = false;
        BtnDuplicate.IsEnabled = false;
        BtnCancel.Content = "DETENER";

        PbProgress.Minimum = 0;
        PbProgress.Maximum = copies;
        PbProgress.Value = 0;
        TxtProgressStatus.Visibility = Visibility.Visible;
        TxtProgressStatus.Text = $"Iniciando duplicación (0/{copies})...";

        var progress = new Progress<int>(current =>
        {
            PbProgress.Value = current;
            int percent = (int)((double)current / copies * 100);
            TxtProgressStatus.Text = $"Generando copia {current} de {copies}... ({percent}%)";
        });

        try
        {
            CharactersGenerated = await _duplicationService.DuplicateCharacterAsync(
                _originalCharacter,
                outputFolderPath,
                copies,
                progress,
                _cancellationTokenSource.Token);

            MessageBox.Show(
                $"Se generaron {CharactersGenerated} copias exitosamente del personaje.",
                "Operación completada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Close();
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(
                $"Operación cancelada. Se alcanzaron a generar {CharactersGenerated} de {copies} copias.",
                "Cancelado",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al duplicar: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isDuplicating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            TxtCopies.IsEnabled = true;
            BtnMinusCopies.IsEnabled = true;
            BtnPlusCopies.IsEnabled = true;
            RbSameFolder.IsEnabled = true;
            RbCustomFolder.IsEnabled = true;
            BtnDuplicate.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnCancel.Content = "CANCELAR";
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isDuplicating)
        {
            _cancellationTokenSource?.Cancel();
        }
        base.OnClosing(e);
    }
}