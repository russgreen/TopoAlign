using static TopoAlign.Tests.TestKit;
using System.IO;
using Settings = TopoAlign.Models.Settings;
using Autodesk.Revit.DB;
using TopoAlign.Helpers;
using TopoAlign.Models;

namespace TopoAlign.Tests;

public class SettingsTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    // ---- defaults -----------------------------------------------------------------------------------------------

    [Test]
    public async Task Defaults_MatchTheDocumentedStartingValues()
    {
        var settings = new Settings();

        await Assert.That(settings.SingleElement).IsTrue();
        await Assert.That(settings.CleanTopoPoints).IsTrue();
        await Assert.That(settings.TopFace).IsTrue();
        await Assert.That(settings.CreateAlignmentReverseDirection).IsFalse();
        await Assert.That(settings.CreateAlignmentDeleteSourceLines).IsFalse();
        await Assert.That((double)settings.CreateAlignmentStartStation).IsEqualTo(0.0).Within(Tol);
        await Assert.That((double)settings.DivideEdgeDistance).IsEqualTo(5000 / 304.8).Within(1e-6);
        await Assert.That((double)settings.VerticalOffset).IsEqualTo(50 / 304.8).Within(1e-6);
    }

    [Test]
    public async Task LoadSettings_NoFile_KeepsTheDefaults()
    {
        using var backup = new SettingsBackup();
        var settings = new Settings();

        settings.LoadSettings();

        await Assert.That(settings.SingleElement).IsTrue();
        await Assert.That((double)settings.DivideEdgeDistance).IsEqualTo(5000 / 304.8).Within(1e-6);
        await Assert.That(SettingsBackup.FileExists()).IsFalse();
    }

    // ---- saving and loading -------------------------------------------------------------------------------------

    [Test]
    public async Task SaveSettings_CreatesTheFileAndLoadSettingsReadsItBack()
    {
        using var backup = new SettingsBackup();
        var saved = new Settings
        {
            SingleElement = false,
            CleanTopoPoints = false,
            TopFace = false,
            CreateAlignmentReverseDirection = true,
            CreateAlignmentDeleteSourceLines = true,
            CreateAlignmentStartStation = 1234.5m,
            DivideEdgeDistance = 3.25m,
            VerticalOffset = 0.125m
        };

        saved.SaveSettings();
        var loaded = new Settings();
        loaded.LoadSettings();

        await Assert.That(SettingsBackup.FileExists()).IsTrue();
        await Assert.That(loaded.SingleElement).IsFalse();
        await Assert.That(loaded.CleanTopoPoints).IsFalse();
        await Assert.That(loaded.TopFace).IsFalse();
        await Assert.That(loaded.CreateAlignmentReverseDirection).IsTrue();
        await Assert.That(loaded.CreateAlignmentDeleteSourceLines).IsTrue();
        await Assert.That(loaded.CreateAlignmentStartStation).IsEqualTo(1234.5m);
        await Assert.That(loaded.DivideEdgeDistance).IsEqualTo(3.25m);
        await Assert.That(loaded.VerticalOffset).IsEqualTo(0.125m);
    }

    [Test]
    public async Task SaveSettings_CalledTwice_OverwritesThePreviousValues()
    {
        using var backup = new SettingsBackup();
        var settings = new Settings { SingleElement = false };
        settings.SaveSettings();

        settings.SingleElement = true;
        settings.SaveSettings();
        var loaded = new Settings();
        loaded.LoadSettings();

        await Assert.That(loaded.SingleElement).IsTrue();
    }

    [Test]
    public async Task SaveSettings_WritesTypedJsonValues()
    {
        using var backup = new SettingsBackup();

        new Settings { TopFace = false, DivideEdgeDistance = 3.25m, SweepOutput = "Family" }.SaveSettings();

        await Assert.That(SettingsBackup.GetRaw("TopFace")).IsEqualTo("false");
        await Assert.That(SettingsBackup.GetRaw("SingleElement")).IsEqualTo("true");
        await Assert.That(SettingsBackup.GetRaw("DivideEdgeDistance")).IsEqualTo("3.25");
        await Assert.That(SettingsBackup.GetRaw("SweepOutput")).IsEqualTo("\"Family\"");
    }

    [Test]
    public async Task SaveSettings_LeavesNoTemporaryFileBehind()
    {
        using var backup = new SettingsBackup();

        var settings = new Settings();
        settings.SaveSettings();
        settings.SaveSettings();

        await Assert.That(File.Exists(Settings.FilePath + ".tmp")).IsFalse();
    }

    [Test]
    public async Task LoadSettings_PartialFile_KeepsDefaultsForMissingValues()
    {
        using var backup = new SettingsBackup();
        SettingsBackup.SetRaw("SingleElement", "False");
        var settings = new Settings();

        settings.LoadSettings();

        await Assert.That(settings.SingleElement).IsFalse();
        await Assert.That(settings.CleanTopoPoints).IsTrue();
        await Assert.That((double)settings.DivideEdgeDistance).IsEqualTo(5000 / 304.8).Within(1e-6);
    }

    [Test]
    public async Task LoadSettings_UnparseableNumber_KeepsTheDefault()
    {
        using var backup = new SettingsBackup();
        SettingsBackup.SetRaw("DivideEdgeDistance", "not a number");
        var settings = new Settings();

        settings.LoadSettings();

        await Assert.That((double)settings.DivideEdgeDistance).IsEqualTo(5000 / 304.8).Within(1e-6);
    }

    [Test]
    public async Task LoadSettings_CorruptFile_KeepsTheDefaults()
    {
        using var backup = new SettingsBackup();
        SettingsBackup.WriteFile("{ this is not json");
        var settings = new Settings();

        settings.LoadSettings();

        await Assert.That(settings.SingleElement).IsTrue();
        await Assert.That((double)settings.DivideEdgeDistance).IsEqualTo(5000 / 304.8).Within(1e-6);
    }

    // ---- migration from the old registry location ---------------------------------------------------------------

    [Test]
    public async Task LoadSettings_OldRegistryKey_IsMovedToTheJsonFile()
    {
        using var backup = new SettingsBackup();
        SettingsBackup.SetLegacy("SingleElement", "False");
        SettingsBackup.SetLegacy("DivideEdgeDistance", 3.25m.ToString());
        SettingsBackup.SetLegacy("SweepCategory", "OST_Roads");
        var settings = new Settings();

        settings.LoadSettings();

        await Assert.That(settings.SingleElement).IsFalse();
        await Assert.That(settings.DivideEdgeDistance).IsEqualTo(3.25m);
        await Assert.That(settings.SweepCategory).IsEqualTo("OST_Roads");
        await Assert.That(SettingsBackup.FileExists()).IsTrue();
        await Assert.That(SettingsBackup.LegacyKeyExists()).IsFalse();

        var reloaded = new Settings();
        reloaded.LoadSettings();
        await Assert.That(reloaded.SingleElement).IsFalse();
        await Assert.That(reloaded.DivideEdgeDistance).IsEqualTo(3.25m);
    }

    [Test]
    public async Task LoadSettings_JsonFileWins_OverTheOldRegistryKey()
    {
        using var backup = new SettingsBackup();
        new Settings { SingleElement = true }.SaveSettings();
        SettingsBackup.SetLegacy("SingleElement", "False");
        var settings = new Settings();

        settings.LoadSettings();

        await Assert.That(settings.SingleElement).IsTrue();
        await Assert.That(SettingsBackup.LegacyKeyExists()).IsTrue();
    }

    [Test]
    public async Task SettingsBackup_RedirectsAndRestoresTheSettingsLocations()
    {
        // the helper protects real user settings, so it deserves a test of its own
        var realFile = Settings.FilePath;
        var realKey = Settings.LegacyRegistryKeyPath;
        string duringFile;

        using (new SettingsBackup())
        {
            duringFile = Settings.FilePath;
            new Settings().SaveSettings();
            await Assert.That(duringFile).IsNotEqualTo(realFile);
            await Assert.That(Settings.LegacyRegistryKeyPath).IsNotEqualTo(realKey);
            await Assert.That(File.Exists(duringFile)).IsTrue();
        }

        await Assert.That(Settings.FilePath).IsEqualTo(realFile);
        await Assert.That(Settings.LegacyRegistryKeyPath).IsEqualTo(realKey);
        await Assert.That(File.Exists(duringFile)).IsFalse();
    }
}

