using Autodesk.Revit.DB;

namespace TopoAlign;

class FailureHandler : IFailuresPreprocessor
{
    private readonly bool _rollBackOnError;

    public FailureHandler()
    {
    }

    /// <param name="rollBackOnError">
    /// True to undo the transaction quietly when Revit reports an error, instead of leaving Revit to show its error box
    /// (which for a shape edit offers only to reset the toposolid). The message is kept in <see cref="ErrorMessage"/>.
    /// </param>
    public FailureHandler(bool rollBackOnError)
    {
        _rollBackOnError = rollBackOnError;
    }

    public string ErrorMessage { get; set; } = "";
    public string ErrorSeverity { get; set; } = "";

    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        var failureMessages = failuresAccessor.GetFailureMessages();
        foreach (FailureMessageAccessor failureMessageAccessor in failureMessages)
        {
            // We're just deleting all of the warning level
            // failures and rolling back any others

            _ = failureMessageAccessor.GetFailureDefinitionId();
            try
            {
                ErrorMessage = failureMessageAccessor.GetDescriptionText();
            }
            catch
            {
                ErrorMessage = "Unknown Error";
            }

            //if (ErrorMessage.ToLower().Contains("highlighted geometry no longer determines a plane"))
            //{
            //    failuresAccessor.DeleteWarning(failureMessageAccessor);
            //}

            try
            {
                var failureSeverity = failureMessageAccessor.GetSeverity();
                ErrorSeverity = failureSeverity.ToString();
                if (_rollBackOnError && failureSeverity != FailureSeverity.Warning)
                {
                    return FailureProcessingResult.ProceedWithRollBack;
                }

                if (failureSeverity == FailureSeverity.Warning || failureSeverity == FailureSeverity.Error)
                {
                    failuresAccessor.DeleteWarning(failureMessageAccessor);
                }
                else
                {
                    return FailureProcessingResult.ProceedWithRollBack;
                }
            }
            catch
            {
            }
        }

        return FailureProcessingResult.Continue;
    }
}
