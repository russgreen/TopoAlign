using System;
using System.Diagnostics;

namespace ARCHISOFT_topoalign
{
    [CLSCompliant(true)]
    public class EventLogger
    {
        private bool recordlog = true;

        public EventLogger()
        {
            // default constructor
            if (My.MyProject.Computer.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\eventlog\Application\TopoAlign") is null)
            {
                recordlog = false;
            }
        }

        // *************************************************************
        // NAME:          WriteToEventLog
        // PURPOSE:       Write to Event Log
        // PARAMETERS:    Entry - Value to Write
        // AppName - Name of Client Application. Needed 
        // because before writing to event log, you must 
        // have a named EventLog source. 
        // EventType - Entry Type, from EventLogEntryType 
        // Structure e.g., EventLogEntryType.Warning, 
        // EventLogEntryType.Error
        // LogNam1e: Name of Log (System, Application; 
        // Security is read-only) If you 
        // specify a non-existent log, the log will be
        // created
        // RETURNS:       True if successful
        // *************************************************************
        public bool WriteEntry(string entry, EventLogEntryType eventType = EventLogEntryType.Information, string appName = "TopoAlign", string logName = "Application")


        {
            if (recordlog == false)
            {
                return false;
            }
            else
            {
                var objEventLog = new EventLog();
                try
                {

                    // Register the Application as an Event Source
                    if (!EventLog.SourceExists(appName))
                    {
                        EventLog.CreateEventSource(appName, logName);
                    }

                    // log the entry
                    objEventLog.Source = appName;
                    objEventLog.WriteEntry(entry, eventType);
                    return true;
                }
                catch (Exception Ex)
                {
                    return false;
                }
            }
        }
    }
}