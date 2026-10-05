using System.Globalization;
using System.Numerics;

namespace WH3CharacterManager.Services;

public readonly record struct CharacterIdInfo(string BaseId, BigInteger StartNumber, int NumberPadding)
{
    public string GenerateId(int copyIndex)
    {
        BigInteger nextNumber = StartNumber + copyIndex;
        string numStr = nextNumber.ToString(CultureInfo.InvariantCulture).PadLeft(NumberPadding, '0');
        return BaseId + numStr;
    }
}

public static class CharacterIdParser
{
    public static CharacterIdInfo Parse(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        int index = fileName.Length - 1;
        while (index >= 0 && char.IsDigit(fileName[index]))
        {
            index--;
        }

        // If no trailing digits were found (e.g., "Tyrion", "KarlFranz")
        if (index == fileName.Length - 1)
        {
            return new CharacterIdInfo(fileName + "_", BigInteger.Zero, 1);
        }

        string basePart = fileName[..(index + 1)];
        string numPart = fileName[(index + 1)..];

        if (BigInteger.TryParse(numPart, NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger parsedNumber))
        {
            return new CharacterIdInfo(basePart, parsedNumber, numPart.Length);
        }

        return new CharacterIdInfo(fileName + "_", BigInteger.Zero, 1);
    }
}
