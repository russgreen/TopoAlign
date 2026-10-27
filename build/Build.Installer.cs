using Fallout.Common;
using Fallout.Common.Git;
using Fallout.Common.Tools.DotNet;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// Standard installer target. One partial class file per repo, identical apart from the project / output names.
// Multi package repos: call BuildInstaller for each .Setup.wixproj and SignMSI for each <Product>-SetupFiles folder.
partial class Build
{
    Target Installer => _ => _
        .TriggeredBy(Bundle)
        //.OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            // Version home: <Version> in the add-in csproj, same as the wixproj fallback.
            var version = GetProjectVersion(Path.Combine(RootDirectory, "TopoAlign", "TopoAlign.csproj"));
            var wixProject = Path.Combine(RootDirectory, "installer", "TopoAlign.Setup.wixproj");

            Log.Information("WiX project : {wixProject}", wixProject);
            Log.Information("Version : {version}", version);

            // Always pass ProductVersion so the MSI name, the Package version and the signing glob can never disagree.
            DotNetBuild(settings => settings
                .SetProjectFile(wixProject)
                .SetConfiguration("Release")
                .SetProperty("ProductVersion", version)
                .SetVerbosity(DotNetVerbosity.minimal));

            SignMSI(Path.Combine(RootDirectory, "installer", "TopoAlign-SetupFiles"), version);
        });

    static string GetProjectVersion(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);
        var propertyGroup = doc.Root?
            .Elements()
            .FirstOrDefault(x => x.Name.LocalName == "PropertyGroup");

        var versionProperties = new[] { "Version", "VersionPrefix", "ApplicationVersion", "FileVersion", "InformationalVersion" };

        var versionElement = propertyGroup?
            .Elements()
            .FirstOrDefault(x => versionProperties.Contains(x.Name.LocalName) && !string.IsNullOrWhiteSpace(x.Value));

        return versionElement?.Value ?? "1.0.0";
    }

    static void SignMSI(string setupFilesDirectory, string version)
    {
        // A missing MSI after a successful build is a failure, never a silent skip.
        var msiPath = Directory.GetFiles(setupFilesDirectory, $"*.{version}.msi").FirstOrDefault()
            ?? throw new FileNotFoundException($"No *.{version}.msi found in {setupFilesDirectory} to sign.");

        Log.Information("Signing {msiPath}", msiPath);
        SignFiles(new System.Collections.Generic.List<string> { msiPath });
    }
}
