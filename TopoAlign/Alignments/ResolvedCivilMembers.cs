using System;
using System.Reflection;

namespace TopoAlign.Alignments;

internal sealed class ResolvedCivilMembers
{
    public Type? Alignment;
    public Type? AlignmentData;
    public Type? Label;
    public Type? LabelSetOptions;

    public MethodInfo? Get;
    public MethodInfo? IsValid;
    public MethodInfo? CreateAlignment;
    public MethodInfo? UpdateAlignment;
    public MethodInfo? GetPointAtStation;
    public MethodInfo? GetHCurveTangentAtStation;
    public MethodInfo? GetDisplayedHorizontalCurveEndpoints;
    public MethodInfo? GetJson;
    public MethodInfo? GetAlignmentLength;
    public PropertyInfo? DataJson;
    public PropertyInfo? DataDisplayedStartDistance;
    public PropertyInfo? DataDisplayedEndDistance;
    public MethodInfo? LabelCreateSet;
    public MethodInfo? LabelIsValidType;
    public MethodInfo? LabelIsRecommendedTypeForSet;
    public MethodInfo? LabelCreateRecommendedTypeForSet;
}
