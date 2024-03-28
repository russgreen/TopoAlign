using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Octokit;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    Target Installer => _ => _
    .DependsOn(Sign)
    .Executes(() =>
    {

    });
}