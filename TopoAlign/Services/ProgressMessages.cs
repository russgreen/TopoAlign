namespace TopoAlign.Services;

/// <summary>
/// Sent from the Revit thread to the progress window to change what it shows.
/// A null member leaves the matching display value unchanged.
/// </summary>
internal sealed class ProgressUpdateMessage
{
    public string? Title { get; set; }
    public string? Message { get; set; }
    public double? Value { get; set; }
    public double? Maximum { get; set; }
    public bool? IsIndeterminate { get; set; }
}

/// <summary>
/// Sent from the progress window when the user asks to stop the operation.
/// </summary>
internal sealed class ProgressCancelMessage
{
}
