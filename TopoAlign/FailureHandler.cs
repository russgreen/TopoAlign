using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            var id = failureMessageAccessor.GetFailureDefinitionId();
            try
            {
                ErrorMessage = failureMessageAccessor.GetDescriptionText();
            }
            catch
            {
                ErrorMessage = "Unknown Error";
            }

            try
            {
                var failureSeverity__1 = failureMessageAccessor.GetSeverity();
                ErrorSeverity = failureSeverity__1.ToString();
                if (failureSeverity__1 == FailureSeverity.Warning || failureSeverity__1 == FailureSeverity.Error)
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
