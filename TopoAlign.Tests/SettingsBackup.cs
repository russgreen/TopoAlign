using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using Settings = TopoAlign.Models.Settings;

namespace TopoAlign.Tests;

/// <summary>
/// Protects the user's real TopoAlign settings (%AppData%\TopoAlign\settings.json and the old
/// HKCU\SOFTWARE\RGTools\TopoAlign key). Construction points <see cref="Settings.FilePath"/> and the legacy registry
/// key at throwaway locations so the test starts from the defaults; Dispose puts the real locations back and deletes
/// the throwaway ones, so nothing a test saves ever reaches the user's own settings.
/// </summary>
internal sealed class SettingsBackup : IDisposable
{
    private readonly string _previousFilePath;
    private readonly string _previousLegacyKeyPath;
    private readonly string _directory;

    public SettingsBackup()
    {
        _previousFilePath = Settings.FilePath;
        _previousLegacyKeyPath = Settings.LegacyRegistryKeyPath;

        var id = Guid.NewGuid().ToString("N");
        _directory = Path.Combine(Path.GetTempPath(), "TopoAlignTests_" + id);
        Settings.FilePath = Path.Combine(_directory, "TopoAlign", "settings.json");
        Settings.LegacyRegistryKeyPath = @"SOFTWARE\RGTools\TopoAlignTests_" + id;
    }

    /// <summary>Writes a raw string value into the JSON file, creating the file if needed.</summary>
    public static void SetRaw(string name, string value)
    {
        var root = File.Exists(Settings.FilePath) ? (JsonNode.Parse(File.ReadAllText(Settings.FilePath)) as JsonObject ?? new JsonObject()) : new JsonObject();
        root[name] = value;
        Directory.CreateDirectory(Path.GetDirectoryName(Settings.FilePath)!);
        File.WriteAllText(Settings.FilePath, root.ToJsonString());
    }

    /// <summary>Writes the whole settings file as given (for malformed-file tests).</summary>
    public static void WriteFile(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings.FilePath)!);
        File.WriteAllText(Settings.FilePath, contents);
    }

    /// <summary>The value as it appears in the JSON file (<c>true</c>, <c>3.25</c>, <c>"text"</c>), or null if absent.</summary>
    public static string? GetRaw(string name)
    {
        if (!File.Exists(Settings.FilePath))
        {
            return null;
        }

        return (JsonNode.Parse(File.ReadAllText(Settings.FilePath)) as JsonObject)?[name]?.ToJsonString();
    }

    public static bool FileExists() => File.Exists(Settings.FilePath);

    /// <summary>Writes a value into the (throwaway) legacy registry key, as older versions did.</summary>
    public static void SetLegacy(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Settings.LegacyRegistryKeyPath, true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public static bool LegacyKeyExists()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Settings.LegacyRegistryKeyPath, false);
        return key is not null;
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(Settings.LegacyRegistryKeyPath, false);
        Settings.FilePath = _previousFilePath;
        Settings.LegacyRegistryKeyPath = _previousLegacyKeyPath;

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
