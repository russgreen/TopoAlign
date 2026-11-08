using Serilog;
using System.IO;
using System.Text.Json;

namespace TopoAlign.Sweep;

/// <summary>Saves and loads assemblies as one JSON file each, in a folder under the user's AppData.</summary>
internal sealed class AssemblyStore
{
    private static readonly ILogger log = Log.ForContext<AssemblyStore>();

    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public AssemblyStore() : this(DefaultFolder)
    {
    }

    /// <param name="folder">Where the files are kept; created when something is saved.</param>
    public AssemblyStore(string folder)
    {
        Folder = folder;
    }

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TopoAlign", "Assemblies");

    public string Folder { get; }

    /// <summary>Every readable assembly, by name. A file that can't be read is skipped and logged.</summary>
    public List<SweepAssembly> LoadAll()
    {
        var assemblies = new List<SweepAssembly>();
        if (!Directory.Exists(Folder))
        {
            return assemblies;
        }

        foreach (var file in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                var assembly = JsonSerializer.Deserialize<SweepAssembly>(File.ReadAllText(file), jsonOptions);
                if (assembly is null || string.IsNullOrWhiteSpace(assembly.Name))
                {
                    log.Warning("Assembly file {file} has no name and was skipped", file);
                    continue;
                }

                assembly.Parts ??= new List<AssemblyPart>();
                assemblies.Add(assembly);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                log.Warning(ex, "Assembly file {file} could not be read and was skipped", file);
            }
        }

        return assemblies.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void Save(SweepAssembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        if (string.IsNullOrWhiteSpace(assembly.Name))
        {
            throw new ArgumentException("An assembly needs a name.", nameof(assembly));
        }

        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathFor(assembly.Name), JsonSerializer.Serialize(assembly, jsonOptions));
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>The file for an assembly: its name with anything a file name can't hold replaced.</summary>
    internal string PathFor(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Trim().Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
        return Path.Combine(Folder, clean + ".json");
    }
}
