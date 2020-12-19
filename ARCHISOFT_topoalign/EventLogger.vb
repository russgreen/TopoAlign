Imports System.Diagnostics

<CLSCompliant(True)> _
Public Class EventLogger

    Dim recordlog As Boolean = True

    Public Sub New()
        'default constructor
        If My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\services\eventlog\Application\TopoAlign") Is Nothing Then
            recordlog = False
        End If
    End Sub

    '*************************************************************
    'NAME:          WriteToEventLog
    'PURPOSE:       Write to Event Log
    'PARAMETERS:    Entry - Value to Write
    '               AppName - Name of Client Application. Needed 
    '               because before writing to event log, you must 
    '               have a named EventLog source. 
    '               EventType - Entry Type, from EventLogEntryType 
    '               Structure e.g., EventLogEntryType.Warning, 
    '               EventLogEntryType.Error
    '               LogNam1e: Name of Log (System, Application; 
    '               Security is read-only) If you 
    '               specify a non-existent log, the log will be
    '               created
    'RETURNS:       True if successful
    '*************************************************************
    Public Function WriteEntry(ByVal entry As String, _
                        Optional ByVal eventType As EventLogEntryType = EventLogEntryType.Information, _
                        Optional ByVal appName As String = "TopoAlign", _
                                Optional ByVal logName As String = "Application") As Boolean

        If recordlog = False Then
            Return False
        Else
            Dim objEventLog As New EventLog

            Try

                'Register the Application as an Event Source
                If Not EventLog.SourceExists(appName) Then
                    EventLog.CreateEventSource(appName, logName)
                End If

                'log the entry
                objEventLog.Source = appName
                objEventLog.WriteEntry(entry, eventType)

                Return True

            Catch Ex As Exception

                Return False

            End Try
        End If

    End Function


End Class
