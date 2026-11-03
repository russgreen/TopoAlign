# TopoAlign
![Revit Version](https://img.shields.io/badge/Revit%20Version-2022_--_2024-blue.svg) ![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg) <br>
![Revit Version](https://img.shields.io/badge/Revit%20Version-2025_--_2027-blue.svg) ![.NET](https://img.shields.io/badge/.NET-10-blue.svg) 

![GitHub last commit](https://img.shields.io/github/last-commit/russgreen/topoalign) 

For features, usage instructions and known limitations see https://russgreen.github.io/TopoAlign/


# Running the unit tests
The tests in `TopoAlign.Tests` use [RevitUnit](https://github.com/Nice3point/RevitUnit) (TUnit running inside a real Revit session), so a licensed Revit matching the configuration must be installed. They are available for the Revit 2025+ configurations only (TUnit needs .NET 8+).

```
cd TopoAlign.Tests
dotnet run -c "Debug R27"
```

Building the tests also builds `TopoAlign`, which substitutes the Google Analytics keys into `App.cs` during the build, so `ApiSecret.txt` and `MeasurementId.txt` need to be present in the repository root as for a normal build.

The settings and view model tests back up and restore the `HKCU\SOFTWARE\RGTools\TopoAlign` registry key, so your own TopoAlign settings are left as they were.
