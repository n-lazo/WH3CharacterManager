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

    public static byte[] ReplaceUtf16StringNearMarker(
        ReadOnlySpan<byte> source,
        string marker,
        string oldString,
        string newString,
        int searchWindowBytes = 256)
    {
        if (string.IsNullOrEmpty(oldString) || string.IsNullOrEmpty(newString) || oldString == newString)
            return source.ToArray();

        byte[] markerBytes = Encoding.UTF8.GetBytes(marker);
        int markerIdx = source.IndexOf(markerBytes);
        if (markerIdx < 0)
            return source.ToArray();

        int windowStart = markerIdx + markerBytes.Length;
        int windowEnd = Math.Min(source.Length, windowStart + searchWindowBytes);

        byte[] oldUnicodeBytes = Encoding.Unicode.GetBytes(oldString);
        byte[] oldLenPrefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(oldLenPrefix, oldString.Length);

        byte[] targetPattern = [.. oldLenPrefix, .. oldUnicodeBytes];

        int patternIdx = -1;
        for (int i = windowStart; i <= windowEnd - targetPattern.Length; i++)
        {
            if (source.Slice(i, targetPattern.Length).SequenceEqual(targetPattern))
            {
                patternIdx = i;
                break;
            }
        }

        if (patternIdx < 0)
        {
            for (int i = windowStart; i <= windowEnd - oldUnicodeBytes.Length; i++)
            {
                if (source.Slice(i, oldUnicodeBytes.Length).SequenceEqual(oldUnicodeBytes))
                {
                    patternIdx = i;
                    if (i >= 4)
                    {
                        int prefix = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(i - 4, 4));
                        if (prefix == oldString.Length)
                        {
                            patternIdx = i - 4;
                            break;
                        }
                    }
                    break;
                }
            }
        }

        if (patternIdx < 0)
            return source.ToArray();

        bool hasLengthPrefix = false;
        int prefixLen = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(patternIdx, 4));
        if (prefixLen == oldString.Length && (patternIdx + 4 + oldUnicodeBytes.Length) <= source.Length &&
            source.Slice(patternIdx + 4, oldUnicodeBytes.Length).SequenceEqual(oldUnicodeBytes))
        {
            hasLengthPrefix = true;
        }

        byte[] newUnicodeBytes = Encoding.Unicode.GetBytes(newString);
        int oldBlockLen = (hasLengthPrefix ? 4 : 0) + oldUnicodeBytes.Length;
        int newBlockLen = (hasLengthPrefix ? 4 : 0) + newUnicodeBytes.Length;
        int sizeDiff = newBlockLen - oldBlockLen;

        byte[] result = new byte[source.Length + sizeDiff];

        source[..patternIdx].CopyTo(result);

        int writePos = patternIdx;
        if (hasLengthPrefix)
        {
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(writePos, 4), newString.Length);
            writePos += 4;
        }

        newUnicodeBytes.CopyTo(result.AsSpan(writePos, newUnicodeBytes.Length));
        writePos += newUnicodeBytes.Length;

        source[(patternIdx + oldBlockLen)..].CopyTo(result.AsSpan(writePos));

        return result;
    }

    public static byte[] UpdateCharacterNamesInBytes(
        ReadOnlySpan<byte> source,
        CharacterMetadata originalMetadata,
        string? newForename,
        string? newSurname,
        string? newSavedTag)
    {
        byte[] current = source.ToArray();
        const string marker = "SAVED_INITIATIVE_SET_INFO";

        if (!string.IsNullOrWhiteSpace(newForename))
        {
            string oldForename = string.Empty;
            if (!string.IsNullOrWhiteSpace(originalMetadata.CustomName))
            {
                var parts = originalMetadata.CustomName.Split(' ', 2);
                oldForename = parts[0];
            }

            if (!string.IsNullOrEmpty(oldForename) && oldForename != newForename)
            {
                current = ReplaceUtf16StringNearMarker(current, marker, oldForename, newForename);
            }
        }

        if (!string.IsNullOrWhiteSpace(newSurname))
        {
            string oldSurname = string.Empty;
            if (!string.IsNullOrWhiteSpace(originalMetadata.CustomName))
            {
                var parts = originalMetadata.CustomName.Split(' ', 2);
                if (parts.Length > 1)
                    oldSurname = parts[1];
            }

            if (!string.IsNullOrEmpty(oldSurname) && oldSurname != newSurname)
            {
                current = ReplaceUtf16StringNearMarker(current, marker, oldSurname, newSurname);
            }
        }

        if (!string.IsNullOrWhiteSpace(newSavedTag) && !string.IsNullOrWhiteSpace(originalMetadata.SavedName) && originalMetadata.SavedName != newSavedTag)
        {
            current = ReplaceUtf16StringNearMarker(current, marker, originalMetadata.SavedName, newSavedTag);
        }

        return current;
    }
}

public interface ICharacterDuplicationService
{
    Task<int> DuplicateCharacterAsync(
        CharacterFile character,
        string targetDirectory,
        int copies,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        string? namePattern = null,
        string? tagPattern = null);
}

public class CharacterDuplicationService : ICharacterDuplicationService
{
    public async Task<int> DuplicateCharacterAsync(
        CharacterFile character,
        string targetDirectory,
        int copies,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        string? namePattern = null,
        string? tagPattern = null)
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

        string baseForename = string.Empty;
        string baseSurname = string.Empty;
        if (!string.IsNullOrWhiteSpace(character.Metadata?.CustomName))
        {
            var parts = character.Metadata.CustomName.Split(' ', 2);
            baseForename = parts[0];
            if (parts.Length > 1) baseSurname = parts[1];
        }

        string baseTag = character.Metadata?.SavedName ?? string.Empty;

        int generatedCount = 0;

        for (int i = 1; i <= copies; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string newId = idInfo.GenerateId(i);
            byte[] modifiedBytes = CharacterBinaryModifier.ReplaceIdInBytes(originalBytes, character.FileName, newId);

            if (character.Metadata != null)
            {
                string? copyForename = null;
                if (!string.IsNullOrWhiteSpace(namePattern) && !string.IsNullOrWhiteSpace(baseForename))
                {
                    copyForename = namePattern.Replace("{name}", baseForename).Replace("{n}", i.ToString());
                }

                string? copyTag = null;
                if (!string.IsNullOrWhiteSpace(tagPattern) && !string.IsNullOrWhiteSpace(baseTag))
                {
                    copyTag = tagPattern.Replace("{tag}", baseTag).Replace("{n}", i.ToString());
                }

                if (!string.IsNullOrWhiteSpace(copyForename) || !string.IsNullOrWhiteSpace(copyTag))
                {
                    modifiedBytes = CharacterBinaryModifier.UpdateCharacterNamesInBytes(
                        modifiedBytes,
                        character.Metadata,
                        copyForename,
                        baseSurname,
                        copyTag);
                }
            }

            string targetFilePath = Path.Combine(targetDirectory, $"{newId}.twc");
            await File.WriteAllBytesAsync(targetFilePath, modifiedBytes, cancellationToken);

            generatedCount++;
            progress?.Report(generatedCount);
        }

        return generatedCount;
    }
}
