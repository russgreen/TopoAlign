using Microsoft.AppCenter;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign
{
    public static class AppCenterHelper
    {
        public static void SetupCrashHandler()
        {
            AppCenter.LogLevel = LogLevel.Verbose;
            System.Windows.Forms.Application.ThreadException += (sender, args) => { Crashes.TrackError(args.Exception); };
            Crashes.ShouldAwaitUserConfirmation = () =>
            {
                // Build your own UI to ask for user consent here. SDK doesn't provide one by default.     
                var dialog = new DialogUserConfirmation();
                dialog.ShowDialog();
                Crashes.NotifyUserConfirmation(dialog.ClickResult);

                // Return true if you built a UI for user consent and are waiting for user input on that custom UI, otherwise false.     
                return true;
            };

            AppCenter.Start("c26c8f38-0aad-44c7-9064-478429495727", typeof(Crashes));
        }
    }
}
