Public Class FailureHandler
    Implements IFailuresPreprocessor

    Public Property ErrorMessage() As String
        Get
            Return m_ErrorMessage
        End Get
        Set(value As String)
            m_ErrorMessage = Value
        End Set
    End Property
    Private m_ErrorMessage As String
    Public Property ErrorSeverity() As String
        Get
            Return m_ErrorSeverity
        End Get
        Set(value As String)
            m_ErrorSeverity = Value
        End Set
    End Property
    Private m_ErrorSeverity As String

    Public Sub New()
        ErrorMessage = ""
        ErrorSeverity = ""
    End Sub

    Public Function PreprocessFailures(failuresAccessor As FailuresAccessor) As FailureProcessingResult Implements IFailuresPreprocessor.PreprocessFailures
        Dim failureMessages As IList(Of FailureMessageAccessor) = failuresAccessor.GetFailureMessages()

        For Each failureMessageAccessor As FailureMessageAccessor In failureMessages
            ' We're just deleting all of the warning level
            ' failures and rolling back any others

            Dim id As FailureDefinitionId = failureMessageAccessor.GetFailureDefinitionId()

            Try
                ErrorMessage = failureMessageAccessor.GetDescriptionText()
            Catch
                ErrorMessage = "Unknown Error"
            End Try

            Try
                Dim failureSeverity__1 As FailureSeverity = failureMessageAccessor.GetSeverity()

                ErrorSeverity = failureSeverity__1.ToString()

                If failureSeverity__1 = FailureSeverity.Warning OrElse failureSeverity__1 = FailureSeverity.[Error] Then
                    failuresAccessor.DeleteWarning(failureMessageAccessor)
                Else
                    Return FailureProcessingResult.ProceedWithRollBack
                End If
            Catch
            End Try
        Next
        Return FailureProcessingResult.[Continue]
    End Function

End Class
