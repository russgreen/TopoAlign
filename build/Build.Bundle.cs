using Fallout.Common;
using Fallout.Common.Git;
using Fallout.Solutions;
using Serilog;
using System.Collections.Generic;
using System.IO;

partial class Build
{
    Target Bundle => _ => _
    .TriggeredBy(Sign)
    .Executes(() =>
    {
        var bundleDirectory = RootDirectory / "Bundle" / "RG tools Topo Align.bundle";
        var bundleContentsDirectory = bundleDirectory / "Contents";
        var bundlePackageContents = bundleDirectory / "PackageContents.xml";
        var topoAlignProjectFile = Path.Combine(RootDirectory, "TopoAlign", "TopoAlign.csproj");

        foreach (var configuration in GlobBuildConfigurations())
        {
            if (!configuration.StartsWith("Release"))
            {
                continue;
            }

            if (configuration == "Release R21")
            {
                Log.Warning("Skipping configuration {configuration}. This version is no longer supported.", configuration);
                continue;
            }

            var projectBinDirectory = Path.Combine(RootDirectory, "TopoAlign", "bin", configuration);
            Log.Information("Bundle source directory: {directory}", projectBinDirectory);

            var destinationDirectory = Path.Combine(bundleContentsDirectory, "2022");

            var configurationToDirectoryMap = new Dictionary<string, string>
            {
                { "Release R22", "2022" },
                { "Release R23", "2023" },
                { "Release R24", "2024" },
                { "Release R25", "2025" },
                { "Release R26", "2026" },
                { "Release R27", "2027" }
            };

            if (configurationToDirectoryMap.TryGetValue(configuration, out var directoryName))
            {
                destinationDirectory = Path.Combine(bundleContentsDirectory, directoryName);
            }

            Log.Information("Bundle destination directory: {directory}", destinationDirectory);

            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(projectBinDirectory))
            {
                var targetFile = Path.Combine(destinationDirectory, Path.GetFileName(file));

                Log.Information("Copy file {file} to {target}", file, targetFile);

                File.Copy(file, targetFile, true);
            }
        }

        if (File.Exists(bundlePackageContents))
        {
            var packageContents = new _build.ApplicationPackage();
            packageContents.LoadFromXml(bundlePackageContents);

            var version = GetProjectVersion(topoAlignProjectFile);

            packageContents.AppVersion = version;
            packageContents.FriendlyVersion = version;

            //TODO check all versions exist and create new components if not
            foreach (var component in packageContents.Components)
            {
                component.ComponentEntry.Version = version;
            }

            packageContents.SaveToXml(bundlePackageContents);
        }

        var bundleZip = $"{bundleDirectory}.zip";
        if (File.Exists(bundleZip))
        {
            File.Delete(bundleZip);
        }
        System.IO.Compression.ZipFile.CreateFromDirectory(bundleDirectory, bundleZip);
    });
}
