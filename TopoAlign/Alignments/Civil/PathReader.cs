using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TopoAlign.Alignments.Civil;

/// <summary>
/// Reads points and directions along one alignment. Autodesk's public methods are looked up once and reused, and the
/// stations are kept inside the alignment's displayed range (the methods throw outside it).
/// </summary>
internal sealed class PathReader
{
    private readonly object _alignment;
    private readonly MethodInfo _pointAtStation;
    private readonly MethodInfo _tangentAtStation;
    private readonly MethodInfo? _curveEndpoints;
    private readonly Transform _toModel;

    public PathReader(Element alignmentElement)
    {
        if (alignmentElement is null)
        {
            throw new ArgumentNullException(nameof(alignmentElement));
        }

        if (!AlignmentApi.IsValid(alignmentElement))
        {
            throw new ArgumentException("The element is not a valid alignment.", nameof(alignmentElement));
        }

        // GetPointAtStation returns points in Revit coordinates (the active project location's transform is applied), but
        // GetHCurveTangentAtStation returns the direction in the alignment's own coordinates. In a project whose location is
        // rotated (true north, shared coordinates) the two disagree, so the tangent is brought across here.
        _toModel = alignmentElement.Document.ActiveProjectLocation.GetTotalTransform();

        _alignment = AlignmentApi.GetAlignment(alignmentElement)
                     ?? throw new InvalidOperationException("The alignment could not be read.");

        _pointAtStation = CivilAlignmentsBinding.GetPointAtStation
                          ?? throw new MissingMethodException("Alignment.GetPointAtStation was not found.");
        _tangentAtStation = CivilAlignmentsBinding.GetHCurveTangentAtStation
                            ?? throw new MissingMethodException("Alignment.GetHCurveTangentAtStation was not found.");
        _curveEndpoints = CivilAlignmentsBinding.GetDisplayedHorizontalCurveEndpoints;

        StartStation = AlignmentApi.ReadProperty<double>(_alignment, "DisplayedStartStation");
        EndStation = AlignmentApi.ReadProperty<double>(_alignment, "DisplayedEndStation");
    }

    /// <summary>First and last displayed station, in feet.</summary>
    public double StartStation { get; }
    public double EndStation { get; }

    /// <summary>The 3D point on the alignment at a station (plan position and profile elevation), in model coordinates.</summary>
    public XYZ PointAt(double station) => Invoke(_pointAtStation, station);

    /// <summary>The horizontal direction of travel at a station as a unit vector (Z is 0).</summary>
    public XYZ HorizontalTangentAt(double station) => ToModelDirection(Invoke(_tangentAtStation, station));

    /// <summary>The direction as seen in Revit coordinates, flattened to the horizontal and made a unit vector.</summary>
    private XYZ ToModelDirection(XYZ direction)
    {
        var rotated = _toModel.OfVector(direction);
        var flat = new XYZ(rotated.X, rotated.Y, 0.0);
        return flat.GetLength() < 1e-12 ? direction : flat.Normalize();
    }

    /// <summary>Stations where one horizontal curve ends and the next begins.</summary>
    public IList<double> HorizontalCurveStations()
    {
        var stations = new List<double>();
        if (_curveEndpoints?.Invoke(_alignment, null) is System.Collections.IEnumerable endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                stations.Add(AlignmentApi.ReadProperty<double>(endpoint, "Station"));
            }
        }

        return stations;
    }

    private XYZ Invoke(MethodInfo method, double station)
    {
        var clamped = Math.Max(StartStation, Math.Min(EndStation, station));
        try
        {
            return (XYZ?)method.Invoke(_alignment, new object[] { clamped })
                   ?? throw new InvalidOperationException("The alignment returned no value at the station.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
        }
    }
}
