using System.Buffers.Binary;
using System.IO;
using System.Text;
using WH3CharacterManager.Models;

namespace WH3CharacterManager.Services;

public static class CharacterBinaryModifier
{
    public static byte[] ReplaceIdInBytes(ReadOnlySpan<byte> source, string oldId, string newId)
    {
        ArgumentException.ThrowIfNullOrEmpty(oldId);
        ArgumentException.ThrowIfNullOrEmpty(newId);

        byte[] oldBytes = Encoding.UTF8.GetBytes(oldId);
        byte[] newBytes = Encoding.UTF8.GetBytes(newId);

        var matchIndices = new List<int>();
        int searchPos = 0;
        while (searchPos <= source.Length - oldBytes.Length)
        {
            int idx = source[searchPos..].IndexOf(oldBytes);
            if (idx < 0)
                break;

            int matchIdx = searchPos + idx;
            matchIndices.Add(matchIdx);
            searchPos = matchIdx + oldBytes.Length;
        }

        if (matchIndices.Count == 0)
        {
            return source.ToArray();
        }

        // Fast path: same byte length (standard for WH3 with preserved padding)
        if (oldBytes.Length == newBytes.Length)
        {
            byte[] result = source.ToArray();
            Span<byte> resultSpan = result.AsSpan();
            foreach (int idx in matchIndices)
            {
                newBytes.CopyTo(resultSpan.Slice(idx, newBytes.Length));
            }
            return result;
        }

        // Length differs: adjust buffer and update 4-byte string length prefix if present
        int sizeDiffPerMatch = newBytes.Length - oldBytes.Length;
        int totalNewSize = source.Length + (matchIndices.Count * sizeDiffPerMatch);
        byte[] newBuffer = new byte[totalNewSize];

        int srcReadPos = 0;
        int dstWritePos = 0;

        foreach (int matchIdx in matchIndices)
        {
            bool hasLengthPrefix = false;
            int copyUpTo = matchIdx;

            if (matchIdx >= 4)
            {
                int prefixLength = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(matchIdx - 4, 4));
                if (prefixLength == oldBytes.Length)
                {
                    hasLengthPrefix = true;
                    copyUpTo = matchIdx - 4;
                }
            }

            int chunkLen = copyUpTo - srcReadPos;
            if (chunkLen > 0)
            {
                source.Slice(srcReadPos, chunkLen).CopyTo(newBuffer.AsSpan(dstWritePos, chunkLen));
                dstWritePos += chunkLen;
                srcReadPos += chunkLen;
            }

            if (hasLengthPrefix)
            {
                BinaryPrimitives.WriteInt32LittleEndian(newBuffer.AsSpan(dstWritePos, 4), newBytes.Length);
                dstWritePos += 4;
                srcReadPos += 4;
            }

            newBytes.CopyTo(newBuffer.AsSpan(dstWritePos, newBytes.Length));
            dstWritePos += newBytes.Length;
            srcReadPos += oldBytes.Length;
        }

        if (srcReadPos < source.Length)
        {
            int tailLen = source.Length - srcReadPos;
            source.Slice(srcReadPos, tailLen).CopyTo(newBuffer.AsSpan(dstWritePos, tailLen));
            dstWritePos += tailLen;
        }

        return newBuffer;
    }
}

public interface ICharacterDuplicationService
{
    Task<int> DuplicateCharacterAsync(
        CharacterFile character,
        string targetDirectory,
        int copies,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}

public class CharacterDuplicationService : ICharacterDuplicationService
{
    public async Task<int> DuplicateCharacterAsync(
        CharacterFile character,
        string targetDirectory,
        int copies,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        if (copies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(copies), "El número de copias debe ser mayor que 0.");
        }

        if (!File.Exists(character.FilePath))
        {
            throw new FileNotFoundException($"El archivo del personaje no existe: {character.FilePath}", character.FilePath);
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        CharacterIdInfo idInfo = CharacterIdParser.Parse(character.FileName);
        byte[] originalBytes = await File.ReadAllBytesAsync(character.FilePath, cancellationToken);

        int generatedCount = 0;

        for (int i = 1; i <= copies; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string newId = idInfo.GenerateId(i);
            byte[] modifiedBytes = CharacterBinaryModifier.ReplaceIdInBytes(originalBytes, character.FileName, newId);

            string targetFilePath = Path.Combine(targetDirectory, $"{newId}.twc");
            await File.WriteAllBytesAsync(targetFilePath, modifiedBytes, cancellationToken);

            generatedCount++;
            progress?.Report(generatedCount);
        }

        return generatedCount;
    }
}
