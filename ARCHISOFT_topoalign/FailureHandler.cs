using Autodesk.Revit.DB;

namespace ARCHISOFT_topoalign
{
    public class FailureHandler : IFailuresPreprocessor
    {
        public string ErrorMessage
        {
            get
            {
                return m_ErrorMessage;
            }

            set
            {
                m_ErrorMessage = value;
            }
        }

        private string m_ErrorMessage;

        public string ErrorSeverity
        {
            get
            {
                return m_ErrorSeverity;
            }

            set
            {
                m_ErrorSeverity = value;
            }
        }

        private string m_ErrorSeverity;

        public FailureHandler()
        {
            ErrorMessage = "";
            ErrorSeverity = "";
        }

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
}