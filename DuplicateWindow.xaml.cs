using System.IO;
using System.Text;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace WH3CharacterManager;

public partial class DuplicateWindow : Window
{
    private CharacterFile originalCharacter;
    public int CharactersGenerated { get; private set; }

    public DuplicateWindow(CharacterFile character)
    {
        InitializeComponent();
        originalCharacter = character;
        LblCharacterName.Content = character.FileName;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int copies;
            if (!int.TryParse(TxtCopies.Text, out copies) || copies <= 0)
            {
                MessageBox.Show("Por favor, introduzca un número válido de copias.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string outputFolderPath;
            if (RbSameFolder.IsChecked == true)
            {
                outputFolderPath = Path.GetDirectoryName(originalCharacter.FilePath);
            }
            else
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "Selecciona la carpeta de destino para las copias";
                    
                var result = dialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                    
                outputFolderPath = dialog.SelectedPath;
            }

            // Extraer la base del ID y el número
            (string baseIdPersonaje, int numeroIdOriginal) = ExtraerBaseId(originalCharacter.FileName);

            // Comenzar la duplicación
            PbProgress.Maximum = copies;
            PbProgress.Value = 0;
            CharactersGenerated = 0;

            for (int i = 1; i <= copies; i++)
            {
                int nuevoNumeroId = numeroIdOriginal + i;
                string nuevoIdPersonaje = baseIdPersonaje + nuevoNumeroId;

                byte[] datosModificados = ReemplazarIdEnDatos(
                    originalCharacter.FileContent,
                    originalCharacter.FileName,
                    nuevoIdPersonaje
                );

                string nuevaRutaArchivo = Path.Combine(outputFolderPath, $"{nuevoIdPersonaje}.twc");
                File.WriteAllBytes(nuevaRutaArchivo, datosModificados);

                CharactersGenerated++;
                PbProgress.Value = i;
            }

            MessageBox.Show($"Se generaron {CharactersGenerated} copias del personaje.", "Operación completada", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al duplicar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static (string baseId, int numeroId) ExtraerBaseId(string idCompleto)
    {
        int length = idCompleto.Length;
        int index = length - 1;

        // Recorremos la cadena desde el final hacia el principio
        while (index >= 0 && char.IsDigit(idCompleto[index]))
        {
            index--;
        }

        // index + 1 es el inicio de la parte numérica
        string baseId = idCompleto.Substring(0, index + 1);
        string numeroStr = idCompleto.Substring(index + 1);

        // Convertimos la parte numérica a entero
        if (int.TryParse(numeroStr, out int numeroId))
        {
            return (baseId, numeroId);
        }
        else
        {
            throw new ArgumentException("El formato de la cadena no es válido.");
        }
    }

    private static byte[] ReemplazarIdEnDatos(byte[] datos, string idAntiguo, string idNuevo)
    {
        string datosHex = BitConverter.ToString(datos).Replace("-", "").ToLower();
        string idAntiguoHex = ConvertirBytesAHex(Encoding.UTF8.GetBytes(idAntiguo));
        string idNuevoHex = ConvertirBytesAHex(Encoding.UTF8.GetBytes(idNuevo));

        string datosHexModificados = datosHex.Replace(idAntiguoHex, idNuevoHex);

        return ConvertirHexAByteArray(datosHexModificados);
    }

    private static string ConvertirBytesAHex(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    private static byte[] ConvertirHexAByteArray(string hex)
    {
        int longitud = hex.Length;
        byte[] datos = new byte[longitud / 2];

        for (int i = 0; i < longitud; i += 2)
            datos[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);

        return datos;
    }
}