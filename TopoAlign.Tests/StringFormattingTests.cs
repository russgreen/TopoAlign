using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Helpers;

namespace TopoAlign.Tests;

public class StringFormattingTests : TopoAlignTest
{
    [Test]
    public async Task RealString_RoundsToTwoDecimals()
    {
        string whole = null, rounded = null, negative = null;
        WithInvariantCulture(() =>
        {
            whole = StringFormatting.RealString(3);
            rounded = StringFormatting.RealString(1.2345);
            negative = StringFormatting.RealString(-0.5);
        });

        await Assert.That(whole).IsEqualTo("3");
        await Assert.That(rounded).IsEqualTo("1.23");
        await Assert.That(negative).IsEqualTo("-0.5");
    }

    [Test]
    public async Task HashString_KeepsNineDecimals()
    {
        string third = null, whole = null;
        WithInvariantCulture(() =>
        {
            third = StringFormatting.HashString(1.0 / 3.0);
            whole = StringFormatting.HashString(2.0);
        });

        await Assert.That(third).IsEqualTo("0.333333333");
        await Assert.That(whole).IsEqualTo("2");
    }

    [Test]
    public async Task HashString_Point()
    {
        string text = null;
        WithInvariantCulture(() => text = StringFormatting.HashString(new XYZ(1, 2.5, -3)));

        await Assert.That(text).IsEqualTo("(1,2.5,-3)");
    }

    [Test]
    public async Task PointString_Xyz()
    {
        string commas = null, spaces = null;
        WithInvariantCulture(() =>
        {
            commas = StringFormatting.PointString(new XYZ(1, 2.456, 3));
            spaces = StringFormatting.PointString(new XYZ(1, 2.456, 3), true);
        });

        await Assert.That(commas).IsEqualTo("(1,2.46,3)");
        await Assert.That(spaces).IsEqualTo("1 2.46 3");
    }

    [Test]
    public async Task PointString_Uv()
    {
        string commas = null, spaces = null;
        WithInvariantCulture(() =>
        {
            commas = StringFormatting.PointString(new UV(1.5, 2));
            spaces = StringFormatting.PointString(new UV(1.5, 2), true);
        });

        await Assert.That(commas).IsEqualTo("(1.5,2)");
        await Assert.That(spaces).IsEqualTo("1.5 2");
    }

    [Test]
    public async Task PointArrayString_JoinsPoints()
    {
        string commas = null, spaces = null;
        var points = new[] { new XYZ(1, 2, 3), new XYZ(4, 5, 6) };
        WithInvariantCulture(() =>
        {
            commas = StringFormatting.PointArrayString(points);
            spaces = StringFormatting.PointArrayString(points, true);
        });

        await Assert.That(commas).IsEqualTo("(1,2,3), (4,5,6)");
        await Assert.That(spaces).IsEqualTo("1 2 3 4 5 6");
    }

    [Test]
    public async Task PointArrayString_NoPoints_IsEmpty()
    {
        await Assert.That(StringFormatting.PointArrayString(Array.Empty<XYZ>())).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task BoundingBoxString_Uv()
    {
        string commas = null, spaces = null;
        var box = new BoundingBoxUV(0, 0, 1, 2);
        WithInvariantCulture(() =>
        {
            commas = StringFormatting.BoundingBoxString(box);
            spaces = StringFormatting.BoundingBoxString(box, true);
        });

        await Assert.That(commas).IsEqualTo("((0,0),(1,2))");
        await Assert.That(spaces).IsEqualTo("0 0 1 2");
    }

    [Test]
    public async Task AngleString_ConvertsRadiansToDegrees()
    {
        string half = null, quarter = null;
        WithInvariantCulture(() =>
        {
            half = StringFormatting.AngleString(Math.PI);
            quarter = StringFormatting.AngleString(Math.PI / 2);
        });

        await Assert.That(half).IsEqualTo("180 degrees");
        await Assert.That(quarter).IsEqualTo("90 degrees");
    }

    [Test]
    public async Task MmString_ConvertsFeetToWholeMillimetres()
    {
        await Assert.That(StringFormatting.MmString(1.0)).IsEqualTo("305 mm");
        await Assert.That(StringFormatting.MmString(0.00328084)).IsEqualTo("1 mm");
        await Assert.That(StringFormatting.MmString(0.0)).IsEqualTo("0 mm");
    }

    [Test]
    [Arguments(1, ":")]
    [Arguments(5, ":")]
    [Arguments(0, ".")]
    [Arguments(-1, ".")]
    public async Task DotOrColon(int count, string expected)
    {
        await Assert.That(StringFormatting.DotOrColon(count)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(1, "")]
    [Arguments(0, "s")]
    [Arguments(2, "s")]
    public async Task PluralSuffix(int count, string expected)
    {
        await Assert.That(StringFormatting.PluralSuffix(count)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(1, "y")]
    [Arguments(0, "ies")]
    [Arguments(3, "ies")]
    public async Task PluralSuffixY(int count, string expected)
    {
        await Assert.That(StringFormatting.PluralSuffixY(count)).IsEqualTo(expected);
    }

    [Test]
    public async Task ListForgeTypeIds_RunsWithoutThrowing()
    {
        var exception = Catch(StringFormatting.ListForgeTypeIds);

        await Assert.That(exception).IsNull();
    }
}
