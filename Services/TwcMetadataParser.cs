using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WH3CharacterManager.Models;

namespace WH3CharacterManager.Services;

public static class TwcMetadataParser
{
    private static readonly Dictionary<string, string> CultureNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "wef", "Elfos Silvanos" },
        { "emp", "El Imperio" },
        { "hef", "Altos Elfos" },
        { "def", "Elfos Oscuros" },
        { "dwf", "Enanos" },
        { "grn", "Pieles Verdes" },
        { "vmp", "Condes Vampiro" },
        { "chs", "Guerreros del Caos" },
        { "bst", "Hombres Bestia" },
        { "nor", "Norsca" },
        { "brt", "Bretonia" },
        { "skv", "Skaven" },
        { "lzd", "Hombres Lagarto" },
        { "tmb", "Reyes Funerarios" },
        { "cst", "Costa del Vampiro" },
        { "ksl", "Kislev" },
        { "cth", "Gran Catai" },
        { "kho", "Khorne" },
        { "nur", "Nurgle" },
        { "sla", "Slaanesh" },
        { "tze", "Tzeentch" },
        { "ogr", "Reinos Ogros" },
        { "chd", "Enanos del Caos" },
        { "dae", "Demonios del Caos" }
    };

    private static readonly Dictionary<string, string> SubtypeFriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "glade_captain", "Glade Captain (Capitana del Claro)" },
        { "waystalker", "Waystalker (Acechador de Caminos)" },
        { "spellsinger", "Spellsinger (Cantora de Hechizos)" },
        { "branchwraith", "Branchwraith (Espectro de los Ramajes)" },
        { "glade_lord", "Glade Lord (Señor del Claro)" },
        { "treeman", "Ancient Treeman (Hombre Árbol Milenario)" },
        { "spellweaver", "Spellweaver (Tejedora de Hechizos)" },

        { "engineer", "Master Engineer (Ingeniero)" },
        { "captain", "Empire Captain (Capitán del Imperio)" },
        { "warrior_priest", "Warrior Priest (Sacerdote Guerrero)" },
        { "witch_hunter", "Witch Hunter (Cazador de Brujas)" },
        { "wizard", "Battle Wizard (Hechicero de Batalla)" },
        { "general", "General of the Empire (General del Imperio)" },
        { "arch_lector", "Arch Lector (Archilector)" },
        { "huntsman_general", "Huntsman General (General Cazador)" },

        { "thane", "Thane (Señor del Clan)" },
        { "runesmith", "Runesmith (Herrero Rúnico)" },
        { "master_engineer", "Master Engineer (Maestro Ingeniero)" },
        { "dragonslayer", "Dragonslayer (Matadragones)" },

        { "noble", "Noble" },
        { "mage", "Mage (Mago)" },
        { "loremaster", "Loremaster of Hoeth (Maestro del Saber)" },
        { "handmaiden", "Handmaiden (Doncella)" },

        { "assassin", "Assassin (Asesino)" },
        { "sorceress", "Sorceress (Hechicera)" },
        { "death_hag", "Death Hag (Bruja Elfa)" },
        { "master", "Master (Amo)" },

        { "chieftain", "Chieftain (Caudillo)" },
        { "warlock_engineer", "Warlock Engineer (Ingeniero Brujo)" },
        { "plague_priest", "Plague Priest (Sacerdote de la Peste)" },
        { "packmaster", "Packmaster (Señor de la Manada)" },
        { "eshin_sorcerer", "Eshin Sorcerer (Hechicero Eshin)" },

        { "patriarch", "Patriarch (Patriarca)" },
        { "frost_maiden", "Frost Maiden (Doncella de Hielo)" },
        { "alchemist", "Alchemist (Alquimista)" },
        { "astromancer", "Astromancer (Astromante)" },
        { "gate_master", "Gate Master (Maestro de la Puerta)" },

        { "wight_king", "Wight King (Rey Tumulario)" },
        { "vampire", "Vampire (Vampiresa)" },
        { "necromancer", "Necromancer (Nigromante)" },
        { "banshee", "Banshee" },
        { "vampire_lord", "Vampire Lord (Señor de los Vampiros)" },
        { "strigoi_ghoul_king", "Strigoi Ghoul King (Rey Necrófago Strigoi)" },
        { "master_necromancer", "Master Necromancer (Gran Nigromante)" }
    };

    public static CharacterMetadata Parse(ReadOnlySpan<byte> bytes, IGameNameResolverService? nameResolver = null)
    {
        var (customName, savedTag) = ExtractInitiativeNames(bytes);
        var tokens = ExtractTokens(bytes);

        string loreName = (nameResolver ?? GameNameResolverService.Instance).ResolveLoreName(bytes);

        string rawSubtype = FindSubtypeKey(tokens);
        string cultureCode = ExtractCultureCode(rawSubtype);
        string friendlyCulture = CultureNames.TryGetValue(cultureCode, out var cName) ? cName : "Desconocido";

        string friendlyClass = ResolveFriendlyClass(rawSubtype);
        string agentRole = ResolveAgentRole(tokens, rawSubtype);
        string trait = ExtractInnateTrait(tokens);
        string faction = ExtractFaction(tokens);
        var skills = ExtractActiveSkills(tokens);
        int level = Math.Max(1, 1 + skills.Count);

        string portholePath = ExtractPortholePath(tokens, bytes);

        return new CharacterMetadata
        {
            CustomName = customName,
            LoreName = loreName,
            SavedName = savedTag,
            HeroClass = friendlyClass,
            Race = friendlyCulture,
            Role = agentRole,
            Trait = trait,
            Faction = faction,
            Level = level,
            Skills = skills,
            SubtypeRaw = rawSubtype,
            CultureCode = cultureCode,
            PortholePath = portholePath
        };
    }

    private static string ExtractPortholePath(List<string> tokens, ReadOnlySpan<byte> bytes)
    {
        string ascii = Encoding.ASCII.GetString(bytes);
        var match = Regex.Match(ascii, @"UI/Portraits/Portholes/[^\x00\s]+\.png", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Value;

        foreach (string t in tokens)
        {
            if (t.Contains("portholes", StringComparison.OrdinalIgnoreCase) && t.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return string.Empty;
    }

    private static (string CustomName, string SavedTag) ExtractInitiativeNames(ReadOnlySpan<byte> bytes)
    {
        byte[] marker = "SAVED_INITIATIVE_SET_INFO"u8.ToArray();
        int markerIdx = bytes.IndexOf(marker);
        if (markerIdx < 0) return (string.Empty, string.Empty);

        int searchStart = markerIdx + marker.Length;
        int searchEnd = Math.Min(bytes.Length - 4, searchStart + 120);
        var strings = new List<string>();

        for (int i = searchStart; i < searchEnd; i++)
        {
            int charLen = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i, 4));
            if (charLen >= 1 && charLen <= 40 && (i + 4 + charLen * 2) <= bytes.Length)
            {
                var slice = bytes.Slice(i + 4, charLen * 2);
                bool isUtf16 = true;
                for (int c = 0; c < charLen; c++)
                {
                    byte b1 = slice[c * 2];
                    byte b2 = slice[c * 2 + 1];
                    if (b2 != 0 || b1 < 32 || b1 > 126)
                    {
                        isUtf16 = false;
                        break;
                    }
                }

                if (isUtf16)
                {
                    strings.Add(Encoding.Unicode.GetString(slice));
                    i += 3 + charLen * 2;
                }
            }
        }

        if (strings.Count == 0)
            return (string.Empty, string.Empty);

        string savedTag = strings[^1];
        string customName = string.Empty;

        if (strings.Count >= 3)
        {
            // Formato de héroe con nombre y apellido personalizados: [0]=Apellido (Nitales), [1]=Nombre (Jorge), [2]=Tag (HawkShisho)
            string surname = strings[0];
            string forename = strings[1];
            customName = $"{forename} {surname}".Trim();
        }
        else if (strings.Count == 2)
        {
            // Un solo nombre personalizado: [0]=Nombre (ej: Legolas), [1]=Tag (HawkShisho)
            customName = strings[0].Trim();
        }

        return (customName, savedTag);
    }

    private static List<string> ExtractTokens(ReadOnlySpan<byte> bytes)
    {
        var tokens = new List<string>();
        string ascii = Encoding.ASCII.GetString(bytes);
        var matches = Regex.Matches(ascii, @"[\w\-\/\.]{4,}");
        foreach (Match m in matches)
        {
            tokens.Add(m.Value);
        }
        return tokens;
    }

    private static string FindSubtypeKey(List<string> tokens)
    {
        // 1. Priorizar si coincide con algún subtipo conocido de la base de datos
        foreach (string t in tokens)
        {
            if (t.Contains("_art_set_") || t.Contains("skill") || t.Contains("dummy") || t.Contains("cha_"))
                continue;

            foreach (var kvp in SubtypeFriendlyNames)
            {
                if (t.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("_" + kvp.Key + "_", StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
        }

        // 2. Buscar patrón típico de subtipos de héroes / lores:
        // ej: wh2_twa02_wef_glade_captain, wh_dlc05_wef_waystalker, wh3_dlc25_emp_engineer
        foreach (string t in tokens)
        {
            if (t.Contains("_art_set_") || t.Contains("skill") || t.Contains("dummy") || t.Contains("cha_") ||
                t.Contains("_host_") || t.Contains("_rebel") || t.Contains("_clan") || t.Contains("_tribe"))
                continue;

            var match = Regex.Match(t, @"^wh\d*_[a-z0-9]+_([a-z]{3})_([a-z0-9_]+)$");
            if (match.Success)
            {
                return t;
            }
        }

        // 3. Búsqueda secundaria si no hubo coincidencia exacta
        foreach (string t in tokens)
        {
            if (t.Contains("_cha_") && !t.Contains("skill") && !t.Contains("art_set"))
            {
                return t;
            }
        }

        return string.Empty;
    }

    private static string ExtractCultureCode(string subtypeKey)
    {
        if (string.IsNullOrEmpty(subtypeKey)) return string.Empty;

        var m = Regex.Match(subtypeKey, @"_(wef|emp|hef|def|dwf|grn|vmp|chs|bst|nor|brt|skv|lzd|tmb|cst|ksl|cth|kho|nur|sla|tze|ogr|chd|dae)_");
        if (m.Success)
        {
            return m.Groups[1].Value;
        }

        return string.Empty;
    }

    private static string ResolveFriendlyClass(string subtypeKey)
    {
        if (string.IsNullOrEmpty(subtypeKey)) return "Héroe / Comandante";

        // Intentar buscar el sufijo en el diccionario de nombres conocidos
        foreach (var kvp in SubtypeFriendlyNames)
        {
            if (subtypeKey.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                subtypeKey.Contains("_" + kvp.Key + "_", StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Limpieza heurística: extraer la última parte del identificador
        int lastUnderscore = subtypeKey.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < subtypeKey.Length - 1)
        {
            string part = subtypeKey[(lastUnderscore + 1)..];
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.Replace('_', ' '));
        }

        return subtypeKey;
    }

    private static string ResolveAgentRole(List<string> tokens, string subtypeKey)
    {
        if (tokens.Contains("general") || tokens.Contains("colonel") || subtypeKey.Contains("lord"))
            return "Lord / Comandante";

        return "Héroe";
    }

    private static string ExtractInnateTrait(List<string> tokens)
    {
        foreach (string t in tokens)
        {
            if (t.Contains("_skill_innate_"))
            {
                int idx = t.IndexOf("_skill_innate_", StringComparison.Ordinal);
                string traitRaw = t[(idx + "_skill_innate_".Length)..];
                // Remover prefijo de cultura si lo tiene (ej: wef_talon_of_kurnous -> talon_of_kurnous)
                traitRaw = Regex.Replace(traitRaw, @"^[a-z]{3}_", "");
                traitRaw = Regex.Replace(traitRaw, @"^all_", "");
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(traitRaw.Replace('_', ' '));
            }
        }

        return "Sin rasgo innato";
    }

    private static string ExtractFaction(List<string> tokens)
    {
        foreach (string t in tokens)
        {
            if (Regex.IsMatch(t, @"^wh\d*_[a-z0-9]+_[a-z]{3}_[a-z0-9_]+$") &&
                !t.Contains("skill") && !t.Contains("art_set") && !t.Contains("cha_"))
            {
                int lastIdx = t.LastIndexOf('_');
                if (lastIdx > 0)
                {
                    string fName = t[(lastIdx + 1)..];
                    return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fName);
                }
            }
        }

        return string.Empty;
    }

    private static List<string> ExtractActiveSkills(List<string> tokens)
    {
        var skills = new List<string>();
        foreach (string t in tokens)
        {
            if (t.Contains("_skill_") &&
                !t.Contains("innate") &&
                !t.Contains("dummy") &&
                !t.Contains("node_set") &&
                !t.Contains("success_scaling"))
            {
                int idx = t.IndexOf("_skill_", StringComparison.Ordinal);
                string raw = t[(idx + "_skill_".Length)..];
                string clean = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.Replace('_', ' ').Replace('-', ' '));
                if (!skills.Contains(clean))
                {
                    skills.Add(clean);
                }
            }
        }
        return skills;
    }
}
