using Autodesk.Revit.DB;

namespace TopoAlign;

class FailureHandler : IFailuresPreprocessor
{
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
