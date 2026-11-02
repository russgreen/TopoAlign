using Microsoft.Win32;

namespace TopoAlign.Tests;

/// <summary>
/// Protects the user's real TopoAlign settings (HKCU\SOFTWARE\RGTools\TopoAlign). Construction saves the current
/// values and clears the key so the test starts from the defaults; Dispose puts everything back exactly as it was.
/// </summary>
internal sealed class RegistryBackup : IDisposable
{
    public const string KeyPath = @"SOFTWARE\RGTools\TopoAlign";

    private readonly bool _existed;
    private readonly List<(string Name, object Value, RegistryValueKind Kind)> _values = new();

    public RegistryBackup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
        _existed = key is not null;

        if (key is not null)
        {
            foreach (var name in key.GetValueNames())
            {
                _values.Add((name, key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), key.GetValueKind(name)));
            }
        }

        Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
    }

    /// <summary>Writes a raw string value, creating the key if needed.</summary>
    public static void SetRaw(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public static string GetRaw(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
        return key?.GetValue(name) as string;
    }

    public static bool KeyExists()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
        return key is not null;
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);

        if (!_existed)
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        foreach (var (name, value, kind) in _values)
        {
            key.SetValue(name, value, kind);
        }
    }
}
