using TopoAlign.Alignments.Plan;

namespace TopoAlign.Alignments;

/// <summary>
/// Identifies one T-junction. A side road can have a junction at each end, so the side alignment alone isn't enough: the key is
/// the side alignment's unique id plus the point where it meets the main road, to the nearest hundredth of a foot.
/// </summary>
internal static class TJunctionKey
{
    private const char Separator = '|';

    public static string Make(string sideUniqueId, PlanPoint junction)
    {
        return string.Join(Separator.ToString(), sideUniqueId,
            junction.X.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            junction.Y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>The unique id of the side alignment the key was made for. Also right for the older keys, which were just that id.</summary>
    public static string SideUniqueId(string key)
    {
        var at = key.IndexOf(Separator);
        return at < 0 ? key : key.Substring(0, at);
    }

    /// <summary>True if the key belongs to the side alignment, whether it is an older key or one with a junction point.</summary>
    public static bool IsFor(string key, string sideUniqueId) => SideUniqueId(key) == sideUniqueId;

    /// <summary>A short, repeatable tag for the junction, used to keep the names of its kerb returns apart.</summary>
    public static string Short(string key)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in key)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return (hash & 0xFFFFu).ToString("X4");
        }
    }
}
