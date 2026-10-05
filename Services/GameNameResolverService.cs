using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WH3CharacterManager.Services;

public interface IGameNameResolverService
{
    string? Resolve(string key);
    string ResolveLoreName(ReadOnlySpan<byte> bytes);
}

public class GameNameResolverService : IGameNameResolverService
{
    private static readonly Lazy<GameNameResolverService> _lazyInstance = new(() => new GameNameResolverService());
    public static GameNameResolverService Instance => _lazyInstance.Value;

    private readonly Dictionary<string, string> _namesDict = new(StringComparer.OrdinalIgnoreCase);

    public GameNameResolverService()
    {
        LoadEmbeddedNames();
    }

    private void LoadEmbeddedNames()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Try to find the embedded resource
            string resourceName = "WH3CharacterManager.Assets.names_db.json";
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string json = reader.ReadToEnd();
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        _namesDict[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch
        {
            // Fallback silencioso si el recurso no pudo cargarse
        }
    }

    public string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _namesDict.TryGetValue(key, out string? val) ? val : null;
    }

    public string ResolveLoreName(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return string.Empty;

        string ascii = Encoding.ASCII.GetString(bytes);
        var matches = Regex.Matches(ascii, @"names_name_\d+");
        if (matches.Count == 0) return string.Empty;

        string forename = string.Empty;
        string familyName = string.Empty;

        foreach (Match m in matches)
        {
            if (_namesDict.TryGetValue(m.Value, out string? resolved) && !string.IsNullOrWhiteSpace(resolved))
            {
                int start = Math.Max(0, m.Index - 35);
                string context = ascii.Substring(start, m.Index - start);

                if (context.Contains("family_name", StringComparison.OrdinalIgnoreCase))
                {
                    familyName = resolved;
                }
                else if (context.Contains("forename", StringComparison.OrdinalIgnoreCase) ||
                         context.Contains("clan_name", StringComparison.OrdinalIgnoreCase) ||
                         context.Contains("other", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(forename))
                    {
                        forename = resolved;
                    }
                    else if (string.IsNullOrEmpty(familyName))
                    {
                        familyName = resolved;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(forename))
                    {
                        forename = resolved;
                    }
                    else if (string.IsNullOrEmpty(familyName))
                    {
                        familyName = resolved;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(forename) && !string.IsNullOrEmpty(familyName) &&
            !forename.Equals(familyName, StringComparison.OrdinalIgnoreCase))
        {
            return $"{forename} {familyName}";
        }

        return !string.IsNullOrEmpty(forename) ? forename : familyName;
    }
}
