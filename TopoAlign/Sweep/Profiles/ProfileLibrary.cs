using Autodesk.Revit.DB;

namespace TopoAlign.Sweep.Profiles;

/// <summary>
/// The profile family types in a document, with the section read from each the first time it is asked for. Reading a
/// profile opens its family in the editor, so each is read once and kept.
/// </summary>
internal sealed class ProfileLibrary
{
    private readonly Document _document;
    private readonly Dictionary<string, (SectionProfile? Profile, string? Error)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProfileLibrary(Document document)
    {
        _document = document;
        Types = SectionProfileReader.ListProfileTypes(document);
    }

    public List<ProfileTypeInfo> Types { get; }

    /// <summary>The section of the named type, or null with the reason if the type is missing or its shape can't be read.</summary>
    public SectionProfile? Get(string name, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "No profile has been chosen.";
            return null;
        }

        if (!_cache.TryGetValue(name, out var cached))
        {
            var type = Types.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (type is null)
            {
                cached = (null, $"The profile '{name}' is not loaded in this project.");
            }
            else
            {
                var profile = SectionProfileReader.Read(_document, type.SymbolId, out var readError);
                cached = (profile, profile is null ? readError ?? "The profile could not be read." : null);
            }

            _cache[name] = cached;
        }

        error = cached.Error;
        return cached.Profile;
    }

    public SectionProfile? Get(string name) => Get(name, out _);
}
