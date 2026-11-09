using TopoAlign.Grading;

namespace TopoAlign.Tests;

/// <summary>Finds new points that are too close to ones already kept.</summary>
public class PlanIndexTests : TopoAlignTest
{
    [Test]
    public async Task APointWithinTheDistance_IsNear_EvenAcrossACellBoundary()
    {
        var index = new PlanIndex(0.05);
        index.Add(0.0499, 10.0);

        await Assert.That(index.HasNear(0.0501, 10.0)).IsTrue();
        await Assert.That(index.HasNear(0.2, 10.0)).IsFalse();
    }

    [Test]
    public async Task TryAdd_RefusesANearPoint_AndKeepsAFarOne()
    {
        var index = new PlanIndex(0.05);

        await Assert.That(index.TryAdd(-3.0, -3.0)).IsTrue();
        await Assert.That(index.TryAdd(-3.01, -3.01)).IsFalse();
        await Assert.That(index.TryAdd(-3.2, -3.0)).IsTrue();
    }
}
