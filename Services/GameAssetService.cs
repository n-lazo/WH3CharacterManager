using System.IO;
using System.Text;
using Microsoft.Win32;
using ZstdSharp;

namespace WH3CharacterManager.Services;

public interface IGameAssetService
{
    string? GetPortraitImagePath(string? portholePath);
    string? GetRaceIconPath(string? cultureCode);
}

public class GameAssetService : IGameAssetService
{
    private static readonly Lazy<GameAssetService> _lazyInstance = new(() => new GameAssetService());
    public static GameAssetService Instance => _lazyInstance.Value;

    private readonly string _cacheDir;
    private readonly string? _uiPackPath;
    private readonly object _lock = new();

    private bool _indexLoaded;
    private readonly Dictionary<string, (long Offset, uint Size, byte Comp)> _packIndex = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> RaceFlagPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        { "emp", @"ui\flags\wh_main_emp_empire\mon_64.png" },
        { "dwf", @"ui\flags\wh_main_dwf_dwarfs\mon_64.png" },
        { "grn", @"ui\flags\wh_main_grn_greenskins\mon_64.png" },
        { "vmp", @"ui\flags\wh_main_vmp_vampire_counts\mon_64.png" },
        { "chs", @"ui\flags\wh_main_chs_chaos\mon_64.png" },
        { "brt", @"ui\flags\wh_main_brt_bretonnia\mon_64.png" },
        { "bst", @"ui\flags\wh_dlc03_bst_beastmen\mon_64.png" },
        { "wef", @"ui\flags\wh_dlc05_wef_wood_elves\mon_64.png" },
        { "nor", @"ui\flags\wh_dlc08_nor_norsca\mon_64.png" },
        { "hef", @"ui\flags\wh2_main_hef_eataine\mon_64.png" },
        { "def", @"ui\flags\wh2_main_def_naggarond\mon_64.png" },
        { "lzd", @"ui\flags\wh2_main_lzd_hexoatl\mon_64.png" },
        { "skv", @"ui\flags\wh2_main_skv_clan_mors\mon_64.png" },
        { "tmb", @"ui\flags\wh2_dlc09_tmb_khemri\mon_64.png" },
        { "cst", @"ui\flags\wh2_dlc11_cst_vampire_coast\mon_64.png" },
        { "ksl", @"ui\flags\wh3_main_ksl_the_ice_court\mon_64.png" },
        { "cth", @"ui\flags\wh3_main_cth_the_northern_provinces\mon_64.png" },
        { "kho", @"ui\flags\wh3_main_kho_exiles_of_khorne\mon_64.png" },
        { "nur", @"ui\flags\wh3_main_nur_poxmakers_of_nurgle\mon_64.png" },
        { "sla", @"ui\flags\wh3_main_sla_seducers_of_slaanesh\mon_64.png" },
        { "tze", @"ui\flags\wh3_main_tze_oracles_of_tzeentch\mon_64.png" },
        { "ogr", @"ui\flags\wh3_main_ogr_goldtooth\mon_64.png" },
        { "chd", @"ui\flags\wh3_dlc23_chd_astragoth\mon_64.png" },
        { "dae", @"ui\flags\wh3_main_dae_daemon_prince\mon_64.png" }
    };

    public GameAssetService()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WH3CharacterManager",
            "cache",
            "images");

        try
        {
            Directory.CreateDirectory(_cacheDir);
        }
        catch { }

        _uiPackPath = DetectUiPackPath();
    }

    private static string? DetectUiPackPath()
    {
        string[] candidates = [
            @"C:\Program Files (x86)\Steam\steamapps\common\Total War WARHAMMER III\data\ui.pack",
            @"D:\SteamLibrary\steamapps\common\Total War WARHAMMER III\data\ui.pack",
            @"E:\SteamLibrary\steamapps\common\Total War WARHAMMER III\data\ui.pack"
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Total War: WARHAMMER III");
            if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
            {
                string pack = Path.Combine(loc, "data", "ui.pack");
                if (File.Exists(pack))
                    return pack;
            }
        }
        catch { }

        return null;
    }

    private void EnsurePackIndexLoaded()
    {
        if (_indexLoaded || string.IsNullOrEmpty(_uiPackPath) || !File.Exists(_uiPackPath))
            return;

        lock (_lock)
        {
            if (_indexLoaded) return;

            try
            {
                using var fs = File.OpenRead(_uiPackPath);
                using var br = new BinaryReader(fs);

                br.ReadBytes(20);
                int indexSize = br.ReadInt32();
                br.ReadBytes(4);

                byte[] indexBytes = br.ReadBytes(indexSize);
                long currentDataOffset = fs.Position;

                using var ms = new MemoryStream(indexBytes);
                using var ims = new BinaryReader(ms);

                while (ms.Position < ms.Length)
                {
                    uint size = ims.ReadUInt32();
                    byte comp = ims.ReadByte();
                    var sb = new StringBuilder();
                    byte b;
                    while ((b = ims.ReadByte()) != 0) sb.Append((char)b);

                    string path = sb.ToString();
                    _packIndex[path] = (currentDataOffset, size, comp);
                    currentDataOffset += size;
                }

                _indexLoaded = true;
            }
            catch
            {
                _indexLoaded = true; // No reintentar en bucle si el archivo está corrupto
            }
        }
    }

    public string? GetPortraitImagePath(string? portholePath)
    {
        if (string.IsNullOrWhiteSpace(portholePath)) return null;

        string normPath = portholePath.Trim().Replace('/', '\\');
        string fileName = Path.GetFileName(normPath);
        if (string.IsNullOrEmpty(fileName)) return null;

        string localCacheFile = Path.Combine(_cacheDir, fileName);
        if (File.Exists(localCacheFile))
            return localCacheFile;

        return ExtractFromPack(normPath, localCacheFile);
    }

    public string? GetRaceIconPath(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return null;

        string code = cultureCode.Trim().ToLowerInvariant();
        if (!RaceFlagPaths.TryGetValue(code, out string? packRelPath))
            return null;

        string localCacheFile = Path.Combine(_cacheDir, $"race_{code}.png");
        if (File.Exists(localCacheFile))
            return localCacheFile;

        return ExtractFromPack(packRelPath, localCacheFile);
    }

    private string? ExtractFromPack(string packRelativePath, string destinationFile)
    {
        EnsurePackIndexLoaded();

        if (string.IsNullOrEmpty(_uiPackPath) || !_packIndex.TryGetValue(packRelativePath, out var entry))
            return null;

        lock (_lock)
        {
            if (File.Exists(destinationFile))
                return destinationFile;

            try
            {
                using var fs = File.OpenRead(_uiPackPath);
                fs.Seek(entry.Offset, SeekOrigin.Begin);
                byte[] raw = new byte[entry.Size];
                fs.ReadExactly(raw, 0, (int)entry.Size);

                byte[] data;
                if (entry.Comp == 1)
                {
                    using var decompressor = new Decompressor();
                    data = decompressor.Unwrap(raw.AsSpan(4)).ToArray();
                }
                else
                {
                    data = raw;
                }

                File.WriteAllBytes(destinationFile, data);
                return destinationFile;
            }
            catch
            {
                return null;
            }
        }
    }
}