public class ClientIdProviderTests : TopoAlignTest
{
    private static string NewFolderName() => "TopoAlignTests_" + Guid.NewGuid().ToString("N");

    private static string FolderPath(string folder)
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), folder);
    }

    private static void Cleanup(string folder)
    {
        try
        {
            Directory.Delete(FolderPath(folder), true);
        }
        catch
        {
            // best effort
        }
    }

    [Test]
    public async Task GetOrCreateClientId_CreatesA32CharacterHexIdAndPersistsIt()
    {
        var folder = NewFolderName();
        try
        {
            var id = ClientIdProvider.GetOrCreateClientId(folder, "id.txt");

            await Assert.That(id.Length).IsEqualTo(32);
            await Assert.That(id.All(Uri.IsHexDigit)).IsTrue();
            await Assert.That(File.ReadAllText(Path.Combine(FolderPath(folder), "id.txt"))).IsEqualTo(id);
        }
        finally
        {
            Cleanup(folder);
        }
    }

    [Test]
    public async Task GetOrCreateClientId_CalledAgain_ReturnsTheSameId()
    {
        var folder = NewFolderName();
        try
        {
            var first = ClientIdProvider.GetOrCreateClientId(folder, "id.txt");
            var second = ClientIdProvider.GetOrCreateClientId(folder, "id.txt");

            await Assert.That(second).IsEqualTo(first);
        }
        finally
        {
            Cleanup(folder);
        }
    }

    [Test]
    public async Task GetOrCreateClientId_ReusesAnExistingIdAndTrimsIt()
    {
        var folder = NewFolderName();
        try
        {
            Directory.CreateDirectory(FolderPath(folder));
            File.WriteAllText(Path.Combine(FolderPath(folder), "id.txt"), "  existing-id \r\n");

            var id = ClientIdProvider.GetOrCreateClientId(folder, "id.txt");

            await Assert.That(id).IsEqualTo("existing-id");
        }
        finally
        {
            Cleanup(folder);
        }
    }

    [Test]
    public async Task GetOrCreateClientId_BlankFile_IsReplacedWithANewId()
    {
        var folder = NewFolderName();
        try
        {
            Directory.CreateDirectory(FolderPath(folder));
            File.WriteAllText(Path.Combine(FolderPath(folder), "id.txt"), "   ");

            var id = ClientIdProvider.GetOrCreateClientId(folder, "id.txt");

            await Assert.That(id.Length).IsEqualTo(32);
        }
        finally
        {
            Cleanup(folder);
        }
    }

    [Test]
    public async Task GetOrCreateClientId_DifferentFolders_GetDifferentIds()
    {
        var one = NewFolderName();
        var two = NewFolderName();
        try
        {
            var first = ClientIdProvider.GetOrCreateClientId(one, "id.txt");
            var second = ClientIdProvider.GetOrCreateClientId(two, "id.txt");

            await Assert.That(first).IsNotEqualTo(second);
        }
        finally
        {
            Cleanup(one);
            Cleanup(two);
        }
    }

    [Test]
    public async Task GetOrCreateClientId_UnusableFolderName_FallsBackToAStableMachineHash()
    {
        // '|' is not allowed in a Windows path, so creating the folder fails
        var first = ClientIdProvider.GetOrCreateClientId("bad|folder", "id.txt");
        var second = ClientIdProvider.GetOrCreateClientId("bad|folder", "id.txt");

        await Assert.That(first.Length).IsEqualTo(64);
        await Assert.That(first).IsEqualTo(first.ToUpperInvariant());
        await Assert.That(first.All(Uri.IsHexDigit)).IsTrue();
        await Assert.That(second).IsEqualTo(first);
    }
}

public class ModelTests : TopoAlignTest
{
    [Test]
    public async Task DisplayUnitModel_HoldsUnitAndLabel()
    {
        var model = new DisplayUnitModel { DisplayUnit = UnitTypeId.Millimeters, Label = "Millimeters" };

        await Assert.That(model.DisplayUnit).IsEqualTo(UnitTypeId.Millimeters);
        await Assert.That(model.Label).IsEqualTo("Millimeters");
    }

    [Test]
    public async Task TopoItemModel_HoldsIdAndLabel()
    {
        var model = new TopoItemModel { Id = 4294967296L, Label = "Site topo" };

        await Assert.That(model.Id).IsEqualTo(4294967296L);
        await Assert.That(model.Label).IsEqualTo("Site topo");
    }
}
