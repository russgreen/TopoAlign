using TopoAlign.Alignments.Profile;

namespace TopoAlign.Tests;

/// <summary>Refitting a side road profile so it finishes at the main road level where the roads meet.</summary>
public class ProfileFitToJunctionTests : TopoAlignTest
{
    private static ProfileVertex V(double distance, double elevation, double curveLength = 0.0) => new ProfileVertex(distance, elevation, curveLength);

    private static readonly ProfileVertex[] Original = { V(0.0, 10.0), V(50.0, 11.0, 10.0), V(100.0, 12.0) };

    [Test]
    public async Task Extended_at_the_end_finishes_at_the_junction_level()
    {
        var result = ProfileEditing.FitToJunction(Original, 100.0, 120.0, false, 9.5);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[2].Distance).IsEqualTo(120.0);
        await Assert.That(result[2].Elevation).IsEqualTo(9.5);
        await Assert.That(ProfileEditing.Validate(result, 120.0).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Trimmed_at_the_end_drops_stations_beyond_it()
    {
        var result = ProfileEditing.FitToJunction(Original, 100.0, 40.0, false, 10.8);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[1].Distance).IsEqualTo(40.0);
        await Assert.That(result[1].Elevation).IsEqualTo(10.8);
        await Assert.That(ProfileEditing.Validate(result, 40.0).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Extended_at_the_start_shifts_the_stations_and_starts_at_the_junction_level()
    {
        var result = ProfileEditing.FitToJunction(Original, 100.0, 120.0, true, 9.5);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Distance).IsEqualTo(0.0);
        await Assert.That(result[0].Elevation).IsEqualTo(9.5);
        await Assert.That(result[1].Distance).IsEqualTo(70.0);
        await Assert.That(result[2].Distance).IsEqualTo(120.0);
        await Assert.That(result[2].Elevation).IsEqualTo(12.0);
        await Assert.That(ProfileEditing.Validate(result, 120.0).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Trimmed_at_the_start_drops_stations_before_it()
    {
        var result = ProfileEditing.FitToJunction(Original, 100.0, 40.0, true, 11.5);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Elevation).IsEqualTo(11.5);
        await Assert.That(result[1].Distance).IsEqualTo(40.0);
        await Assert.That(result[1].Elevation).IsEqualTo(12.0);
    }
}
