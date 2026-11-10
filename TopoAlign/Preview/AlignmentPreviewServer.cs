using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using TopoAlign.Alignments;

namespace TopoAlign.Preview;

/// <summary>
/// Draws the temporary lines of the pick preview in one plan view. Nothing is added to the model: the lines exist only while the
/// server is registered, which <see cref="AlignmentPickPreview"/> does for the length of a pick.
/// </summary>
internal sealed class AlignmentPreviewServer : IDirectContext3DServer
{
    private static readonly Guid ServiceGuid = new("6b6a5f3e-3d6b-4f5c-9d6e-0a8a2b1c7e41");

    private readonly object _lock = new();
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly ElementId _viewId;

    private List<PreviewLine> _lines = new();
    private double _elevation;
    private bool _dirty = true;
    private bool _reportedError;

    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private VertexFormat? _vertexFormat;
    private EffectInstance? _effect;
    private int _vertexCount;
    private int _lineCount;

    public AlignmentPreviewServer(ElementId viewId)
    {
        _viewId = viewId;
    }

    public Guid ServerId => _serverId;

    /// <summary>Replaces what is drawn. Takes effect the next time Revit repaints the view.</summary>
    public void SetLines(IEnumerable<PreviewLine> lines, double elevation)
    {
        lock (_lock)
        {
            _lines = lines.ToList();
            _elevation = elevation;
            _dirty = true;
        }
    }

    /// <summary>Frees the graphics buffers. Call once the server has been removed from Revit.</summary>
    public void Release()
    {
        lock (_lock)
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _vertexBuffer = null;
            _indexBuffer = null;
            _lineCount = 0;
            _vertexCount = 0;
        }
    }

    // ---- IExternalServer ----------------------------------------------------------------------------------------

    public Guid GetServerId() => _serverId;

    public ExternalServiceId GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;

    public string GetName() => "TopoAlign alignment pick preview";

    public string GetVendorId() => "TopoAlign";

    public string GetDescription() => "Temporary lines shown while picking the points of an alignment in plan.";

    public string GetApplicationId() => string.Empty;

    public string GetSourceId() => ServiceGuid.ToString();

    // ---- IDirectContext3DServer ---------------------------------------------------------------------------------

    public bool UsesHandles() => false;

    public bool CanExecute(View dBView) => dBView is not null && dBView.Id == _viewId;

    public bool UseInTransparentPass(View dBView) => true;

    public Outline GetBoundingBox(View dBView)
    {
        lock (_lock)
        {
            if (_lines.Count == 0)
            {
                return new Outline(XYZ.Zero, new XYZ(1.0, 1.0, 1.0));
            }

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var line in _lines)
            {
                minX = Math.Min(minX, Math.Min(line.From.X, line.To.X));
                minY = Math.Min(minY, Math.Min(line.From.Y, line.To.Y));
                maxX = Math.Max(maxX, Math.Max(line.From.X, line.To.X));
                maxY = Math.Max(maxY, Math.Max(line.From.Y, line.To.Y));
            }

            // padded so the box is never flat
            return new Outline(new XYZ(minX - 1.0, minY - 1.0, _elevation - 1.0), new XYZ(maxX + 1.0, maxY + 1.0, _elevation + 1.0));
        }
    }

    public void RenderScene(View dBView, DisplayStyle displayStyle)
    {
        try
        {
            lock (_lock)
            {
                if (_dirty)
                {
                    BuildBuffers();
                    _dirty = false;
                }

                if (_lineCount == 0 || _vertexBuffer is null || _indexBuffer is null || _vertexFormat is null || _effect is null)
                {
                    return;
                }

                DrawContext.FlushBuffer(_vertexBuffer, _vertexCount, _indexBuffer, _lineCount * 2, _vertexFormat, _effect,
                    PrimitiveType.LineList, 0, _lineCount);
            }
        }
        catch (Exception ex)
        {
            // never let a drawing problem escape into Revit's render loop; the preview just doesn't draw
            if (!_reportedError)
            {
                _reportedError = true;
                Serilog.Log.ForContext<AlignmentPreviewServer>().Warning(ex, "The alignment pick preview failed to draw");
            }
        }
    }

    private void BuildBuffers()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer = null;
        _vertexCount = 0;
        _lineCount = _lines.Count;

        if (_lineCount == 0)
        {
            return;
        }

        _vertexCount = _lineCount * 2;
        _vertexFormat = new VertexFormat(VertexFormatBits.PositionColored);
        _effect = new EffectInstance(VertexFormatBits.PositionColored);

        var vertexFloats = _vertexCount * VertexPositionColored.GetSizeInFloats();
        _vertexBuffer = new VertexBuffer(vertexFloats);
        _vertexBuffer.Map(vertexFloats);
        var vertices = _vertexBuffer.GetVertexStreamPositionColored();

        var indexShorts = _lineCount * IndexLine.GetSizeInShortInts();
        _indexBuffer = new IndexBuffer(indexShorts);
        _indexBuffer.Map(indexShorts);
        var indices = _indexBuffer.GetIndexStreamLine();

        var next = 0;
        foreach (var line in _lines)
        {
            var color = new ColorWithTransparency(line.Red, line.Green, line.Blue, 0);
            vertices.AddVertex(new VertexPositionColored(new XYZ(line.From.X, line.From.Y, _elevation), color));
            vertices.AddVertex(new VertexPositionColored(new XYZ(line.To.X, line.To.Y, _elevation), color));
            indices.AddLine(new IndexLine(next, next + 1));
            next += 2;
        }

        _vertexBuffer.Unmap();
        _indexBuffer.Unmap();
    }
}